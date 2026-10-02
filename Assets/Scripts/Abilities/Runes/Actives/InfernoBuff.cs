using UnityEngine;

/// Temporary outgoing damage boost with a hot glow (Inferno Rounds)
public class InfernoBuff : MonoBehaviour, IOutgoingDamageModifier
{
    float until, mult = 1f;
    Color color;
    int forgeHits;
    float forgeExtended;

    public bool Active => Time.time < until;

    public void Begin(float duration, float multiplier, Color c)
    {
        until = Time.time + duration;
        mult = multiplier;
        color = c;
        forgeHits = 0;
        forgeExtended = 0f;
    }

    /// Furnace (forged gun): every `perHits` payloads add `seconds`, up to `cap` per cast.
    /// Returns true when this hit extended it.
    public bool ForgeHit(int perHits, float seconds, float cap)
    {
        if (!Active || forgeExtended >= cap || ++forgeHits % Mathf.Max(1, perHits) != 0) return false;
        float add = Mathf.Min(seconds, cap - forgeExtended);
        forgeExtended += add;
        until += add;
        return true;
    }

    public float ModifyOutgoing(float amount) => Active ? amount * mult : amount;

    void Update()
    {
        if (Active) PassiveGlow.On(gameObject).Set(color, 1f, 14f);
        else if (until > 0f) { PassiveGlow.On(gameObject).Set(color, 0f); until = 0f; }
    }
}
