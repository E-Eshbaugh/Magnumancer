namespace Magnumancer.Abilities
{
    public interface IActiveAbility
    {
        void Activate(UnityEngine.GameObject caster);
    }

    /// Optional: an ability that can fizzle (no target) reports it so no cooldown is spent
    public interface IAbilityOutcome
    {
        bool Fizzled { get; }
    }
}
