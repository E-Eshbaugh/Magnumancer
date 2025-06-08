using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CharacterSelectController : MonoBehaviour
{
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

    private int currentWizardIndex = 0;
    private bool rightPressed = false;
    private bool leftPressed = false;

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
    }

    void updateWizard()
    {
        selectedWizard = allWizards[currentWizardIndex];

        nameText.text = selectedWizard.wizardName;
        loreText.text = selectedWizard.loreText;
        passiveTextUI.text = selectedWizard.passiveAbility;
        activeTextUI.text = selectedWizard.activeAbility;
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
