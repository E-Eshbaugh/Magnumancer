using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item on the ground: a big, solid-colored model (big enough to keep its shape through
/// the pixel filter) bobs and spins over a dark shadow and a ring in its color (gold rings
/// for rare drops), lit by a small light. Walk over it to take it (ItemGetFx plays the
/// moment). Pickups that sit too long blink, then fade away.
/// </summary>
public class Pickup : MonoBehaviour
{
    public float pickupRadius = 1.5f;
    public float bobHeight = 0.2f;
    [Tooltip("Model size on the ground (rare drops are bigger still)")]
    public float modelScale = 2.2f;
    public float rareModelScale = 2.6f;
    public float hoverHeight = 1.35f;
    public float spinSpeed = 90f;
    [Tooltip("Blinks for this long before it disappears")]
    public float warnTime = 5f;

    public ItemBook.Def Def { get; private set; }
    public float Born => born;
    /// Testing showcase item: never expires, ignores the field cap, respawns when taken
    public bool Showcase { get; set; }

    float born, lifetime;
    Transform model;
    LineRenderer ring, outerRing;
    Light glow;
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

        ItemVisuals.Shadow(transform, rare ? 1.6f : 1.3f).transform.localPosition = Vector3.up * 0.03f;

        var holder = new GameObject("Spin").transform;
        holder.SetParent(transform, false);
        holder.localPosition = Vector3.up * hoverHeight;
        model = ItemVisuals.Model(def.id, holder, solid: true);
        holder.localScale = Vector3.one * Size;

        ring = ItemVisuals.Ring(transform, 1.15f, def.color, 0.14f);
        if (rare)
            outerRing = ItemVisuals.Ring(transform, 1.5f, ItemBook.Gold, 0.11f);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * hoverHeight;
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = def.color;
        glow.range = rare ? 6f : 4.5f;
        glow.intensity = rare ? 3.5f : 2.5f;
        glow.shadows = LightShadows.None;

        live.Add(this);
    }

    float Size => Def.rarity >= Rarity.Rare ? rareModelScale : modelScale;

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
        model.parent.localScale = Vector3.one * scale * Size;
        model.parent.localPosition = Vector3.up * (hoverHeight + Mathf.Sin(age * 2.6f) * bobHeight);
        model.parent.localRotation = Quaternion.Euler(0f, age * spinSpeed, 0f);

        // about to vanish: blink faster and faster
        bool visible = left > warnTime || Mathf.Repeat(age * Mathf.Lerp(8f, 3f, left / warnTime), 1f) > 0.3f;
        model.gameObject.SetActive(visible);

        float pulse = 0.75f + 0.25f * Mathf.Sin(age * 5f);
        AbilityKit.Circle(ring, ground + Vector3.up * 0.06f, 1.15f + 0.07f * Mathf.Sin(age * 3f));
        GlowLine.SetColor(ring, Def.color, (visible ? 0.9f : 0.3f) * pulse);
        if (outerRing != null)
        {
            AbilityKit.Circle(outerRing, ground + Vector3.up * 0.06f, 1.5f + 0.1f * Mathf.Sin(age * 2f + 1f));
            GlowLine.SetColor(outerRing, ItemBook.Gold, (visible ? 0.8f : 0.25f) * pulse);
        }
        glow.intensity = (Def.rarity >= Rarity.Rare ? 3.5f : 2.5f) * pulse * (visible ? 1f : 0.3f);

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

        ItemGetFx.Play(Def, word, player, model.position, model.parent.lossyScale.x);
        ItemAudio.Pickup(Def.rarity);
        if (Showcase && DropDirector.Instance != null) DropDirector.Instance.RespawnShowcase(Def, ground);
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
