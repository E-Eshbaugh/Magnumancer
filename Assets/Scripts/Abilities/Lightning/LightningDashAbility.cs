using UnityEngine;
using Magnumancer.Abilities;
using UnityEngine.InputSystem;
using System.Collections;

public class LightningTeleportAbility : MonoBehaviour, IActiveAbility
{
    [SerializeField] float teleportDistance = 10f;
    [SerializeField] float isoYaw = 45f;
    [SerializeField] GameObject lightningStrikeEffectPrefab;
    [SerializeField] LayerMask groundMask;
    public AudioClip thunderSound;
    public AudioSource audioSource;

    public void Activate(GameObject caster)
    {
        //damage at start
        var blast = caster.GetComponent<LightningBlastDamage>();
        if (blast != null)
        {
            blast.TriggerBlast(caster.transform.position, caster);
        }
        Debug.Log("LightningTeleport activated!");
        // Use the caster's own pad (assigned by MultiplayerManager)
        Gamepad gamepad = caster.TryGetComponent<PlayerMovement3D>(out var movement) ? movement.gamepad : null;
        Rumble.Play(gamepad, 0.6f, 1.0f, 0.2f); // ⚡ Light + heavy motor

        audioSource = caster.GetComponent<AudioSource>();
        if (audioSource && thunderSound)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(thunderSound);
        }
        else if (!audioSource)
        {
            Debug.LogWarning("LightningTeleport: No AudioSource found on caster.");
            if (!thunderSound)
                Debug.LogWarning("LightningTeleport: Thunder sound will not play because AudioClip is missing.");
        }

        // 🦶 Grab the feet transform from the caster
        Transform feet = caster.transform.Find("PlayerFeetPos");
        Vector3 feetStart = feet ? feet.position : caster.transform.position;
        Debug.Log("Player position start: " + caster.transform.position);
        Debug.Log("Feet position start: " + feetStart);

        // 🎮 Read stick input
        Vector2 stick = gamepad?.leftStick.ReadValue() ?? Vector2.zero;
        Vector3 dir3 = new Vector3(stick.x, 0f, stick.y);

        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = caster.transform.forward;
        else
        {
            if (Mathf.Abs(isoYaw) > 0.01f)
                dir3 = Quaternion.Euler(0f, isoYaw, 0f) * dir3;
            dir3.Normalize();
        }

        caster.transform.forward = dir3;

        // 🧱 Stop short of walls: sweep the player's capsule along the teleport direction.
        // Ground (so ramps don't block), players/monsters and triggers are ignored.
        CharacterController cc = caster.GetComponent<CharacterController>();
        float radius = cc ? cc.radius : 0.5f;
        float height = cc ? cc.height : 2f;
        Vector3 capBottom = feetStart + Vector3.up * (radius + 0.35f); // lifted over small steps
        Vector3 capTop = feetStart + Vector3.up * Mathf.Max(radius + 0.35f, height - radius);
        int wallMask = ~(LayerMask.GetMask("Player") | groundMask.value);
        float maxDistance = teleportDistance;
        if (Physics.CapsuleCast(capBottom, capTop, radius, dir3, out RaycastHit wallHit, teleportDistance,
                wallMask, QueryTriggerInteraction.Ignore))
            maxDistance = Mathf.Max(0f, wallHit.distance - 0.1f);

        // 🎯 Find ground at the destination; if there's none (map edge), back off toward the start.
        // Prefer the ground layer, but accept any solid non-player surface so floors on other
        // layers still work. If nothing is found at all, the caster stays put.
        Vector3 feetEnd = feetStart;
        if (!FindLanding(feetStart, dir3, maxDistance, groundMask.value, out feetEnd))
            FindLanding(feetStart, dir3, maxDistance, ~LayerMask.GetMask("Player"), out feetEnd);

        // ⚡ Strike at feetStart
        if (lightningStrikeEffectPrefab)
            Instantiate(lightningStrikeEffectPrefab, feetStart, Quaternion.identity);

        // 🧍 Move player
        if (cc)
        {
            cc.enabled = false;
            caster.transform.position = feetEnd;
            cc.enabled = true;
        }
        else
            caster.transform.position = feetEnd;

        // ✅ Strike at feetEnd directly
        if (lightningStrikeEffectPrefab)
            Instantiate(lightningStrikeEffectPrefab, feetEnd, Quaternion.identity);

        Debug.Log("Player position end: " + caster.transform.position);
        Debug.Log("Feet position end: " + feetEnd);

        //damage at end part
        if (blast != null)
        {
            blast.TriggerBlast(caster.transform.position, caster);
        }

    }

    static bool FindLanding(Vector3 start, Vector3 dir, float maxDistance, int mask, out Vector3 landing)
    {
        for (float d = maxDistance; d > 0f; d -= 0.5f)
        {
            Vector3 origin = start + dir * d + Vector3.up * 10f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, mask, QueryTriggerInteraction.Ignore))
            {
                landing = hit.point;
                return true;
            }
        }
        landing = start;
        return false;
    }


}

