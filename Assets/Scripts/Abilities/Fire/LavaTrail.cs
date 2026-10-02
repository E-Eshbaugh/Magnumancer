using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Blazing Ruin's molten trail. A fire zone: standing in it sets you Burning, and it
/// Combusts poison clouds it touches.
public class LavaTrail : MonoBehaviour, IElementZone
{
    [Header("Landing Behavior")]
    [SerializeField] float fallSpeed = 8f;
    [SerializeField] float groundCheckDistance = 10f;
    [SerializeField] float offsetY = 0.02f;
    [SerializeField] LayerMask groundMask;

    [Header("Effect Settings")]
    [SerializeField] int damagePerTick = 10;
    [SerializeField] float tickInterval = 1f;
    [SerializeField] float slowMultiplier = 0.5f;
    [SerializeField] float lifetime = 10f;

    [HideInInspector] public GameObject owner; // set by FireDashAbility; immune to this trail

    private readonly Dictionary<Collider, GameObject> occupants = new();
    private bool hasLanded = false;
    private bool firstFramePassed = false;

    Collider zone;

    void Start()
    {
        Destroy(gameObject, lifetime); // automatic cleanup
        StartCoroutine(DamageLoop());
        zone = GetComponent<Collider>();
    }

    // ---------- IElementZone ----------
    public Element ZoneElement => Element.Fire;
    public GameObject ZoneOwner => owner;
    public Vector3 ZoneCenter => zone != null ? zone.bounds.center : transform.position;
    public float ZoneRadius => zone != null ? Mathf.Min(zone.bounds.extents.x, zone.bounds.extents.z) + 0.5f : 1f;
    public float DistanceTo(Vector3 p) => zone != null ? ElementZones.FlatDistance(zone, p) : ElementZones.FlatDistance(transform.position, 1f, p);
    /// Water hit it (Steam): the lava cools into rock and stops burning
    public void Consume()
    {
        ElementZones.Unregister(this);
        Vector3 c = ZoneCenter;
        RockDebris.Burst(c, 10, 3f, 0.3f);
        RockDebris.Dust(c, 2f, 10);
        Destroy(gameObject);
    }

    void Update()
    {
        if (!firstFramePassed)
        {
            firstFramePassed = true;
            return;
        }

        if (hasLanded) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
        {
            transform.position = hit.point + Vector3.up * offsetY;
            hasLanded = true;
            ElementZones.Register(this); // on the ground now: react with clouds it landed in
            return;
        }

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }

    IEnumerator DamageLoop()
    {
        while (true)
        {
            foreach (var target in new HashSet<GameObject>(occupants.Values))
            {
                if (!DamageEvents.IsAlive(target)) continue;
                DamageEvents.Deal(target, damagePerTick, owner);
                ElementReactions.ZoneHit(target, owner, Element.Fire, damagePerTick);
            }
            yield return new WaitForSeconds(tickInterval);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var target = DamageEvents.RootOf(other);
        if (!DamageEvents.IsEnemy(target, owner) || !DamageEvents.IsAlive(target)) return;
        occupants[other] = target;
        StatusEffects.Of(target).SetSpeedModifier(RumbleKey, slowMultiplier);
        Rumble.Hold(target.GetComponent<PlayerMovement3D>()?.gamepad, RumbleKey, 0.1f, 0.2f);
    }

    void OnTriggerExit(Collider other)
    {
        if (!occupants.TryGetValue(other, out var target)) return;
        occupants.Remove(other);
        if (!occupants.ContainsValue(target)) Release(target);
    }

    void Release(GameObject target)
    {
        if (target == null) return;
        target.GetComponent<StatusEffects>()?.ClearSpeedModifier(RumbleKey);
        Rumble.Release(target.GetComponent<PlayerMovement3D>()?.gamepad, RumbleKey);
    }

    string RumbleKey => "lava" + GetEntityId();

    void OnDisable()
    {
        ElementZones.Unregister(this);
        foreach (var target in new HashSet<GameObject>(occupants.Values)) Release(target);
        occupants.Clear();
    }
}
