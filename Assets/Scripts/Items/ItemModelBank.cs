using UnityEngine;

/// Art models the item looks are built from (ItemVisuals). The one in use is
/// Resources/ItemModels.asset; swap a model there and the item picks it up. Any slot left
/// empty falls back to a simple shape.
[CreateAssetMenu(menuName = "Magnumancer/Item Model Bank", fileName = "ItemModels")]
public class ItemModelBank : ScriptableObject
{
    [Header("Gems and crystals")]
    public GameObject crystal;        // Rune Shard
    public GameObject dipyramid;      // Blink Charm
    public GameObject hexGem;         // Portal Stone
    public GameObject radiant;        // frost spikes on the Frost Cannon

    [Header("Props")]
    public GameObject bottle;         // Healing Draught
    public GameObject ammoBox;        // Overdrive Orb
    public GameObject shield;         // Aegis Sigil
    public GameObject keg;            // Thunder Maul head
    public GameObject staff;          // Thunder Maul handle

    [Header("Munitions")]
    public GameObject bullet;         // Elemental Rounds
    public GameObject bulletSniper;
    public GameObject bulletShotgun;
    public GameObject grenade;        // Hex Grenade
    public GameObject bomb;           // Goblin Bomb

    [Header("Guns")]
    public GameObject rocketLauncher; // Singularity Launcher
    public GameObject blaster;        // Frost Cannon
    public GameObject machineGun;     // Ember Minigun

    static ItemModelBank loaded;
    static bool tried;

    public static ItemModelBank Get()
    {
        if (!tried)
        {
            tried = true;
            loaded = Resources.Load<ItemModelBank>("ItemModels");
        }
        return loaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = null; tried = false; }
}
