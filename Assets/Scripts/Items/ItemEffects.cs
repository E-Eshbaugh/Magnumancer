using UnityEngine;

/// <summary>
/// What each item does to whoever picks it up. Returns the word to pop over them (null =
/// the item's name).
/// </summary>
public static class ItemEffects
{
    public static float HealAmount = 40f;
    public static float OverdriveTime = 8f;
    public static float OverdriveFireRate = 1.5f;
    public static int BlinkCharges = 3;
    public static float AegisShield = 50f;
    public static float AegisTime = 6f;

    public static string Apply(ItemBook.Def def, GameObject player)
    {
        if (player == null) return null;
        switch (def.id)
        {
            case ItemId.RuneShard:
            {
                var ability = player.GetComponentInChildren<WizardAbilityController>();
                if (ability != null && ability.Cooldown != null) ability.Cooldown.Refresh();
                return null;
            }
            case ItemId.HealingDraught:
            {
                var health = player.GetComponent<PlayerHealthControl>();
                if (health != null) health.Heal(Mathf.RoundToInt(HealAmount));
                PowerFx.Sparks(AbilityKit.Chest(player), def.color, 18, 3f, 0.7f, 0.08f, -0.6f);
                return $"+{Mathf.RoundToInt(HealAmount)}";
            }
            case ItemId.OverdriveOrb:
                OverdriveBuff.Give(player, OverdriveTime, OverdriveFireRate, def.color);
                return "OVERDRIVE!";
            case ItemId.ElementalRounds:
            {
                var element = ElementalRounds.Give(player);
                return element != Element.None ? $"{element.ToString().ToUpperInvariant()} ROUNDS!" : null;
            }
            case ItemId.BlinkCharm:
                BlinkCharm.Give(player, BlinkCharges, def.color);
                return null;
            case ItemId.AegisSigil:
                AegisShieldBuff.Give(player, AegisShield, AegisTime, def.color);
                return null;
            case ItemId.HexGrenade:
            case ItemId.StickyBomb:
            case ItemId.PortalStone:
                ThrowableSlot.Give(player, def);
                return null;
            case ItemId.SingularityLauncher: WonderWeapon.Give<SingularityLauncher>(player, def); return null;
            case ItemId.FrostCannon: WonderWeapon.Give<FrostCannon>(player, def); return null;
            case ItemId.ThunderMaul: WonderWeapon.Give<ThunderMaul>(player, def); return null;
            case ItemId.GaleHorn: WonderWeapon.Give<GaleHorn>(player, def); return null;
            case ItemId.EmberMinigun: WonderWeapon.Give<EmberMinigun>(player, def); return null;
            case ItemId.HeartRelic:
            {
                var health = player.GetComponent<PlayerHealthControl>();
                if (health != null) health.AddLife();
                CameraShake.Shake(0.15f, 0.2f);
                return "+1 LIFE!";
            }
        }
        return null;
    }
}
