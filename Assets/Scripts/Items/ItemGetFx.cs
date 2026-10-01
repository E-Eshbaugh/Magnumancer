using System.Collections;
using UnityEngine;

/// <summary>
/// The "item get!" moment: the item leaps off the ground to hang over the player's head,
/// spinning, with rays of light wheeling around it, then dives into them and bursts
/// (rings, sparks, flash, a kick of shake and rumble). Rare items get gold rays and a
/// bigger blast. The name slams in above them (ItemGetText).
/// </summary>
public class ItemGetFx : MonoBehaviour
{
    const float RiseTime = 0.22f, HoldTime = 0.38f, DiveTime = 0.12f;
    const float OverHead = 2.9f;
    const int RayCount = 12;

    ItemBook.Def def;
    GameObject player;
    Color color, accent;
    float size;
    bool rare;
    Vector3 lastAbove;
    LineRenderer[] rays;
    Light glow;

    public static void Play(ItemBook.Def def, string word, GameObject player, Vector3 from, float modelScale)
    {
        bool rare = def.rarity >= Rarity.Rare;
        var go = new GameObject($"ItemGet ({def.name})");
        go.transform.position = from;
        var fx = go.AddComponent<ItemGetFx>();
        fx.def = def;
        fx.player = player;
        fx.color = def.color;
        fx.accent = rare ? ItemBook.Gold : Color.white;
        fx.size = modelScale;
        fx.rare = rare;
        fx.StartCoroutine(fx.Run(from));
        ItemGetText.Show(word, def.color, fx.accent, player, rare ? 1.15f : 1f);
    }

    Vector3 Above()
    {
        if (player != null && player.activeInHierarchy)
            lastAbove = player.transform.position + Vector3.up * OverHead;
        return lastAbove;
    }

    IEnumerator Run(Vector3 from)
    {
        lastAbove = from + Vector3.up * 1.5f;
        var spin = new GameObject("Spin").transform;
        spin.SetParent(transform, false);
        ItemVisuals.Model(def.id, spin, solid: true);
        transform.localScale = Vector3.one * size;

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = color; glow.range = 6f; glow.intensity = 4f; glow.shadows = LightShadows.None;

        PowerFx.Flash(from, color, 6f, 5f, 0.25f);
        PowerFx.Sparks(from, color, 14, 5f, 0.35f, 0.08f, 0.6f);
        AbilityKit.Shockwave(AbilityKit.Ground(from), 1.6f, color, 0.3f);

        // 1) leap up over their head, spinning hard
        float t = 0f, spinAngle = 0f;
        while (t < RiseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / RiseTime);
            float e = 1f - (1f - k) * (1f - k);
            transform.position = Vector3.Lerp(from, Above(), e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
            transform.localScale = Vector3.one * size * Mathf.Lerp(1f, 1.45f, e);
            spinAngle += Time.deltaTime * Mathf.Lerp(1400f, 600f, k);
            spin.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
            yield return null;
        }

        // 2) hang there, rays wheeling around it
        BuildRays();
        t = 0f;
        while (t < HoldTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / HoldTime);
            transform.position = Above() + Vector3.up * Mathf.Sin(t * 9f) * 0.06f;
            transform.localScale = Vector3.one * size * (1.45f + 0.12f * Mathf.Sin(t * 22f));
            spinAngle += Time.deltaTime * 420f;
            spin.localRotation = Quaternion.Euler(0f, spinAngle, Mathf.Sin(t * 14f) * 10f);
            DrawRays(t, Mathf.Clamp01(t / 0.08f), 1f);
            glow.intensity = 4f + 3f * Mathf.Sin(t * 20f);
            yield return null;
        }

        // 3) dive into the player
        Vector3 top = transform.position;
        t = 0f;
        while (t < DiveTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DiveTime);
            Vector3 chest = player != null && player.activeInHierarchy ? AbilityKit.Chest(player) : lastAbove - Vector3.up * 1.8f;
            transform.position = Vector3.Lerp(top, chest, k * k);
            transform.localScale = Vector3.one * size * Mathf.Lerp(1.45f, 0.2f, k);
            DrawRays(HoldTime + t, 1f - k, 1f + k);
            yield return null;
        }

        Burst(transform.position);
        Destroy(gameObject);
    }

    void BuildRays()
    {
        rays = new LineRenderer[RayCount];
        for (int i = 0; i < RayCount; i++)
        {
            var lr = GlowLine.Make(transform, "Ray", 2, 0.26f, AbilityKit.Glow());
            lr.endWidth = 0f;   // taper to a point
            rays[i] = lr;
        }
    }

    // Rays fanned around the item in the camera's plane, wheeling, alternating colors
    void DrawRays(float t, float alpha, float stretch)
    {
        if (rays == null) return;
        var cam = Camera.main;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        Vector3 c = transform.position;
        for (int i = 0; i < rays.Length; i++)
        {
            float a = i / (float)rays.Length * Mathf.PI * 2f + t * 2.2f;
            bool longRay = i % 2 == 0;
            float len = (longRay ? 2.6f : 1.6f) * (0.85f + 0.15f * Mathf.Sin(t * 18f + i)) * stretch * (rare ? 1.25f : 1f);
            Vector3 d = right * Mathf.Cos(a) + up * Mathf.Sin(a);
            rays[i].SetPosition(0, c + d * 0.35f);
            rays[i].SetPosition(1, c + d * len);
            GlowLine.SetColor(rays[i], longRay ? accent : color, alpha * (longRay ? 0.9f : 0.7f));
        }
    }

    void Burst(Vector3 at)
    {
        Vector3 feet = player != null && player.activeInHierarchy ? player.transform.position : AbilityKit.Ground(at);
        AbilityKit.Shockwave(feet, rare ? 3.6f : 2.6f, color, 0.4f);
        AbilityKit.Shockwave(feet, rare ? 2.2f : 1.5f, accent, 0.3f);
        PowerFx.Flash(at, color, rare ? 12f : 8f, 7f, 0.35f);
        PowerFx.Sparks(at, color, rare ? 60 : 36, 9f, 0.55f, 0.09f, 0.6f);
        PowerFx.Sparks(at, accent, rare ? 30 : 16, 7f, 0.7f, 0.07f, 1.2f, Vector3.up, 70f);
        CameraShake.Shake(rare ? 0.2f : 0.1f, rare ? 0.25f : 0.15f);
        if (player != null) Rumble.Play(player, rare ? 0.8f : 0.45f, 1f, rare ? 0.4f : 0.25f);
        if (rare && DropDirector.Instance != null) DropDirector.Instance.StartCoroutine(Aftershock(feet, color));
    }

    // a second gold ring a beat later for rare items
    static IEnumerator Aftershock(Vector3 feet, Color c)
    {
        yield return new WaitForSeconds(0.12f);
        AbilityKit.Shockwave(feet, 4.5f, ItemBook.Gold, 0.5f);
        PowerFx.Sparks(feet + Vector3.up * 0.3f, ItemBook.Gold, 30, 8f, 0.6f, 0.08f, 1f, Vector3.up, 160f);
    }
}
