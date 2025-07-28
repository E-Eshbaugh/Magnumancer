using UnityEngine;
using Magnumancer.Abilities;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal; // or your actual namespace

public class FireDashAbility : MonoBehaviour, IActiveAbility
{
    [Tooltip("Meters to travel if unobstructed.")]
    [SerializeField] float fireDashDistance = 8f;
    [Tooltip("Seconds dash takes to complete.")]
    [SerializeField] float fireDashTime = 0.15f;
    [SerializeField] float lavaLifeTime = 5f;
    [Tooltip("Rotate stick input for isometric worlds.")]
    [SerializeField] float fireDashIsoYaw = 45f;
    public PlayerHealthControl playerHealthControl;
    public CharacterController _cc;
    bool _fireDashing;
    [SerializeField] GameObject lavaTrailPrefab;
    [SerializeField] LayerMask enemyLayer;
    [SerializeField] float enemyHitboxRadius = 1f;
    [SerializeField] int dashDamage = 40;
    [SerializeField] float lavaSpawnInterval = 0.05f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    public void Activate(GameObject caster)
    {
        Debug.Log("FireDash activated!");
        if (_fireDashing) return;

        Vector2 stick = Vector2.zero;
        var playerInput = caster.GetComponent<PlayerInput>();
        if (playerInput != null && Gamepad.current != null)
        {
            stick = Gamepad.current.leftStick.ReadValue();
        }

        Vector3 dir3 = new Vector3(stick.x, 0f, stick.y);

        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = caster.transform.forward;
        else
        {
            if (Mathf.Abs(fireDashIsoYaw) > 0.01f)
                dir3 = Quaternion.Euler(0f, fireDashIsoYaw, 0f) * dir3;

            dir3.Normalize();
        }

        caster.transform.forward = dir3;
        caster.GetComponent<MonoBehaviour>().StartCoroutine(FireDashRoutine(caster, dir3));
    }

    System.Collections.IEnumerator FireDashRoutine(GameObject caster, Vector3 dir)
    {
        _fireDashing = true;
        var phc = caster.GetComponent<PlayerHealthControl>();
        if (phc != null) phc.invincible = true;

        // Spawn 1 lava trail at the start of dash — snap to ground if possible
        Quaternion rotation = Quaternion.LookRotation(dir);
        Vector3 rayOrigin = caster.transform.position + Vector3.up * 1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f))
        {
            Vector3 spawnPos = hit.point + Vector3.up * 0.02f;
            Instantiate(lavaTrailPrefab, spawnPos, rotation);
        }
        else
        {
            Vector3 fallbackPos = caster.transform.position + Vector3.up * 0.2f;
            Instantiate(lavaTrailPrefab, fallbackPos, rotation);
        }

        float elapsed = 0f;
        Vector3 startPos = caster.transform.position;
        float totalDist = fireDashDistance;
        Vector3 stepVel = dir * (totalDist / fireDashTime);

        CharacterController cc = caster.GetComponent<CharacterController>();
        HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

        while (elapsed < fireDashTime)
        {
            float dt = Time.deltaTime;
            Vector3 step = stepVel * dt;

            if (cc != null)
                cc.Move(step);
            else
                caster.transform.position += step;

            Collider[] hits = Physics.OverlapSphere(caster.transform.position, enemyHitboxRadius, enemyLayer);
            foreach (var h in hits)
            {
                if (!hitEnemies.Contains(h.gameObject))
                {
                    var enemyHealth = h.GetComponent<PlayerHealthControl>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.TakeDamage(dashDamage);
                        hitEnemies.Add(h.gameObject);
                    }
                }
            }

            elapsed += dt;
            yield return null;
        }

        // Snap to final position
        Vector3 desiredEnd = startPos + dir * totalDist;
        Vector3 remain = desiredEnd - caster.transform.position;

        if (cc != null)
            cc.Move(remain);
        else
            caster.transform.position += remain;

        _fireDashing = false;

        // Remove invincibility after lava life
        yield return new WaitForSeconds(lavaLifeTime);
        if (phc != null) phc.invincible = false;
    }


}
