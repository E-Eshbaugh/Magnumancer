using UnityEngine;

[CreateAssetMenu(menuName = "Mangumancer/Wizard")]
public class WizardData : ScriptableObject
{
    public string wizardName;
    public int heartCount;
    public int orbCount;
    public int loadoutOrbs;
    public Sprite factionEmblem;
    public Sprite charcterImage;
    public string passiveAbilityTxt;
    public string activeAbilityTxt;
    public string loreText;
    public Material material;
    public GameObject activeAbilityPrefab;
    public float abilityCooldown;
    public GameObject customBulletPrefab;
    [Tooltip("Base move speed multiplier (Granite Vow is slow, Voltborn is quick)")]
    public float moveSpeedMultiplier = 1f;

    [Header("Theme")]
    [Tooltip("Signature color: laser sights, spawn bolt/flash")]
    public Color themeColor = Color.white;
    [Tooltip("Optional particle burst played where the wizard (re)spawns, on top of the bolt")]
    public GameObject spawnEffectPrefab;

    [Header("Passive")]
    public PassiveType passive;
    [Tooltip("VFX/prefab the passive spawns (poison cloud, fire burst, ice shield, bolt impact)")]
    public GameObject passiveEffectPrefab;
}

public enum PassiveType
{
    None = 0,
    VirulentShroud = 1,   // Blightward
    BrandOfFlereous = 2,  // Emberguard
    FractalshotShield = 3,// Frostwarden
    Stonebind = 4,        // Granite Vow
    LastRites = 5,        // The Hollow
    Undercurrent = 6,     // Tidebound
    VerdantResurgence = 7,// Verdant Circle
    LightningReflex = 8   // Voltborn
}
