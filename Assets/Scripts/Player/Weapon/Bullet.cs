using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [Header("Damage & Motion")]
    public int damage = 10;
    [Tooltip("Speed in units/sec")]
    public float speed = 20f;
    [Tooltip("Seconds before a bullet that hits nothing is destroyed")]
    public float lifetime = 5f;

    [Header("Explosion Flash")]
    public float flashIntensity = 8f;
    public float flashDuration = 0.1f;

    [Tooltip("Shove per point of damage on a player hit (stacks across pellets, reduced by the target's gun weight)")]
    public float knockbackPerDamage = 0.18f;

    [HideInInspector] public GameObject owner; // player who fired it (damage credit)

    private Vector3 _direction;
    private Light   _light;
    private float   _originalIntensity;
    private bool    _hasHit;
    private BulletFX _fx;
    private Element _element;      // the shooter's element (reactions, statuses)
    const int FireVsIce = 2;
    private bool    _zoneReacted;  // a round only sets off one zone (gas, water, brambles)

    // internal target position computed in FixedUpdate
    private Vector3 _targetPosition;

    void Awake()
    {
        // Movement and hits are handled by our own raycasts. Keep physics from also
        // simulating the bullet (shoving grenades/mines/other pellets around).
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.isKinematic = true;
        foreach (var col in GetComponentsInChildren<Collider>())
            col.isTrigger = true;
    }

    void Start()
    {
        _light = GetComponentInChildren<Light>();
        if (_light != null)
            _originalIntensity = _light.intensity;
        else
            Debug.LogWarning("Bullet: no Light found for flash effect.");

        // initialize targetPosition to current
        _targetPosition = transform.position;

        // clean up bullets that miss everything
        Destroy(gameObject, lifetime);
    }

    public void Initialize(Vector3 dir)
    {
        _direction = dir.normalized;
        // tracer, trail and glow in the shooter's wizard colors (owner/damage are set by now)
        _fx = BulletFX.Attach(this, _direction);
        _element = Elements.Of(owner);
    }

    void FixedUpdate()
    {
        // already hit something; just waiting on the flash to finish
        if (_hasHit) return;

        // compute how far to move this physics step
        float moveDist = speed * Time.fixedDeltaTime;

        // raycast ahead (ignore triggers like poison clouds and lava trails)
        bool blocked = Physics.Raycast(_targetPosition, _direction, out RaycastHit hit, moveDist,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // rounds flying through zones react with them: fire ignites gas and brambles,
        // frost freezes water, lightning electrifies it, earth turns it to mud
        if (_element != Element.None && !_zoneReacted)
            _zoneReacted = ElementReactions.OnElementPass(_targetPosition,
                blocked ? hit.point : _targetPosition + _direction * moveDist, owner, _element);

        if (blocked)
        {
            HandleHit(hit.collider, hit.point, hit.normal);
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

    private void HandleHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
    {
        // 1) ignore other bullets
        if (hitCollider.GetComponentInParent<Bullet>() != null)
            return;

        _hasHit = true;

        // Mines and grenades can be shot to set them off
        bool setOff = Explosions.Shoot(hitCollider, owner);
        hitCollider.GetComponentInParent<IBulletImpact>()?.OnBulletHit(hitPoint, owner);

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
        {
            ph.TakeDamage(damage, owner);
            if (ph.movement != null && ph.gameObject != owner)
                ph.movement.AddKnockback(_direction * damage * knockbackPerDamage);
        }

        //iceWall effect
        var iceWall = hitCollider.GetComponent<IceWallEffect>();
        if (iceWall != null)
        {
            // fire melts ice walls twice as fast
            iceWall.TakeDamage(_element == Element.Fire ? damage * FireVsIce : damage);
        }
        else
        {
            var crystal = hitCollider.GetComponentInParent<CrystalHealth>();
            if (crystal != null)
            {
                crystal.TakeDamage(damage);
            }
        }

        //Goblin
        var goblin = hitCollider.GetComponent<GoblinHealth>();
        if (goblin != null)
            goblin.TakeDamage(damage, owner);

        var progWall = hitCollider.GetComponent<DestructibleWall>();
        if (progWall)
            progWall.TakeDamage(damage);

        // environment props wear down (fire burns wood faster, earth smashes stone...)
        var prop = hitCollider.GetComponentInParent<Destructible>();
        if (prop != null)
            prop.TakeDamage(damage, owner, hitPoint, _element);

        // the world itself (floors, walls) keeps a bullet hole or scorch mark
        bool solidWorld = ph == null && goblin == null && prop == null && iceWall == null && !setOff
                          && (hitCollider.attachedRigidbody == null || hitCollider.attachedRigidbody.isKinematic);
        if (solidWorld)
            ImpactMarks.Mark(hitPoint, hitNormal, _element, damage);

        // Elements: set off a reaction with whatever's on them, or leave our own status
        var struck = ph != null ? ph.gameObject : (goblin != null ? goblin.gameObject : null);
        if (struck != null && struck != owner)
            ElementReactions.BulletHit(struck, owner, damage, hitPoint);
        // 3) snap both target and actual to impact
        _targetPosition = hitPoint;
        transform.position = hitPoint;

        if (_fx != null)
            _fx.Impact(hitPoint, hitNormal, ph != null || goblin != null);

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
