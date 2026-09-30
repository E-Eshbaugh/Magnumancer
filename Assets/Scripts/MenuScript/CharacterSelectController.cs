using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CharacterSelectController : MonoBehaviour

{
    public Gamepad activePad;
    [Header("-- Wizard Setup --")]
    public WizardData[] allWizards; // Set these in Inspector
    public Image mainWiz;
    public Text nameText;
    public Text loreText;
    public Text passiveTextUI;
    public Text activeTextUI;
    public Image leftIcon;
    public Image rightIcon;
    public Image centerIcon;
    public GameObject[] hearts;
    public GameObject[] orbs;

    [Header("-- Runtime Selection --")]
    public static CharacterSelectController Instance;
    public WizardData selectedWizard;

    [Header("-- Runes --")]
    [Tooltip("D-pad left/right picks the active rune, up/down the passive rune")]
    public bool showRuneHints = true;
    public int selectedActiveRune;
    public int selectedPassiveRune;

    private int currentWizardIndex = 0;
    private bool rightPressed = false;
    private bool leftPressed = false;

    void Start()
    {
        updateWizard();
    }

    void Update()
    {
        if (activePad != null && (activePad.leftShoulder.wasPressedThisFrame || activePad.rightShoulder.wasPressedThisFrame))
        Debug.Log($"Wizard scroll from {activePad.displayName}");

        if (activePad == null) return;

        bool isPressedR = activePad.rightShoulder.isPressed;
        bool isPressedL = activePad.leftShoulder.isPressed;

        if (rightPressed && !isPressedR)
        {
            currentWizardIndex = (currentWizardIndex + 1) % allWizards.Length;
            updateWizard();
        }
        else if (leftPressed && !isPressedL)
        {
            currentWizardIndex = (currentWizardIndex - 1 + allWizards.Length) % allWizards.Length;
            updateWizard();
        }

        rightPressed = isPressedR;
        leftPressed = isPressedL;

        // Runes: d-pad left/right = active (3), up/down = passive (2)
        bool changed = false;
        if (activePad.dpad.right.wasPressedThisFrame) { selectedActiveRune = (selectedActiveRune + 1) % RuneBook.ActiveCount; changed = true; }
        if (activePad.dpad.left.wasPressedThisFrame) { selectedActiveRune = (selectedActiveRune + RuneBook.ActiveCount - 1) % RuneBook.ActiveCount; changed = true; }
        if (activePad.dpad.up.wasPressedThisFrame || activePad.dpad.down.wasPressedThisFrame)
        {
            selectedPassiveRune = (selectedPassiveRune + 1) % RuneBook.PassiveCount;
            changed = true;
        }
        if (changed)
        {
            UpdateRuneText();
            Rumble.Swap(activePad);
        }
    }

    /// Shows the picked runes in the ability text boxes (plus the wizard's weapon affinity)
    void UpdateRuneText()
    {
        // the name takes the shade this active rune gives the wizard in the match
        if (nameText != null && selectedWizard != null)
            nameText.color = GlowLine.Brighten(WizardShade.Shade(selectedWizard, selectedActiveRune));

        var runes = RuneBook.For(selectedWizard);
        if (runes == null)
        {
            passiveTextUI.text = selectedWizard.passiveAbilityTxt;
            activeTextUI.text = selectedWizard.activeAbilityTxt;
            return;
        }

        var a = runes.actives[selectedActiveRune];
        var p = runes.passives[selectedPassiveRune];
        string aHint = showRuneHints ? "<  " : "";
        string aHintEnd = showRuneHints ? "  >" : "";
        activeTextUI.text =
            $"{aHint}Rune {RuneBook.Numeral(selectedActiveRune)}/{RuneBook.Numeral(RuneBook.ActiveCount - 1)}: {a.name}{aHintEnd}  ({a.cooldown:0}s)\n{a.description}";

        string pHint = showRuneHints ? "^v " : "";
        var f = runes.affinity;
        passiveTextUI.text =
            $"{pHint}Rune {RuneBook.Numeral(selectedPassiveRune)}/{RuneBook.Numeral(RuneBook.PassiveCount - 1)}: {p.name}\n{p.description}" +
            (f != null ? $"\nAffinity - {RuneBook.ClassList(f)}: {f.name}. {f.description}" : "");
    }

    // Which player is picking (slots before it are this session's locked picks)
    int pickerSlot;

    public void SetPicker(int slot)
    {
        pickerSlot = slot;
        // refresh just the hint (going back to this page keeps the rune picks)
        if (selectedWizard != null && loreText != null) loreText.text = CompHint() + selectedWizard.loreText;
    }

    /// Role and named comps (Elemental Ecosystem), plus a callout in co-op/team modes
    /// when an earlier pick completes a comp with the highlighted wizard
    string CompHint()
    {
        var dm = DataManager.Instance;
        var picked = new System.Collections.Generic.List<(int, WizardData)>();
        if (dm != null && dm.Wizards != null)
            for (int i = 0; i < pickerSlot && i < dm.Wizards.Length; i++)
                if (dm.Wizards[i] != null) picked.Add((i, dm.Wizards[i]));
        bool teamMode = dm != null && dm.SelectedMode != 0;   // 0 = deathmatch
        return ElementComps.Hint(selectedWizard, picked, teamMode);
    }

    void updateWizard()
    {
        selectedWizard = allWizards[currentWizardIndex];

        nameText.text = selectedWizard.wizardName;
        loreText.text = CompHint() + selectedWizard.loreText;
        // new wizard: start from their original runes
        selectedActiveRune = 0;
        selectedPassiveRune = 0;
        UpdateRuneText();
        mainWiz.sprite = selectedWizard.charcterImage;
        centerIcon.sprite = selectedWizard.factionEmblem;

        leftIcon.sprite = allWizards[(currentWizardIndex - 1 + allWizards.Length) % allWizards.Length].factionEmblem;
        rightIcon.sprite = allWizards[(currentWizardIndex + 1) % allWizards.Length].factionEmblem;

        for (int i = 0; i < hearts.Length; i++)
            hearts[i].SetActive(i < selectedWizard.heartCount);

        for (int i = 0; i < orbs.Length; i++)
            orbs[i].SetActive(i < selectedWizard.orbCount);
    }
}
