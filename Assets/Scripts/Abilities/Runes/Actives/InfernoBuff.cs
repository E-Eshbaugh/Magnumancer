using UnityEngine;

/// Temporary outgoing damage boost with a hot glow (Inferno Rounds)
public class InfernoBuff : MonoBehaviour, IOutgoingDamageModifier
{
    float until, mult = 1f;
    Color color;

    public bool Active => Time.time < until;

    public void Begin(float duration, float multiplier, Color c)
    {
        until = Time.time + duration;
        mult = multiplier;
        color = c;
    }

    public float ModifyOutgoing(float amount) => Active ? amount * mult : amount;

    void Update()
    {
        if (Active) PassiveGlow.On(gameObject).Set(color, 1f, 14f);
        else if (until > 0f) { PassiveGlow.On(gameObject).Set(color, 0f); until = 0f; }
    }
}
