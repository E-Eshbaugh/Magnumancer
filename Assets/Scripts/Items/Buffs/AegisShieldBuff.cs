using UnityEngine;

/// <summary>
/// Aegis Sigil: a shield that soaks up the next chunk of damage for a few seconds. A faint
/// bubble surrounds you (it flares when hit) and the ring at your feet shows the shield
/// left. It shatters when it runs out of points.
/// </summary>
public class AegisShieldBuff : MonoBehaviour, IIncomingDamageModifier
{
    float shield, maxShield, until, duration, flash;
    Color color;
    GameObject bubble;
    Renderer bubbleRenderer;
    BuffRing ring;

    public float Shield => shield;

    public static void Give(GameObject player, float points, float time, Color color)
    {
        var b = player.GetComponent<AegisShieldBuff>();
        if (b == null) b = player.AddComponent<AegisShieldBuff>();
        b.Begin(points, time, color);
    }

    void Begin(float points, float time, Color c)
    {
        color = c;
        maxShield = points;
        shield = points;
        duration = time;
        Sfx.Play(SfxId.ShieldUp, transform.position);
        until = Time.time + time;
        flash = 1f;
        if (bubble == null)
        {
            bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "AegisBubble";
            Destroy(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(transform, false);
            bubbleRenderer = bubble.GetComponent<MeshRenderer>();
            bubbleRenderer.sharedMaterial = AbilityKit.Glow();
            bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (ring == null) ring = new BuffRing(transform, 0.7f, color);
        AbilityKit.Shockwave(transform.position, 2f, color, 0.3f);
    }

    public float ModifyIncoming(float amount, GameObject attacker)
    {
        if (shield <= 0f || amount <= 0f) return amount;
        float absorbed = Mathf.Min(shield, amount);
        shield -= absorbed;
        flash = 1f;
        PowerFx.Sparks(AbilityKit.Chest(gameObject), color, 6, 3f, 0.25f, 0.05f, 0f);
        if (shield <= 0f) Shatter();
        return amount - absorbed;
    }

    void Shatter()
    {
        Vector3 at = AbilityKit.Chest(gameObject);
        Sfx.Play(SfxId.ShieldBreak, at);
        PowerFx.IceShards(at, color, 14, 5f, 0.12f);
        PowerFx.Flash(at, color, 5f, 4f, 0.25f);
        AbilityKit.Shockwave(transform.position, 1.8f, color, 0.3f);
        Rumble.Play(gameObject, 0.4f, 0.6f, 0.2f);
        Destroy(this);
    }

    void Update()
    {
        float left = until - Time.time;
        if (left <= 0f || shield <= 0f) { Destroy(this); return; }

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
        bool ending = left < 1.2f;
        float blink = ending ? 0.5f + 0.5f * Mathf.PingPong(Time.time * 8f, 1f) : 1f;

        bubble.transform.position = AbilityKit.Chest(gameObject) - Vector3.up * 0.2f;
        bubble.transform.localScale = Vector3.one * (2.3f + 0.05f * Mathf.Sin(Time.time * 4f) + 0.2f * flash) / ParentScale();
        ItemVisuals.Tint(bubbleRenderer, color, (0.12f + 0.5f * flash) * blink);
        ring.Draw(transform.position, shield / maxShield, 0.6f * blink);
    }

    float ParentScale()
    {
        var s = transform.lossyScale;
        return Mathf.Max(0.01f, (s.x + s.y + s.z) / 3f);
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) Destroy(this); }   // eliminated: the player switches off

    void OnDestroy()
    {
        if (bubble != null) Destroy(bubble);
        ring?.Destroy();
    }
}
