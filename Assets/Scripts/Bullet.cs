using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("Movement")]
    public float speed    = 20f;
    public float lifetime = 5f;

    [Header("Flash")]
    public Light bulletLight;                 // Assign in prefab (child light)
    public float flashIntensity   = 5f;       // peak intensity on hit
    public float flashDuration    = 0.1f;     // how long to hold the flash
    public GameObject explosionVFXPrefab;     // optional

    private Vector3 direction;
    private float   originalIntensity;

    void Start()
    {
        Destroy(gameObject, lifetime);
        if (bulletLight != null)
            originalIntensity = bulletLight.intensity;
    }

    void Update()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction * speed;
    }

    public void Fire(Vector3 dir)
    {
        direction = dir.normalized;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Spawn explosion VFX if you have one
        if (explosionVFXPrefab != null)
            Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);

        // Flash the light, then destroy
        if (bulletLight != null)
            StartCoroutine(FlashAndDie());
        else
            Destroy(gameObject);
    }

    private IEnumerator FlashAndDie()
    {
        // Ramp up
        bulletLight.intensity = flashIntensity;
        // Optional: also increase range for a quick “bang”
        float origRange = bulletLight.range;
        bulletLight.range = origRange * 2f;

        yield return new WaitForSeconds(flashDuration);

        // Reset (or skip if you’re about to destroy)
        bulletLight.intensity = originalIntensity;
        bulletLight.range     = origRange;

        Destroy(gameObject);
    }
}


