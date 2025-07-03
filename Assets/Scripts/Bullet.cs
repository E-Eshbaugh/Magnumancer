using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [Tooltip("Speed in units/sec")]
    public float speed = 20f;

    [Header("Explosion Flash")]
    [Tooltip("How bright the light gets when you hit something")]
    public float flashIntensity = 8f;
    [Tooltip("How long (seconds) the flash lasts before destroying the bullet")]
    public float flashDuration = 0.1f;

    private Vector3 _direction;
    private Light   _light;
    private float   _originalIntensity;

    void Start()
    {
        // Cache the Light component (on this object or in children)
        _light = GetComponentInChildren<Light>();
        if (_light != null)
            _originalIntensity = _light.intensity;
        else
            Debug.LogWarning("Bullet: no Light found for flash effect.");
    }

    /// <summary>
    /// Call this right after you instantiate the bullet.
    /// </summary>
    public void Initialize(Vector3 dir)
    {
        _direction = dir.normalized;
    }

    void Update()
    {
        transform.position += _direction * speed * Time.deltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Start the flash & destroy sequence if not hitting another bullet
        //check if colliding with another bullet
        if (collision.gameObject.CompareTag("Bullet"))
        {
            return;
        }

        if (_light != null)
            StartCoroutine(FlashAndDestroy());
        else
            Destroy(gameObject);
    }

    private IEnumerator FlashAndDestroy()
    {
        // Crank up the light
        _light.intensity = flashIntensity;

        // Wait for the flash duration
        yield return new WaitForSeconds(flashDuration);

        // (Optional) reset in case you have pooled bullets
        _light.intensity = _originalIntensity;

        // Finally, destroy the bullet
        Destroy(gameObject);
    }
}



