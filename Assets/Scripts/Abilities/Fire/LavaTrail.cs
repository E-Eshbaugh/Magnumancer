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

    [Header("Target Settings")]
    [SerializeField] string playerTag = "Player";

    [HideInInspector] public GameObject owner; // set by FireDashAbility; immune to this trail

    private HashSet<GameObject> affectedPlayers = new();
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
    public void Consume() { }   // lava isn't used up by reactions

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
            foreach (var player in affectedPlayers)
            {
                var health = player.GetComponent<PlayerHealthControl>();
                if (health != null)
                {
                    health.TakeDamage(damagePerTick, owner);
                    ElementReactions.ZoneHit(player, owner, Element.Fire, damagePerTick);
                }
            }

            yield return new WaitForSeconds(tickInterval);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.tag.StartsWith(playerTag)) return;
        if (other.gameObject == owner) return;
        if (affectedPlayers.Contains(other.gameObject)) return;

        affectedPlayers.Add(other.gameObject);

        var move = other.GetComponent<PlayerMovement3D>();
        if (move != null)
            move.currentMoveSpeed *= slowMultiplier;

        Rumble.Hold(move?.gamepad, RumbleKey, 0.1f, 0.2f);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.tag.StartsWith(playerTag)) return;
        if (!affectedPlayers.Contains(other.gameObject)) return;

        affectedPlayers.Remove(other.gameObject);

        var move = other.GetComponent<PlayerMovement3D>();
        if (move != null)
            move.currentMoveSpeed /= slowMultiplier;

        Rumble.Release(move?.gamepad, RumbleKey);
    }

    string RumbleKey => "lava" + GetEntityId();

    void OnDestroy()
    {
        ElementZones.Unregister(this);
        foreach (var player in affectedPlayers)
        {
            var move = player.GetComponent<PlayerMovement3D>();
            if (move != null)
                move.currentMoveSpeed /= slowMultiplier;

            Rumble.Release(move?.gamepad, RumbleKey);
        }

        affectedPlayers.Clear();
    }
}
