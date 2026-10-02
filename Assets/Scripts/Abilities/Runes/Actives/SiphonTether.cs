using System.Collections.Generic;
using UnityEngine;

/// The living vine between a Verdant caster and their victim
public class SiphonTether : MonoBehaviour
{
    GameObject caster, target;
    PlayerHealthControl casterHealth;
    float duration, dps, breakRange, started, lashTime = 0.15f;
    float damagePending, healPending, nextTick, nextBloom;
    Color leaf, bark, glow;
    bool snapping;
    float snapStart;

    LineRenderer vine, strand;
    readonly List<LineRenderer> coils = new();
    readonly List<Transform> motes = new();
    const int Points = 24;

    static Material vineMat;

    public void Begin(GameObject c, GameObject t, float dur, float drain, float brk, Color theme)
    {
        caster = c; target = t; duration = dur; dps = drain; breakRange = brk;
        casterHealth = c.GetComponent<PlayerHealthControl>();
        started = Time.time;
        leaf = GlowLine.Brighten(theme);
        bark = new Color(0.3f, 0.21f, 0.12f, 1f);
        glow = Color.Lerp(leaf, Color.white, 0.4f);

        if (vineMat == null) vineMat = GlowLine.CreateMaterial(1.2f, additive: false);
        vine = GlowLine.Make(transform, "Vine", Points, 0.2f, vineMat);
        vine.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 0.9f));
        vine.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(bark, 0f), new GradientColorKey(Color.Lerp(bark, leaf, 0.5f), 0.5f), new GradientColorKey(bark, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        };
        strand = GlowLine.Make(transform, "LifeStrand", Points * 2, 0.07f, AbilityKit.Glow());

        // roots coil around the victim
        for (int i = 0; i < 2; i++)
            coils.Add(GlowLine.Make(transform, "Coil", 16, 0.09f, vineMat));
        foreach (var coil in coils) coil.colorGradient = vine.colorGradient;

        // life motes flowing down the vine
        for (int i = 0; i < 7; i++)
        {
            var m = AbilityKit.GlowOrb(glow, 0.2f);
            m.transform.SetParent(transform, true);
            motes.Add(m.transform);
        }

        // the lash
        var wiz = AbilityKit.Wizard(caster);
        PowerFx.Prefab(wiz != null ? wiz.spawnEffectPrefab : null, AbilityKit.Chest(target), 2f);
        PowerFx.Sparks(AbilityKit.Chest(target), leaf, 20, 5f, 0.5f, 0.08f, 0.5f);
        PowerFx.Flash(AbilityKit.Chest(target), leaf, 5f, 4f, 0.3f);
        Rumble.Play(caster, 0.4f, 0.7f, 0.2f);
        Rumble.Play(target, 0.6f, 0.4f, 0.25f);
    }

    void Update()
    {
        float now = Time.time;
        bool gone = caster == null || target == null || !caster.activeInHierarchy || !target.activeInHierarchy
                    || PlayerHealthControl.IsIncapacitated(target.transform);
        if (!snapping && (gone || now - started >= duration
            || Vector3.Distance(caster.transform.position, target.transform.position) > breakRange))
        {
            snapping = true;
            snapStart = now;
            if (!gone)
            {
                PowerFx.Sparks(Mid(), leaf, 16, 4f, 0.5f, 0.07f, 1f);
                PowerFx.Puffs(Mid(), leaf, 6, 1.5f, 0.35f, 0.6f, additive: true);
            }
        }

        if (snapping)
        {
            // the vine recoils back into the caster and fades
            float k = Mathf.Clamp01((now - snapStart) / 0.3f);
            if (caster == null || target == null || k >= 1f) { Destroy(gameObject); return; }
            Draw(1f - k, 1f - k);
            foreach (var m in motes) if (m != null) m.gameObject.SetActive(false);
            return;
        }

        float lash = Mathf.Clamp01((now - started) / lashTime);
        Draw(lash, 1f);
        if (lash < 1f) return;

        // drain → heal
        if (now >= nextTick)
        {
            nextTick = now + 0.25f;
            DamageEvents.Deal(target, dps * 0.25f, caster);
            healPending += dps * 0.25f;
            int whole = Mathf.FloorToInt(healPending);
            if (whole > 0 && casterHealth != null) { casterHealth.Heal(whole); healPending -= whole; }
        }
        if (now >= nextBloom)
        {
            nextBloom = now + 0.6f;
            PowerFx.Sparks(AbilityKit.Chest(caster), leaf, 6, 2f, 0.6f, 0.07f, -0.3f, Vector3.up, 60f);
            PowerFx.Puffs(AbilityKit.Chest(target), bark, 3, 1f, 0.3f, 0.5f);
        }
        PassiveGlow.On(caster).Set(leaf, 0.8f, 8f);
    }

    Vector3 Mid() => (AbilityKit.Chest(caster) + AbilityKit.Chest(target)) * 0.5f;

    // Vine from caster (0) to target (1), grown to `reach`; `alpha` fades it
    void Draw(float reach, float alpha)
    {
        Vector3 a = AbilityKit.Chest(caster), b = AbilityKit.Chest(target);
        float t = Time.time;
        Vector3 along = b - a;
        Vector3 side = Vector3.Cross(along.normalized, Vector3.up);
        Vector3 up = Vector3.Cross(side, along.normalized);

        for (int i = 0; i < Points; i++)
        {
            float f = i / (float)(Points - 1) * reach;
            vine.SetPosition(i, VinePoint(a, along, side, up, f, t));
        }
        var g = vine.colorGradient;
        g.alphaKeys = new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) };
        g.colorKeys = new[] { new GradientColorKey(bark, 0f), new GradientColorKey(Color.Lerp(bark, leaf, 0.55f), 0.5f), new GradientColorKey(bark, 1f) };
        vine.colorGradient = g;

        // glowing life strand twisting around the vine
        int n = strand.positionCount;
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1) * reach;
            float ang = f * 18f - t * 10f;
            Vector3 p = VinePoint(a, along, side, up, f, t) + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * 0.13f;
            strand.SetPosition(i, p);
        }
        GlowLine.SetColor(strand, glow, 0.8f * alpha);

        // coils around the victim once the vine arrives
        for (int c = 0; c < coils.Count; c++)
        {
            var coil = coils[c];
            coil.enabled = reach >= 0.99f;
            if (!coil.enabled) continue;
            Vector3 feet = target.transform.position;
            for (int i = 0; i < coil.positionCount; i++)
            {
                float f = i / (float)(coil.positionCount - 1);
                float ang = c * Mathf.PI + f * Mathf.PI * 4f + t * 2f;
                coil.SetPosition(i, feet + new Vector3(Mathf.Cos(ang) * 0.5f, 0.1f + f * 1.2f, Mathf.Sin(ang) * 0.5f));
            }
        }

        // motes ride from the victim back to the caster
        for (int m = 0; m < motes.Count; m++)
        {
            if (motes[m] == null) continue;
            bool show = reach >= 0.99f && alpha > 0.5f;
            motes[m].gameObject.SetActive(show);
            if (!show) continue;
            float f = 1f - Mathf.Repeat(t * 0.9f + m / (float)motes.Count, 1f);
            motes[m].position = VinePoint(a, along, side, up, f, t);
            motes[m].localScale = Vector3.one * 0.2f * (0.6f + 0.6f * Mathf.Sin(f * Mathf.PI));
        }
    }

    // sagging, writhing curve between the two
    static Vector3 VinePoint(Vector3 a, Vector3 along, Vector3 side, Vector3 up, float f, float t)
    {
        float sag = Mathf.Sin(f * Mathf.PI);
        return a + along * f
               - Vector3.up * (0.45f * sag)
               + side * (Mathf.Sin(f * 9f - t * 6f) * 0.12f * sag)
               + up * (Mathf.Cos(f * 7f - t * 5f) * 0.08f * sag);
    }

    void OnDestroy()
    {
        if (caster != null) PassiveGlow.On(caster).Set(leaf, 0f);
    }
}
