using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Magnumancer.Abilities;

/// Tidebound — Rune III: Riptide. Surge forward in a torrent of water, shoving through
/// anyone in the way, leaving a swirling anchor where you started. Press Y again within
/// `returnWindow` seconds to rush back to the anchor (washing off freeze on the way).
public class RiptideAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 7f;
    public float surgeTime = 0.22f;
    public float returnTime = 0.16f;
    public float returnWindow = 3f;
    public float hitRadius = 1.3f;
    public float damage = 15f;
    public float shove = 12f;

    bool running;

    public void Activate(GameObject caster)
    {
        if (running) return;
        StartCoroutine(Run(caster));
    }

    IEnumerator Run(GameObject caster)
    {
        running = true;
        var cc = caster.GetComponent<CharacterController>();
        var pad = AbilityKit.Pad(caster);
        var wiz = AbilityKit.Wizard(caster);
        Color water = AbilityKit.Theme(caster);
        Color foam = Color.Lerp(water, Color.white, 0.75f);

        Vector3 anchor = AbilityKit.Ground(caster.transform.position + Vector3.up);
        Vector3 dir = AbilityKit.MoveDir(caster);
        caster.transform.forward = dir;

        // 1) burst of water at the start
        Splash(anchor, water, foam, wiz, 1f);
        var eddy = WhirlpoolFx.Spawn(anchor, 1.1f, returnWindow + surgeTime, water);
        CameraShake.Shake(0.15f, 0.2f);
        Rumble.Play(caster, 0.6f, 0.5f, 0.2f);

        // 2) the surge
        var wake = new GameObject("RiptideWake");
        var trail = GlowLine.Make(wake.transform, "Wake", 2, 0.5f, AbilityKit.Glow());
        var trailCore = GlowLine.Make(wake.transform, "WakeFoam", 2, 0.18f, AbilityKit.Glow());
        var path = new List<Vector3> { anchor + Vector3.up * 0.4f };
        var hit = new HashSet<GameObject>();
        float speed = distance / surgeTime;
        float nextSpray = 0f;

        for (float t = 0; t < surgeTime && !PlayerHealthControl.IsIncapacitated(caster.transform); t += Time.deltaTime)
        {
            Vector3 step = dir * speed * Time.deltaTime;
            if (cc != null && cc.enabled) cc.Move(step); else caster.transform.position += step;
            path.Add(caster.transform.position + Vector3.up * 0.4f);
            SetPath(trail, path, water, 0.8f);
            SetPath(trailCore, path, foam, 1f);

            if (Time.time >= nextSpray)
            {
                nextSpray = Time.time + 0.03f;
                PowerFx.Sparks(caster.transform.position + Vector3.up * 0.3f, foam, 6, 5f, 0.45f, 0.1f, 1.6f,
                               Vector3.up - dir * 0.6f, 70f);
            }

            foreach (var e in AbilityKit.Enemies(caster.transform.position + Vector3.up, hitRadius, caster))
            {
                if (!hit.Add(e)) continue;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (Vector3.Dot(e.transform.position - caster.transform.position, side) < 0f) side = -side;
                DamageEvents.Deal(e, damage, caster);
                AbilityKit.Knockback(e, (side * 0.8f + dir * 0.4f).normalized * shove);
                ElementReactions.AbilityHit(e, caster, damage);   // Soaked (or Conduct/Steam)
                Splash(e.transform.position, water, foam, wiz, 0.8f);
            }
            yield return null;
        }
        Vector3 landing = AbilityKit.Ground(caster.transform.position + Vector3.up);
        Splash(landing, water, foam, wiz, 1f);
        StartCoroutine(FadeWake(wake, trail, trailCore, water, foam, 0.6f));

        // 3) the return window: a current line points back to the anchor
        var current = GlowLine.Make(eddy.transform, "Current", 12, 0.08f, AbilityKit.Glow());
        yield return null; // don't count the casting press
        bool recalled = false;
        for (float t = 0; t < returnWindow; t += Time.deltaTime)
        {
            if (caster == null || !caster.activeInHierarchy || PlayerHealthControl.IsIncapacitated(caster.transform) || current == null) break;
            // flowing dashes along the way home
            Vector3 from = caster.transform.position + Vector3.up * 0.15f, to = anchor + Vector3.up * 0.15f;
            for (int i = 0; i < current.positionCount; i++)
            {
                float f = i / (float)(current.positionCount - 1);
                current.SetPosition(i, Vector3.Lerp(from, to, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.3f);
            }
            float blink = t > returnWindow - 0.8f ? (Mathf.Sin(Time.time * 25f) > 0f ? 1f : 0.3f) : 1f;
            GlowLine.SetColor(current, foam, (0.35f + 0.25f * Mathf.Sin(Time.time * 8f)) * blink);

            if (pad != null && pad.buttonNorth.wasPressedThisFrame && !GamePause.InputBlocked)
            {
                recalled = true;
                break;
            }
            yield return null;
        }
        if (current != null) Destroy(current.gameObject);

        // 4) rush home
        if (recalled && caster != null && !PlayerHealthControl.IsIncapacitated(caster.transform))
        {
            Vector3 start = caster.transform.position;
            var streak = new GameObject("RiptideReturn");
            var line = GlowLine.Make(streak.transform, "Rush", 2, 0.6f, AbilityKit.Glow());
            var core = GlowLine.Make(streak.transform, "RushFoam", 2, 0.2f, AbilityKit.Glow());
            Splash(start, water, foam, wiz, 0.9f);
            Rumble.Play(caster, 0.5f, 0.6f, 0.2f);
            for (float t = 0; t < returnTime && !PlayerHealthControl.IsIncapacitated(caster.transform); t += Time.deltaTime)
            {
                Vector3 p = Vector3.Lerp(start, anchor, t / returnTime);
                caster.GetComponent<PlayerMovement3D>()?.Teleport(p, caster.transform.rotation);
                var pts = new List<Vector3> { start + Vector3.up * 0.5f, p + Vector3.up * 0.5f };
                SetPath(line, pts, water, 0.9f);
                SetPath(core, pts, foam, 1f);
                yield return null;
            }
            if (!PlayerHealthControl.IsIncapacitated(caster.transform))
                caster.GetComponent<PlayerMovement3D>()?.Teleport(anchor, caster.transform.rotation);
            StatusEffects.Of(caster).ClearFreeze();          // the tide washes it off
            Splash(anchor, water, foam, wiz, 1.2f);
            AbilityKit.Shockwave(anchor, 2.5f, water, 0.35f);
            CameraShake.Shake(0.15f, 0.15f);
            StartCoroutine(FadeWake(streak, line, core, water, foam, 0.35f));
        }
        if (eddy != null) Destroy(eddy.gameObject, 0.1f);
        running = false;
    }

    void OnDisable() => running = false;

    static void SetPath(LineRenderer lr, List<Vector3> pts, Color c, float a)
    {
        lr.positionCount = pts.Count;
        lr.SetPositions(pts.ToArray());
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 1f));
        GlowLine.SetColor(lr, c, a);
    }

    static IEnumerator FadeWake(GameObject go, LineRenderer a, LineRenderer b, Color ca, Color cb, float time)
    {
        for (float t = 0; t < time; t += Time.deltaTime)
        {
            float k = 1f - t / time;
            if (a != null) { GlowLine.SetColor(a, ca, 0.8f * k); a.widthMultiplier = Mathf.Lerp(0.4f, 1f, k); }
            if (b != null) GlowLine.SetColor(b, cb, k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    static void Splash(Vector3 at, Color water, Color foam, WizardData wiz, float scale)
    {
        PowerFx.Prefab(wiz != null ? wiz.spawnEffectPrefab : null, at + Vector3.up * 0.2f, 2f, scale * 1.3f);
        PowerFx.Sparks(at + Vector3.up * 0.2f, foam, Mathf.RoundToInt(22 * scale), 6f * scale, 0.6f, 0.12f, 1.8f, Vector3.up, 90f);
        PowerFx.Puffs(at + Vector3.up * 0.3f, water, Mathf.RoundToInt(6 * scale), 2.5f, 0.6f * scale, 0.6f, additive: true);
        PowerFx.Flash(at + Vector3.up, water, 4f * scale, 5f, 0.25f);
    }
}
