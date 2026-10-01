using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// An item on the ground: its glowing model bobs and spins over a dark shadow and a ring
/// in its color (gold rings for rare drops), lit by a small light, with its name floating
/// above. Walk over it to take it. Pickups that sit too long blink, then fade away.
/// </summary>
public class Pickup : MonoBehaviour
{
    public float pickupRadius = 1.2f;
    public float bobHeight = 0.15f;
    public float spinSpeed = 90f;
    [Tooltip("Blinks for this long before it disappears")]
    public float warnTime = 5f;

    public ItemBook.Def Def { get; private set; }
    public float Born => born;

    float born, lifetime;
    Transform model;
    LineRenderer ring, outerRing;
    Light glow;
    TextMeshPro label;
    Vector3 ground;
    bool taken;

    static readonly List<Pickup> live = new();
    public static IReadOnlyList<Pickup> Live => live;

    public static Pickup Spawn(ItemBook.Def def, Vector3 groundPoint, float lifetime)
    {
        var go = new GameObject($"Pickup ({def.name})");
        go.transform.position = groundPoint;
        var p = go.AddComponent<Pickup>();
        p.Init(def, groundPoint, lifetime);
        return p;
    }

    void Init(ItemBook.Def def, Vector3 at, float life)
    {
        Def = def;
        ground = at;
        born = Time.time;
        lifetime = life;
        bool rare = def.rarity >= Rarity.Rare;

        ItemVisuals.Shadow(transform, rare ? 1.1f : 0.85f).transform.localPosition = Vector3.up * 0.03f;

        var holder = new GameObject("Spin").transform;
        holder.SetParent(transform, false);
        holder.localPosition = Vector3.up * 0.95f;
        model = ItemVisuals.Model(def.id, holder);
        if (rare) holder.localScale = Vector3.one * 1.35f;

        ring = ItemVisuals.Ring(transform, 0.75f, def.color, 0.07f);
        if (rare || def.rarity == Rarity.VeryRare)
            outerRing = ItemVisuals.Ring(transform, 1.05f, ItemBook.Gold, 0.05f);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 1f;
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = def.color;
        glow.range = rare ? 4.5f : 3f;
        glow.intensity = rare ? 3.5f : 2.2f;
        glow.shadows = LightShadows.None;

        label = ItemVisuals.Label(transform, def.name.ToUpperInvariant(), rare ? ItemBook.Gold : def.color, rare ? 5.5f : 4.5f);
        if (label != null) label.transform.localPosition = Vector3.up * 2f;

        live.Add(this);
    }

    void OnDestroy() => live.Remove(this);

    void Update()
    {
        if (taken) return;
        float age = Time.time - born;
        float left = lifetime - age;
        if (left <= 0f) { Destroy(gameObject); return; }

        // pops in, then bobs and spins
        float popIn = Mathf.Clamp01(age / 0.25f);
        float scale = popIn < 1f ? Mathf.Lerp(0.2f, 1.15f, popIn) : 1f;
        model.parent.localScale = Vector3.one * scale * (Def.rarity >= Rarity.Rare ? 1.35f : 1f);
        model.parent.localPosition = Vector3.up * (0.95f + Mathf.Sin(age * 2.6f) * bobHeight);
        model.parent.localRotation = Quaternion.Euler(0f, age * spinSpeed, 0f);

        // about to vanish: blink faster and faster
        bool visible = left > warnTime || Mathf.Repeat(age * Mathf.Lerp(8f, 3f, left / warnTime), 1f) > 0.3f;
        model.gameObject.SetActive(visible);

        float pulse = 0.75f + 0.25f * Mathf.Sin(age * 5f);
        AbilityKit.Circle(ring, ground + Vector3.up * 0.06f, 0.75f + 0.05f * Mathf.Sin(age * 3f));
        GlowLine.SetColor(ring, Def.color, (visible ? 0.9f : 0.3f) * pulse);
        if (outerRing != null)
        {
            AbilityKit.Circle(outerRing, ground + Vector3.up * 0.06f, 1.05f + 0.08f * Mathf.Sin(age * 2f + 1f));
            GlowLine.SetColor(outerRing, ItemBook.Gold, (visible ? 0.8f : 0.25f) * pulse);
        }
        glow.intensity = (Def.rarity >= Rarity.Rare ? 3.5f : 2.2f) * pulse * (visible ? 1f : 0.3f);

        if (label != null)
        {
            label.transform.rotation = ItemVisuals.Billboard(label.transform.rotation);
            label.alpha = visible ? 1f : 0.4f;
        }

        if (age > 0.2f) TryCollect();
    }

    void TryCollect()
    {
        foreach (var player in DropDirector.LivingPlayers())
        {
            Vector3 d = player.transform.position - ground;
            if (Mathf.Abs(d.y) > 2f) continue;
            d.y = 0f;
            if (d.sqrMagnitude > pickupRadius * pickupRadius) continue;
            Collect(player.gameObject);
            return;
        }
    }

    void Collect(GameObject player)
    {
        taken = true;
        string word = ItemEffects.Apply(Def, player) ?? Def.name.ToUpperInvariant() + "!";

        Vector3 at = ground + Vector3.up;
        AbilityKit.Shockwave(ground, 1.6f, Def.color, 0.35f);
        PowerFx.Sparks(at, Def.color, Def.rarity >= Rarity.Rare ? 40 : 20, 6f, 0.5f, 0.07f, 0.5f);
        PowerFx.Flash(at, Def.color, 5f, 5f, 0.3f);
        ReactionPopup.Show(word, Def.color, Def.rarity >= Rarity.Rare ? ItemBook.Gold : Color.white,
                           AbilityKit.Chest(player) + Vector3.up * 1.8f, Def.rarity >= Rarity.Rare ? 0.9f : 0.65f);
        Rumble.Play(player, 0.3f, 0.7f, 0.2f);
        ItemAudio.Pickup(Def.rarity);
        Destroy(gameObject);
    }

    /// Clears every pickup (match reset)
    public static void ClearAll()
    {
        for (int i = live.Count - 1; i >= 0; i--)
            if (live[i] != null) Destroy(live[i].gameObject);
        live.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => live.Clear();
}
