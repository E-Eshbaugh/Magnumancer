using UnityEngine;

/// <summary>
/// Base for wizard passives. MultiplayerManager adds the right one to each player
/// based on WizardData.passive (see AddTo).
/// </summary>
public abstract class WizardPassive : MonoBehaviour
{
    protected WizardData wizard;
    protected PlayerHealthControl health;
    protected PlayerMovement3D movement;

    public virtual void Init(WizardData data)
    {
        wizard = data;
        health = GetComponent<PlayerHealthControl>();
        movement = GetComponent<PlayerMovement3D>();
    }

    /// Another player or a monster (never ourselves).
    protected bool IsEnemy(GameObject other)
        => other != null && other != gameObject && DamageEvents.IsCombatant(other);

    protected bool IsAlive => health != null && !health.IsDead;

    PassiveGlow glow;
    /// Subtle glow around the player for buff indicators
    protected PassiveGlow Glow => glow ? glow : (glow = PassiveGlow.On(gameObject));

    /// Spawns the wizard's passiveEffectPrefab (if any). lifetime <= 0 leaves cleanup to the prefab.
    protected GameObject SpawnEffect(Vector3 position, float lifetime)
    {
        if (wizard == null || wizard.passiveEffectPrefab == null) return null;
        var fx = Instantiate(wizard.passiveEffectPrefab, position, Quaternion.identity);
        if (lifetime > 0f) Destroy(fx, lifetime);
        return fx;
    }

    public static WizardPassive AddTo(GameObject player, WizardData data, int passiveRune = 0)
    {
        if (player == null || data == null) return null;

        // Rune II swaps in the alternative passive
        var rune = RuneBook.Passive(data, passiveRune);
        if (passiveRune > 0 && rune != null && rune.passiveType != null)
        {
            var alt = player.AddComponent(rune.passiveType) as WizardPassive;
            if (alt != null) alt.Init(data);
            return alt;
        }

        WizardPassive passive = data.passive switch
        {
            PassiveType.VirulentShroud    => player.AddComponent<VirulentShroudPassive>(),
            PassiveType.BrandOfFlereous   => player.AddComponent<BrandOfFlereousPassive>(),
            PassiveType.FractalshotShield => player.AddComponent<FractalshotShieldPassive>(),
            PassiveType.Stonebind         => player.AddComponent<StonebindPassive>(),
            PassiveType.LastRites         => player.AddComponent<LastRitesPassive>(),
            PassiveType.Undercurrent      => player.AddComponent<UndercurrentPassive>(),
            PassiveType.VerdantResurgence => player.AddComponent<VerdantResurgencePassive>(),
            PassiveType.LightningReflex   => player.AddComponent<LightningReflexPassive>(),
            _ => null
        };

        if (passive != null) passive.Init(data);
        return passive;
    }
}
