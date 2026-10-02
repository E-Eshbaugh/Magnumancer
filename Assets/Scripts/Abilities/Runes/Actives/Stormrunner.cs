using UnityEngine;

/// The Stormrunner state on a Voltborn player: dashes become blinks with lightning fences
public class Stormrunner : MonoBehaviour
{
    public float blinkDistance = 5f;
    [Range(0f, 1f)] public float dashCooldownMultiplier = 0.15f;

    PlayerMovement3D movement;
    float until;
    bool active;
    Color color, core;
    LineRenderer[] arcs;
    float nextArc;

    const string Key = "stormrunner";

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        arcs = new LineRenderer[3];
        for (int i = 0; i < arcs.Length; i++)
        {
            arcs[i] = GlowLine.Make(transform, "StormArc", 6, 0.07f, AbilityKit.Glow());
            arcs[i].enabled = false;
        }
    }

    public void Begin(float duration, Color theme)
    {
        color = theme;
        core = Color.Lerp(theme, Color.white, 0.65f);
        until = Time.time + duration;
        if (!active && movement != null)
        {
            movement.OnDash += Blink;
            movement.SetDashCooldownModifier(Key, dashCooldownMultiplier);
        }
        active = true;

        // a bolt from the sky charges them up
        var wiz = movement != null ? movement.wizard : null;
        PowerFx.Prefab(wiz != null ? wiz.passiveEffectPrefab : null, transform.position, 2f);
        AbilityKit.Zap(transform.position + Vector3.up * 14f, transform.position + Vector3.up, core, 0.2f, 0.4f);
        PowerFx.Flash(transform.position + Vector3.up, color, 10f, 8f, 0.4f);
        PowerFx.Sparks(transform.position + Vector3.up, core, 30, 7f, 0.5f, 0.07f, 1f);
        AbilityKit.Shockwave(transform.position, 3f, color, 0.35f);
        CameraShake.Shake(0.2f, 0.2f);
        Rumble.Play(gameObject, 0.5f, 1f, 0.3f);
        Sfx.Play(SfxId.ThunderMaul, transform.position, 0.8f, 1.1f);   // the sky answers
    }

    void Blink()
    {
        if (!active || movement == null) return;
        movement.CancelDash();

        Vector3 dir = movement.MoveDirection;
        Vector3 from = transform.position;
        float dist = blinkDistance;
        // stop short of walls (players don't block a blink)
        var hits = Physics.RaycastAll(from + Vector3.up, dir, blinkDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (DamageEvents.IsCombatant(DamageEvents.RootOf(h.collider)) || h.collider.transform.IsChildOf(transform)) continue;
            dist = Mathf.Min(dist, Mathf.Max(0f, h.distance - 0.6f));
        }
        Vector3 to = from + dir * dist;
        movement.Teleport(to, Quaternion.LookRotation(dir));

        new GameObject("LightningFence").AddComponent<LightningFence>().Init(gameObject, from, to, color);
        PowerFx.Flash(from + Vector3.up, color, 5f, 4f, 0.2f);
        PowerFx.Flash(to + Vector3.up, core, 6f, 4f, 0.25f);
        PowerFx.Sparks(from + Vector3.up, core, 14, 5f, 0.35f, 0.06f, 1f);
        PowerFx.Sparks(to + Vector3.up, core, 18, 6f, 0.4f, 0.06f, 1f);
        Rumble.Play(gameObject, 0.2f, 0.7f, 0.12f);
        Sfx.Play(SfxId.Blink, to);
        Sfx.Play(SfxId.Zap, to, 0.7f);
    }

    void Update()
    {
        if (!active) return;
        if (Time.time >= until) { End(); return; }

        // crackling around their body, and a pulsing electric glow
        if (Time.time >= nextArc)
        {
            nextArc = Time.time + 0.06f;
            foreach (var arc in arcs)
            {
                if (Random.value < 0.3f) { arc.enabled = false; continue; }
                Vector3 c = transform.position + Vector3.up * Random.Range(0.4f, 1.6f);
                Vector3 a = c + Random.onUnitSphere * 0.55f, b = c + Random.onUnitSphere * 0.55f;
                GlowLine.Bolt(arc, a, b, 0.2f);
                GlowLine.SetColor(arc, core, Random.Range(0.6f, 1f));
                arc.enabled = true;
            }
            if (Random.value < 0.5f)
                PowerFx.Sparks(transform.position + Vector3.up * Random.Range(0.3f, 1.5f), color, 3, 3f, 0.25f, 0.05f, 1f);
        }
        float left = until - Time.time;
        PassiveGlow.On(gameObject).Set(color, left < 1f ? 0.5f : 1f, left < 1f ? 25f : 12f);
    }

    void End()
    {
        active = false;
        if (movement != null)
        {
            movement.OnDash -= Blink;
            movement.SetDashCooldownModifier(Key, 1f);
        }
        foreach (var arc in arcs) if (arc != null) arc.enabled = false;
        PassiveGlow.On(gameObject).Set(color, 0f);
        PowerFx.Sparks(transform.position + Vector3.up, color, 12, 3f, 0.4f, 0.05f, 1f);
    }

    void OnDisable() { if (active) End(); }
}
