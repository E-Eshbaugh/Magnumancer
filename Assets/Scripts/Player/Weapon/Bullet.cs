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
    [HideInInspector] public int structureDamage = -1; // damage to props, walls and crystals (-1 = same as damage)
    [HideInInspector] public Element element;  // set before Initialize to carry another element (None = the shooter's)
    [HideInInspector] public bool forged;      // Pack-a-Punch round: hits trigger the owner's rune power-up

    int StructureDamage => structureDamage >= 0 ? structureDamage : damage;

    private Vector3 _direction;
    private Light   _light;
    private float   _originalIntensity;
    private bool    _hasHit;
    private BulletFX _fx;
    private Element _element;      // the shooter's element (reactions, statuses), or the one it was loaded with
    private bool    _infused;      // carries an element other than the shooter's (Elemental Rounds, Ember Minigun)
    private bool    _zoneReacted;  // a round only sets off one zone (gas, water, brambles)
    private bool    _throughOwnWall; // forged round that flew through its owner's ice wall (Rampart)

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
        var own = Elements.Of(owner);
        _element = element != Element.None ? element : own;
        _infused = _element != own;
        // tracer, trail and glow in the shooter's wizard colors (owner/damage are set by now),
        // or in the loaded element's colors
        _fx = BulletFX.Attach(this, _direction, _infused ? _element : Element.None);
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

        // Rampart: a forged round flies through its owner's own ice wall
        if (blocked && forged && ForgedRunes.PassesOwnWall(owner, hit.collider))
        {
            _throughOwnWall = true;
            blocked = FirstHitPastOwnWall(moveDist, out hit);
        }

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

    static readonly RaycastHit[] passHits = new RaycastHit[16];

    bool FirstHitPastOwnWall(float moveDist, out RaycastHit first)
    {
        first = default;
        int n = Physics.RaycastNonAlloc(_targetPosition, _direction, passHits, moveDist,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (passHits[i].distance >= best || ForgedRunes.PassesOwnWall(owner, passHits[i].collider)) continue;
            best = passHits[i].distance;
            first = passHits[i];
        }
        return best < float.MaxValue;
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
        if (ph != null && Teams.CanHarm(ph.gameObject, owner))
        {
            ph.TakeDamage(damage, owner);
            if (ph.movement != null && ph.gameObject != owner)
                ph.movement.AddKnockback(_direction * damage * knockbackPerDamage);
        }

        //iceWall effect
        var iceWall = hitCollider.GetComponent<IceWallEffect>();
        if (iceWall != null)
        {
            // ice walls can't be broken, but fire melts them faster
            if (_element == Element.Fire) iceWall.Scorch(StructureDamage);
            else iceWall.TakeDamage(StructureDamage);
        }
        else
        {
            var crystal = hitCollider.GetComponentInParent<CrystalHealth>();
            if (crystal != null)
            {
                crystal.TakeDamage(StructureDamage);
            }
        }

        //Goblin
        var goblin = hitCollider.GetComponentInParent<GoblinHealth>();
        if (goblin != null)
            goblin.TakeDamage(damage, owner);

        var progWall = hitCollider.GetComponent<DestructibleWall>();
        if (progWall)
            progWall.TakeDamage(damage);

        // environment props wear down (fire burns wood faster, earth smashes stone...)
        var prop = hitCollider.GetComponentInParent<Destructible>();
        if (prop != null)
            prop.TakeDamage(StructureDamage, owner, hitPoint, _element);

        // the world itself (floors, walls) keeps a bullet hole or scorch mark
        bool solidWorld = ph == null && goblin == null && prop == null && iceWall == null && !setOff
                          && (hitCollider.attachedRigidbody == null || hitCollider.attachedRigidbody.isKinematic);
        if (solidWorld)
            ImpactMarks.Mark(hitPoint, hitNormal, _element, StructureDamage);

        // Elements: set off a reaction with whatever's on them, or leave our own status
        var struck = ph != null ? ph.gameObject : (goblin != null ? goblin.gameObject : null);
        if (struck != null && struck != owner)
            ElementReactions.BulletHit(struck, owner, damage, hitPoint, _infused ? _element : Element.None);
        if (forged && struck != null && struck != owner)
            ForgedRunes.OnForgedHit(owner, struck, damage, hitPoint, _direction, _throughOwnWall);
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
