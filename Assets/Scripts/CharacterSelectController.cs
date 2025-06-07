using System;
using Unity.Multiplayer.Center.Common;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CharacterSelectController : MonoBehaviour
{
    // order - Emberguard, Frostwarden, Voltborn, Tidebound, Verdant Circle, Blightward, Granite Vow, The Hollow
    public Sprite[] wizardImages;
    public Sprite[] iconImages;
    public GameObject[] hearts;
    public GameObject[] orbs;
    public String[] WizardNames = {
        "Emberguard",
        "Frostwarden",
        "Voltborn",
        "Tidebound",
        "Verdant Circle",
        "Blightward",
        "Granite Vow",
        "The Hollow"
    };

    public string[] wizardLore = {
        "Once revered for forging metal and inspiring awe through flame, the Emberguard have turned their craft into a weapon of war. Dwelling in volcanic forges and ash-choked wastes, they’ve traded elegance for brutal efficiency. Their gun spells roar like dragon breath, best suited for ARs and SMGs that mirror their ferocity.",
        "Descendants of the ancient cryomancers, the Frostwardens dwell in glacial sanctuaries charged with ancient magic. Their frozen lands shaped them into disciplined marksmen who wield precision sniper spells to halt foes in their tracks. Strategic and icy in demeanor, they dominate from a distance.",
        "Living among storm-wracked peaks, Voltborn are speed and chaos incarnate. Balanced in both stats, they prefer SMGs and high rate of fire, mobility-enhancing weapons and gadgets to strike chaos across the battlefield. Volborn’s edge is unpredictability—they attack hard and fast then vanishing like lightning, their electrified combat style keeps enemies guessing.",
        "Once peaceful stewards of healing springs and coastal sanctuaries, the Tidebound have evolved into relentless close-combatants hidden deep in flooded ruins and sea caverns. hardened by amphibious goblin raids during the great war, they master the tides with close-range whirlpool attacks and brutal melee spells. They crash in like waves—overwhelming and personal.",
        "Guardians of the Old Woods, the secretive Verdant Circle blend ancient healing with cunning survival tactics. Patient and persistent, they lay traps and wield massive weapons to protect the last living Lost Groves. Their strength lies in outlasting any foe.",
        "Magicians of medicine during the Great Goblin War, the Viridant Order of medical mages healed their wizard brothers on field. After being corrupted by corruptive spores released by goblin shamans, the powerful healers were warped into the Blightward, using their strong magical power and tactical knowledge to attack with AoE weapons to slowly poison and decay their enemies.",
        "Forged in canyon fortresses, the Granite Vow are elite engineer-soldiers bound by strength and discipline. Raised on the frontlines of the Dark Rifts, they carry unmatched durability and power—slow to move but impossible to stop, wielding heavy, crushing weapons and firepower to accompany their strong fortification spells and will power.",
        "Wielders of forsaken magics, the Hollow were not an original Great Faction, instead abandoned their kin of various factions to chase unspeakable power. Hidden in cursed ruins and shadowed tunnels, they mastered every form of magnumancy, bringing upon an eternal dark curse to their whole faction. Ruthless and unpredictable, they walk the line between mastery and madness."
    };

    public string[] passiveText = {
        "Flameshot: Bullets deal burn damage on contact with enemies or leave a burning flame on ground",
        "Frostarmor: Small chance to deflect bullets back at enemies",
        "Thunderstrike: Bullets chain to nearby enemies, dealing reduced damage",
        "Mistshroud: Every 3rd dash ability triggers a small smoke screen",
        "Bounty of the Forest: Returns a portion of damage dealt by planted thorns as health",
        "Blight: Bullets apply a poison effect to enemies that can spread to nearby targets",
        "Heavy Weight: Slams have increas AoE knockback and damage",
        "Shadowshot: 25% chance to fire a shadow bullet that can phase through walls and obstacles"
    };

    public string[] activeText = {
        "Hephaestus's Blessing: Ignite yourself in flames, igniting all enemies you come into contact with and burn away incoming bullets",
        "Winter's Judgement: Fires a piercing ice shard that does extreme damage and freezes enemy in place",
        "Thunderfall: Slam down, creating a shockwave that damages and stuns enemies in a large radius sendin chaining damage out to nearby enemies",
        "Abyssal Vortex: Next bullet fired creates a whirlpool that pulls enemies in, trapping them in place and dealing damage over time",
        "Spinebloom Mine: Deploy a mine that ensnares enemies caught in it's radius. Explodes after a period of time, releasing thorn spikes",
        "Pharoh's Plague: Release a toxic cloud that lingers in the air, floating around the battlefield for a duration, poisoning all enemies that come into contact with it",
        "Earthwarden's Bulwark: Summon a powerful stone wall, bloacking incoming bullets and providing cover until destroyed",
        "Forsaken Bargain: Next bullet fired curses hit enemy, causing them to take 2x damage from you. However, all other incoming damage is instead dealt to you. Curse lasts 5 sec."
    };

    public Image mainWiz;
    public Text nameText;
    public Text loreText;
    public Text passiveTextUI;
    public Text activeTextUI;
    public Image leftIcon;
    public Image rightIcon;
    public Image centerIcon;
    private int currentWizardIndex = 0;
    private bool rightPressed = false;
    private bool leftPressed = false;
    private int numHearts = 0;
    private int numOrbs = 0;
    void Start()
    {
        updateWizard();
    }

    void Update()
    {
        bool isPressedR = Gamepad.current.rightShoulder.isPressed;
        bool isPressedL = Gamepad.current.leftShoulder.isPressed;

        if (rightPressed && !isPressedR)
        {
            currentWizardIndex++;
            if (currentWizardIndex >= wizardImages.Length)
            {
                currentWizardIndex = 0;
            }
        }
        else if (leftPressed && !isPressedL)
        {
            currentWizardIndex--;
            if (currentWizardIndex < 0)
            {
                currentWizardIndex = wizardImages.Length - 1;
            }
        }

        leftPressed = isPressedL;
        rightPressed = isPressedR;

        updateWizard();
    }

    void updateWizard()
    {   
        string currentWizardName = WizardNames[currentWizardIndex];

        if (currentWizardName == "Emberguard")
        {
            numHearts = 3;
            numOrbs = 2;
        }
        else if (currentWizardName == "Frostwarden")
        {
            numHearts = 2;
            numOrbs = 4;
        }
        else if (currentWizardName == "Voltborn")
        {
            numHearts = 3;
            numOrbs = 3;
        }
        else if (currentWizardName == "Tidebound")
        {
            numHearts = 4;
            numOrbs = 2;
        }
        else if (currentWizardName == "Verdant Circle")
        {
            numHearts = 4;
            numOrbs = 3;
        }
        else if (currentWizardName == "Blightward")
        {
            numHearts = 2;
            numOrbs = 4;
        }
        else if (currentWizardName == "Granite Vow")
        {
            numHearts = 5;
            numOrbs = 2;
        }
        else if (currentWizardName == "The Hollow")
        {
            numHearts = 1;
            numOrbs = 5;
        }

        for (int i = 0; i < hearts.Length; i++)
        {
            if (i < numHearts)
                hearts[i].SetActive(true);
            else
                hearts[i].SetActive(false);
        }

        for (int i = 0; i < orbs.Length; i++)
        {
            if (i < numOrbs)
                orbs[i].SetActive(true);
            else
                orbs[i].SetActive(false);
        }

        loreText.text = wizardLore[currentWizardIndex];
        nameText.text = WizardNames[currentWizardIndex];
        passiveTextUI.text = passiveText[currentWizardIndex];
        activeTextUI.text = activeText[currentWizardIndex];
        mainWiz.sprite = wizardImages[currentWizardIndex];
        centerIcon.sprite = iconImages[currentWizardIndex];
        leftIcon.sprite = iconImages[(currentWizardIndex - 1 + wizardImages.Length) % wizardImages.Length];
        rightIcon.sprite = iconImages[(currentWizardIndex + 1) % wizardImages.Length];
    }
}
