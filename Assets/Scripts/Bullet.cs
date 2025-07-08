using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [Header("Damage & Motion")]
    public int damage = 10;
    [Tooltip("Speed in units/sec")]
    public float speed = 20f;

    [Header("Explosion Flash")]
    [Tooltip("How bright the light gets when you hit something")]
    public float flashIntensity = 8f;
    [Tooltip("How long (seconds) the flash lasts before destroying the bullet")]
    public float flashDuration = 0.1f;

    private Vector3 _direction;
    private Light _light;
    private float _originalIntensity;

    void Start()
    {
        // grab your light (if any)
        _light = GetComponentInChildren<Light>();
        if (_light != null)
            _originalIntensity = _light.intensity;
        else
            Debug.LogWarning("Bullet: no Light found for flash effect.");
    }

    /// <summary>
    /// Call this right after Instantiate so we know which way to go.
    /// </summary>
    public void Initialize(Vector3 dir)
    {
        _direction = dir.normalized;
    }

    void Update()
    {
        // how far we’ll move this frame
        float moveDist = speed * Time.deltaTime;

        // cast a short ray ahead
        if (Physics.Raycast(transform.position, _direction, out RaycastHit hit, moveDist))
        {
            HandleHit(hit.collider, hit.point);
            
        }
        else
        {
            // no hit → just move forward
            transform.position += _direction * moveDist;
        }
    }

    private void HandleHit(Collider hitCollider, Vector3 hitPoint)
    {
        Debug.Log($"[Bullet] Raycast hit: {hitCollider.name} (tag = {hitCollider.tag})");

        // 1) Ignore other bullets
        if (hitCollider.GetComponentInParent<Bullet>() != null)
            return;

        // 2) Try to find a PlayerHealthControl on the collider or any of its parents
        var ph = hitCollider.GetComponentInParent<PlayerHealthControl>();

        // 3) Fallback: overlap a tiny sphere at the hit point to catch the controller
        if (ph == null)
        {
            Collider[] c = Physics.OverlapSphere(hitPoint, 0.1f);
            foreach (var col in c)
            {
                ph = col.GetComponent<PlayerHealthControl>();
                if (ph != null) break;
            }
        }

        if (ph != null)
        {
            Debug.Log($"[Bullet]  → Found PlayerHealthControl on {ph.gameObject.name}, dealing {damage}");
            ph.TakeDamage(damage);
        }
        else
        {
            Debug.Log($"[Bullet]  → No PlayerHealthControl found near {hitPoint}");
        }

        // 4) Snap to hit point & play flash/destroy
        transform.position = hitPoint;
        if (_light != null)
            StartCoroutine(FlashAndDestroy());
        else
            Destroy(gameObject);
    }




    private IEnumerator FlashAndDestroy()
    {
        // ramp up light
        _light.intensity = flashIntensity;

        // wait, then clean up
        yield return new WaitForSeconds(flashDuration);
        _light.intensity = _originalIntensity;
        Destroy(gameObject);
    }
}
