using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voltborn — Lightning Reflex: after dashing or teleporting, your first attack within
/// 2 seconds also fires a high-speed piercing lightning bolt (hitscan, pierces enemies,
/// stops at walls).
/// </summary>
public class LightningReflexPassive : WizardPassive
{
    public float window = 2f;
    public float range = 40f;
    public float boltDamageMultiplier = 2f;
    public float minBoltDamage = 25f;
    [Tooltip("Keeps a sniper + bolt from one-shotting")]
    public float maxBoltDamage = 50f;

    [Header("Look")]
    public float boltWidth = 0.22f;
    public float boltLifetime = 0.15f;

    public Color glowColor = new Color(0.5f, 0.8f, 1f);

    float armedUntil;
    AmmoControl ammo;
    WizardAbilityController abilities;
    GunOrbitController orbit;
    FireController3D gun;

    static Material boltMaterial;

    public bool Armed => Time.time < armedUntil;

    public override void Init(WizardData data)
    {
        base.Init(data);
        ammo = GetComponentInChildren<AmmoControl>(true);
        abilities = GetComponentInChildren<WizardAbilityController>(true);
        orbit = GetComponentInChildren<GunOrbitController>(true);
        gun = GetComponentInChildren<FireController3D>(true);

        if (movement != null) movement.OnDash += Arm;
        if (abilities != null) abilities.OnAbilityActivated += Arm;
        if (ammo != null) ammo.OnFired += HandleFired;
    }

    void OnDestroy()
    {
        if (movement != null) movement.OnDash -= Arm;
        if (abilities != null) abilities.OnAbilityActivated -= Arm;
        if (ammo != null) ammo.OnFired -= HandleFired;
    }

    void Arm() => armedUntil = Time.time + window;

    // Faint electric flicker while the bolt is armed
    void Update() => Glow.Set(glowColor, Armed ? 0.6f : 0f, 25f);

    void HandleFired(int weaponDamage)
    {
        if (!Armed || !IsAlive) return;
        armedUntil = 0f;
        FireBolt(Mathf.Clamp(weaponDamage * boltDamageMultiplier, minBoltDamage, maxBoltDamage));
    }

    void FireBolt(float damage)
    {
        Vector3 origin = gun != null && gun.firePoint != null
            ? gun.firePoint.position
            : transform.position + Vector3.up * 1.2f;
        Vector3 dir = orbit != null ? orbit.aimDirection : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        var hits = Physics.RaycastAll(origin, dir, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        float end = range;
        var struck = new HashSet<GameObject>();
        foreach (var hit in hits)
        {
            var target = DamageEvents.RootOf(hit.collider);
            if (target == gameObject) continue;

            if (IsEnemy(target))
            {
                // pierce through enemies
                if (struck.Add(target))
                {
                    DamageEvents.Deal(target, damage, gameObject);
                    ElementReactions.AbilityHit(target, gameObject, damage);
                }
                continue;
            }

            // anything solid stops the bolt (ice walls take the hit)
            var ice = hit.collider.GetComponent<IceWallEffect>();
            if (ice != null) ice.TakeDamage(Mathf.RoundToInt(damage));
            end = hit.distance;
            break;
        }

        Vector3 endPoint = origin + dir * end;
        DrawBolt(origin, endPoint);
        SpawnEffect(endPoint, 2f);
    }

    void DrawBolt(Vector3 from, Vector3 to)
    {
        var go = new GameObject("LightningReflexBolt");
        var lr = go.AddComponent<LineRenderer>();
        GlowLine.Configure(lr, 2, boltWidth, GetBoltMaterial());
        Color theme = GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(wizard));
        lr.startColor = Color.Lerp(theme, Color.white, 0.4f);
        lr.endColor = theme;

        // jagged bolt: random sideways offsets, pinned at both ends
        const int segments = 14;
        Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = i / (segments - 1f);
            float jag = (i == 0 || i == segments - 1) ? 0f : Random.Range(-0.35f, 0.35f);
            lr.SetPosition(i, Vector3.Lerp(from, to, t) + side * jag + Vector3.up * Random.Range(-0.15f, 0.15f) * jag);
        }

        Destroy(go, boltLifetime);
    }

    Material GetBoltMaterial()
    {
        // bright additive glow (the old Sprites/Default bolt read dark, like the lasers did)
        if (boltMaterial == null) boltMaterial = GlowLine.CreateMaterial(3f);
        return boltMaterial;
    }
}
