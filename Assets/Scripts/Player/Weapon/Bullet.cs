using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [Header("Damage & Motion")]
    public int damage = 10;
    [Tooltip("Speed in units/sec")]
    public float speed = 20f;

    [Header("Explosion Flash")]
    public float flashIntensity = 8f;
    public float flashDuration = 0.1f;

    private Vector3 _direction;
    private Light   _light;
    private float   _originalIntensity;

    // internal target position computed in FixedUpdate
    private Vector3 _targetPosition;

    void Start()
    {
        _light = GetComponentInChildren<Light>();
        if (_light != null)
            _originalIntensity = _light.intensity;
        else
            Debug.LogWarning("Bullet: no Light found for flash effect.");

        // initialize targetPosition to current
        _targetPosition = transform.position;
    }

    public void Initialize(Vector3 dir)
    {
        _direction = dir.normalized;
    }

    void FixedUpdate()
    {
        // compute how far to move this physics step
        float moveDist = speed * Time.fixedDeltaTime;

        // raycast ahead
        if (Physics.Raycast(_targetPosition, _direction, out RaycastHit hit, moveDist))
        {
            HandleHit(hit.collider, hit.point);
            // after a hit we stop updating movement
        }
        else
        {
            // advance target position
            _targetPosition += _direction * moveDist;
        }
    }

    void Update()
    {
        // smooth render position toward target
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPosition,
            0.5f  // adjust between 0 (no smoothing) and 1 (snap)
        );
    }

    private void HandleHit(Collider hitCollider, Vector3 hitPoint)
    {
        // 1) ignore other bullets
        if (hitCollider.GetComponentInParent<Bullet>() != null)
            return;

        // 2) damage player if found
        var ph = hitCollider.GetComponentInParent<PlayerHealthControl>();
        if (ph == null)
        {
            Collider[] cols = Physics.OverlapSphere(hitPoint, 0.1f);
            foreach (var c in cols)
                if ((ph = c.GetComponentInParent<PlayerHealthControl>()) != null)
                    break;
        }
        if (ph != null)
            ph.TakeDamage(damage);

        //iceWall effect
        var wall = hitCollider.GetComponent<IceWallEffect>();
        if (wall != null)
        {
            wall.TakeDamage(damage);
        }

        // 3) snap both target and actual to impact
        _targetPosition = hitPoint;
        transform.position = hitPoint;

        // 4) flash/destroy
        if (_light != null)
            StartCoroutine(FlashAndDestroy());
        else
            Destroy(gameObject);
    }

    private IEnumerator FlashAndDestroy()
    {
        _light.intensity = flashIntensity;
        yield return new WaitForSeconds(flashDuration);
        _light.intensity = _originalIntensity;
        Destroy(gameObject);
    }
}
