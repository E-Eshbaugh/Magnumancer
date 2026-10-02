using UnityEngine;

/// <summary>
/// Everything the Great Hall shows: wizards, the armory's weapon tiers, the War Table's
/// maps and modes, plus fonts, icons and the wizard model. One asset, referenced by the
/// HallDirector in GreatHall.unity (GreatHallBuilder fills it from the old book menu).
/// </summary>
[CreateAssetMenu(menuName = "Magnumancer/Hall Catalog")]
public class HallCatalog : ScriptableObject
{
    [Header("Wizards (LB/RB order)")]
    public WizardData[] wizards;

    [Header("Armory")]
    public WeaponData[] initiate;
    public WeaponData[] ascendant;
    public WeaponData[] archon;
    [Tooltip("Fills empty loadout slots (what the game expects for 'no gun')")]
    public WeaponData placeholder;
    public string[] tierNames = { "Initiate", "Ascended", "Archon" };

    [System.Serializable]
    public class MapEntry
    {
        [Tooltip("Scene to load. The index in this list is DataManager.SelectedMap")]
        public string scene;
        public string displayName;
        [TextArea] public string flavor;
        public GameObject miniature;
        [Tooltip("Where it floats over the War Table: x = screen right, y = screen up, in table units")]
        public Vector2 tablePos;
        [Tooltip("Extra float height over the land")]
        public float height = 1f;
        [Tooltip("Glow of its beam and the land beneath it")]
        public Color glow = new Color(0.4f, 0.9f, 1f);
        [Tooltip("A Waves (Zombies) map: only on the table in a Waves mode. Every other map is PvP only.")]
        public bool waves;
    }

    [Header("War Table")]
    public MapEntry[] maps;

    [System.Serializable]
    public class ModeEntry
    {
        public string name;
        [TextArea] public string description;
        [Tooltip("Co-op against the horde: the War Table shows only Waves maps")]
        public bool waves;
    }
    public ModeEntry[] modes;

    [Header("Look")]
    public GameObject wizardModel;
    public RuntimeAnimatorController wizardAnimator;
    public Font titleFont;   // Bone: capitals only
    public Font bodyFont;    // Help
    public Sprite heart, orb;
    public Sprite btnA, btnB, btnX, btnY, btnLB, btnRB, btnDpad, btnStick, btnMenu;
    public Color[] playerColors =
    {
        new Color(0.95f, 0.26f, 0.26f),
        new Color(0.26f, 0.55f, 1f),
        new Color(0.35f, 0.9f, 0.35f),
        new Color(1f, 0.82f, 0.2f),
    };

    public WeaponData[] Tier(int t) => t switch { 1 => ascendant, 2 => archon, _ => initiate };
    public int TierCount => 3;
    public Color PlayerColor(int i) => playerColors != null && playerColors.Length > 0 ? playerColors[Mathf.Abs(i) % playerColors.Length] : Color.white;
}
