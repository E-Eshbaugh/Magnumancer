using System.Collections;
using UnityEngine;

/// <summary>
/// A drop's warning, then its arrival: a beam of light from the sky marks the spot for a
/// couple of seconds (white, or gold for a rare drop) while a ring closes in on it; then a
/// bolt strikes like a wizard's spawn and the item appears.
/// </summary>
public class DropBeam : MonoBehaviour
{
    public float height = 18f;
    public float startRing = 2.6f;

    ItemBook.Def def;
    Vector3 ground;
    float telegraph, lifetime;
    Color color;

    public static DropBeam Spawn(ItemBook.Def def, Vector3 groundPoint, float telegraph, float pickupLifetime)
    {
        var go = new GameObject($"DropBeam ({def.name})");
        go.transform.position = groundPoint;
        var b = go.AddComponent<DropBeam>();
        b.def = def; b.ground = groundPoint; b.telegraph = telegraph; b.lifetime = pickupLifetime;
        b.color = ItemBook.RarityColor(def.rarity);
        b.StartCoroutine(b.Run());
        return b;
    }

    /// Where a beam is still on its way down (so two drops don't land on one spot)
    public Vector3 Ground => ground;

    IEnumerator Run()
    {
        bool rare = def.rarity >= Rarity.Rare;
        var mat = AbilityKit.Glow();
        var beam = GlowLine.Make(transform, "Beam", 2, 0.1f, mat);
        var core = GlowLine.Make(transform, "BeamCore", 2, 0.04f, mat);
        var ring = ItemVisuals.Ring(transform, startRing, color, 0.09f);
        var light = new GameObject("BeamLight").AddComponent<Light>();
        light.transform.SetParent(transform, false);
        light.transform.localPosition = Vector3.up * 1.5f;
        light.type = LightType.Point; light.color = color; light.range = 5f; light.shadows = LightShadows.None;

        Vector3 top = ground + Vector3.up * height;
        beam.SetPosition(0, top); beam.SetPosition(1, ground);
        core.SetPosition(0, top); core.SetPosition(1, ground);
        ItemAudio.Incoming(rare);
        if (rare) ReactionPopup.Show("RARE DROP!", ItemBook.Gold, Color.white, ground + Vector3.up * 3f, 0.9f);

        float t = 0f;
        while (t < telegraph)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / telegraph);
            float flicker = 0.75f + 0.25f * Mathf.Sin(t * (10f + 30f * k));
            beam.widthMultiplier = Mathf.Lerp(0.6f, rare ? 4f : 3f, k * k);
            core.widthMultiplier = beam.widthMultiplier;
            GlowLine.SetColor(beam, color, Mathf.Lerp(0.25f, 0.8f, k) * flicker);
            GlowLine.SetColor(core, Color.white, Mathf.Lerp(0.3f, 1f, k) * flicker);
            AbilityKit.Circle(ring, ground + Vector3.up * 0.07f, Mathf.Lerp(startRing, 0.75f, k));
            GlowLine.SetColor(ring, color, Mathf.Lerp(0.3f, 1f, k));
            light.intensity = Mathf.Lerp(0.5f, rare ? 6f : 4f, k) * flicker;
            yield return null;
        }

        // the strike
        Destroy(beam.gameObject);
        Destroy(core.gameObject);
        Destroy(ring.gameObject);
        Destroy(light.gameObject);
        Strike(rare);
        Pickup.Spawn(def, ground, lifetime);

        yield return new WaitForSeconds(0.25f);
        Destroy(gameObject);
    }

    void Strike(bool rare)
    {
        var bolt = new GameObject("DropBolt");
        var lr = GlowLine.Make(bolt.transform, "Bolt", 14, rare ? 0.45f : 0.3f, AbilityKit.Glow());
        GlowLine.Bolt(lr, ground + Vector3.up * height, ground, 0.6f);
        GlowLine.SetColor(lr, Color.Lerp(color, Color.white, 0.4f), 1f);
        Destroy(bolt, 0.18f);

        AbilityKit.Shockwave(ground, rare ? 3.2f : 2.4f, color, 0.45f);
        AbilityKit.Shockwave(ground, 1.4f, def.color, 0.3f);
        PowerFx.Flash(ground + Vector3.up, color, rare ? 12f : 8f, 8f, 0.4f);
        PowerFx.Sparks(ground + Vector3.up * 0.3f, def.color, rare ? 40 : 24, 7f, 0.6f, 0.08f, 1f, Vector3.up, 140f);
        CameraShake.Shake(rare ? 0.18f : 0.1f, 0.15f);
        ItemAudio.Land(rare);
    }
}
