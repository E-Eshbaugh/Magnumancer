using System;
using UnityEngine;

/// <summary>
/// Central combat hooks. Health components report hits and kills here (with the attacker
/// when known), so wizard passives can react without every weapon knowing about them.
/// </summary>
public static class DamageEvents
{
    /// victim, attacker (may be null), damage actually dealt (0 if fully absorbed)
    public static event Action<GameObject, GameObject, float> Damaged;

    /// victim, attacker (may be null), position — a monster died or a player lost a life
    public static event Action<GameObject, GameObject, Vector3> Killed;

    public static void RaiseDamaged(GameObject victim, GameObject attacker, float amount)
        => Damaged?.Invoke(victim, attacker, amount);

    public static void RaiseKilled(GameObject victim, GameObject attacker, Vector3 position)
        => Killed?.Invoke(victim, attacker, position);

    /// Applies the attacker's outgoing modifiers (e.g. Last Rites double damage).
    public static float ModifyOutgoing(GameObject attacker, float amount)
    {
        if (attacker == null) return amount;
        foreach (var mod in attacker.GetComponents<IOutgoingDamageModifier>())
            amount = mod.ModifyOutgoing(amount);
        return amount;
    }

    /// Damages whatever can take damage on target (player or monster).
    /// Returns true if target was damageable.
    public static bool Deal(GameObject target, float amount, GameObject attacker)
    {
        if (target == null) return false;
        var player = target.GetComponentInParent<PlayerHealthControl>();
        if (player != null) { player.TakeDamage(amount, attacker); return true; }
        var monster = target.GetComponentInParent<GoblinHealth>();
        if (monster != null) { monster.TakeDamage(amount, attacker); return true; }
        return false;
    }

    /// The object that "is" a combatant for this collider (players/monsters have a rigidbody root).
    public static GameObject RootOf(Collider col)
        => col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;

    /// True for another player or a monster.
    public static bool IsCombatant(GameObject go)
        => go != null && (go.CompareTag("Monster") || go.GetComponent<PlayerHealthControl>() != null);
}

/// Scales damage this object deals.
public interface IOutgoingDamageModifier
{
    float ModifyOutgoing(float amount);
}

/// Scales/absorbs damage this object takes. Return the damage that gets through.
public interface IIncomingDamageModifier
{
    float ModifyIncoming(float amount, GameObject attacker);
}
