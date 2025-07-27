using UnityEngine;
using UnityEngine.InputSystem;
using Magnumancer.Abilities;

public class WizardAbilityController : MonoBehaviour
{
    public WizardData wizardData;
    private IActiveAbility abilityInstance;
    public Gamepad gamepad;
    public bool isUsingAbility = false;

    private bool isInitialized = false;

    public void Setup(Gamepad pad, WizardData wiz)
    {
        gamepad = pad;
        wizardData = wiz;
        isUsingAbility = false;

        if (wizardData == null)
        {
            Debug.LogWarning("[WizardAbilityController on " + gameObject.name + "] wizardData is NULL in Setup()!");
            return;
        }

        if (wizardData.activeAbilityPrefab == null)
        {
            Debug.LogWarning("[WizardAbilityController on " + gameObject.name + "] activeAbilityPrefab is NULL in Setup()!");
            return;
        }

        GameObject abilityObj = Instantiate(wizardData.activeAbilityPrefab, transform);
        abilityInstance = abilityObj.GetComponent<IActiveAbility>();
        if (abilityInstance == null)
        {
            Debug.LogWarning("[WizardAbilityController on " + gameObject.name + "] ability prefab does not implement IActiveAbility!");
        }

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || gamepad == null) return;

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            abilityInstance?.Activate(gameObject);
        }
    }
}
