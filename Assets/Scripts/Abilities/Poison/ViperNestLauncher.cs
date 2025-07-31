using UnityEngine;
using Magnumancer.Abilities;

public class ViperNestAbility : MonoBehaviour, IActiveAbility
{
    [Header("Mine Settings")]
    public GameObject minePrefab;
    public float launchForce = 10f;
    public float arcUpwardFactor = 0.75f;
    public float delayBetweenMines = 0.15f;
    public int mineCount = 3;

    [Header("Spin Settings")]
    public float spinSpeed = 720f;

    [Header("Spread Settings")]
    public float spreadAngleDegrees = 10f; // Left/right arc in degrees

    private bool isRunning = false;

    public void Activate(GameObject caster)
    {
        if (isRunning || caster == null || minePrefab == null)
        {
            Debug.LogWarning("[ViperNestAbility] Activation failed.");
            return;
        }

        StartCoroutine(LaunchMines(caster));
    }

    private System.Collections.IEnumerator LaunchMines(GameObject caster)
    {
        isRunning = true;

        Transform firePoint = caster.transform.Find("GunPlaceHolder").Find("firePoint");
        Vector3 spawnPos = firePoint ? firePoint.position : caster.transform.position;

        Vector3 baseAim = caster.GetComponentInChildren<GunOrbitController>()?.aimDirection ?? caster.transform.forward;

        for (int i = 0; i < mineCount; i++)
        {
            // Calculate spread angle based on index
            float angleOffset = 0f;
            if (mineCount == 3)
            {
                angleOffset = (i - 1) * spreadAngleDegrees; // -1 → left, 0 → center, +1 → right
            }

            Vector3 spreadDirection = Quaternion.Euler(0f, angleOffset, 0f) * baseAim;
            LaunchMine(spawnPos, spreadDirection);
            yield return new WaitForSeconds(delayBetweenMines);
        }

        isRunning = false;
    }

    private void LaunchMine(Vector3 spawnPosition, Vector3 aimDirection)
    {
        GameObject mine = Instantiate(minePrefab, spawnPosition, Quaternion.identity);

        Rigidbody rb = mine.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 arcDir = (aimDirection.normalized + Vector3.up * arcUpwardFactor).normalized;
            rb.linearVelocity = arcDir * launchForce;
            rb.angularVelocity = Random.onUnitSphere * spinSpeed * Mathf.Deg2Rad;
        }

        Debug.DrawRay(spawnPosition, aimDirection * 3f, Color.yellow, 2f);
    }
}
