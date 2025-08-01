using UnityEngine;
using UnityEngine.InputSystem;
using Magnumancer.Abilities;

public class WizardAbilityController : MonoBehaviour
{
    public WizardData wizardData;
    private IActiveAbility abilityInstance;
    private AbilityCooldown cooldown;
    public Gamepad gamepad;

    private bool isInitialized = false;

    public void Setup(Gamepad pad, WizardData wiz, CircleAbilityUI ui)
    {
        gamepad = pad;
        wizardData = wiz;

        if (wizardData == null)
        {
            Debug.LogWarning("[WizardAbilityController] WizardData is NULL!");
            return;
        }

        if (wizardData.activeAbilityPrefab == null)
        {
            Debug.LogWarning("[WizardAbilityController] activeAbilityPrefab is NULL!");
            return;
        }

        // Instantiate ability object as a child
        GameObject abilityObj = Instantiate(wizardData.activeAbilityPrefab, transform);
        abilityInstance = abilityObj.GetComponent<IActiveAbility>();
        cooldown = abilityObj.GetComponent<AbilityCooldown>();

        if (abilityInstance == null)
        {
            Debug.LogWarning("[WizardAbilityController] ability prefab does not implement IActiveAbility!");
        }

        if (cooldown != null)
        {
            cooldown.cooldownTime = wizardData.abilityCooldown;
            cooldown.ui = ui;
        }

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || gamepad == null) return;

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            if (cooldown != null && cooldown.IsOnCooldown())
                return;

            abilityInstance?.Activate(gameObject);

            if (cooldown != null)
                cooldown.TriggerCooldown();
        }
    }
}
