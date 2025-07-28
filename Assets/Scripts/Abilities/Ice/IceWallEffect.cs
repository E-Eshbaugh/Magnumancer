using UnityEngine;
using System.Collections;

public class IceWallEffect : MonoBehaviour
{
    [Header("Rise Settings")]
    [SerializeField] float riseFromBelow = 3f;
    [SerializeField] float riseSpeed = 10f;
    [SerializeField] float raycastHeight = 5f;
    [SerializeField] LayerMask groundLayers;

    [Header("Health Settings")]
    [SerializeField]
    int maxHealth = 150;
    [SerializeField] GameObject destroyVFX; // optional
    [SerializeField] AudioClip destroySFX; // optional
    [SerializeField] AudioSource audioSource;

    private int currentHealth;
    private float groundY;

    public void BeginRise()
    {
        currentHealth = maxHealth;

        Vector3 above = transform.position + Vector3.up * raycastHeight;
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, raycastHeight + 10f, groundLayers))
        {
            groundY = hit.point.y;

            Vector3 below = hit.point;
            below.y -= riseFromBelow;
            transform.position = below;

            StartCoroutine(RiseToGround());
        }
        else
        {
            Debug.LogWarning("IceWallEffect: Could not find valid ground.");
            Destroy(gameObject);
        }
    }

    IEnumerator RiseToGround()
    {
        while (transform.position.y < groundY)
        {
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;
            yield return null;
        }

        Vector3 pos = transform.position;
        pos.y = groundY;
        transform.position = pos;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Optional: flash, shake, ice crack sound
        }
    }

    private void Die()
    {
        if (destroyVFX)
            Instantiate(destroyVFX, transform.position, Quaternion.identity);

        if (destroySFX && audioSource)
            audioSource.PlayOneShot(destroySFX);

        Destroy(gameObject);
    }
}
