using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Emberblast (WeaponData.remoteDetonate) — LT: Remote Fuse.
/// Hold LT and your grenades won't go off on their own (up to maxHoldTime after
/// each was fired); let go and every held grenade detonates at once. Lob a few
/// around a corner, wait for someone to walk in, release.
/// </summary>
[RequireComponent(typeof(AmmoControl))]
public class RemoteDetonator : MonoBehaviour
{
    public Gamepad gamepad;

    [Tooltip("Longest a grenade can be kept live after it's fired")]
    public float maxHoldTime = 5f;
    public float triggerThreshold = 0.2f;

    AmmoControl ammoControl;
    WeaponAbilityControl abilityBar;
    GameObject owner;
    bool holding;

    public void Setup(Gamepad pad) => gamepad = pad;

    void Awake()
    {
        ammoControl = GetComponent<AmmoControl>();
        var player = GetComponentInParent<PlayerMovement3D>();
        owner = player != null ? player.gameObject : null;
    }

    void Start() => abilityBar = WeaponAbilityControl.FindFor(this);

    void Update()
    {
        var gun = ammoControl != null ? ammoControl.currentGun : null;
        bool active = gun != null && gun.remoteDetonate && gamepad != null;

        bool held = active && !GamePause.InputBlocked && gamepad.leftTrigger.ReadValue() > triggerThreshold;

        if (held)
        {
            holding = true;
            float longest = 0f;
            foreach (var g in GrenadeExplodeAfterDelay.Live)
            {
                if (!IsMine(g)) continue;
                float left = g.SpawnTime + maxHoldTime - Time.time;
                if (left <= 0f) continue; // held too long: let its fuse run out
                g.HoldFuse(0.1f);
                longest = Mathf.Max(longest, left);
            }
            // bar drains as your newest grenade nears the hold limit
            abilityBar?.ReportFill(longest > 0f ? longest / maxHoldTime : 1f);
        }
        else if (holding)
        {
            holding = false;
            // Swapping guns or pausing doesn't blow them — only releasing LT does
            if (active && !GamePause.InputBlocked)
            {
                Rumble.Play(gamepad, 0f, 0.5f, 0.08f, fade: false); // the "click" (blasts rumble on their own)
                DetonateAll();
            }
        }
    }

    bool IsMine(GrenadeExplodeAfterDelay g) => g != null && !g.HasExploded && g.owner == owner;

    void DetonateAll()
    {
        // copy: detonations remove themselves from Live
        var live = GrenadeExplodeAfterDelay.Live.ToArray();
        foreach (var g in live)
            if (IsMine(g)) g.Detonate(0f);
    }
}
