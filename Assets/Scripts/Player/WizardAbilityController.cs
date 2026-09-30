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

    /// Raised when the active ability fires (Lightning Reflex counts teleports)
    public event System.Action OnAbilityActivated;

    /// 0..1 ability charge (1 = ready). No cooldown component = always ready.
    public float Charge => cooldown != null ? cooldown.Charge : (isInitialized ? 1f : 0f);
    public bool IsReady => Charge >= 1f;
    public AbilityCooldown Cooldown => cooldown;

    /// Which active rune (0 = the wizard's original ability)
    public int ActiveRune { get; private set; }

    public void Setup(Gamepad pad, WizardData wiz, CircleAbilityUI ui, int activeRune = 0)
    {
        gamepad = pad;
        wizardData = wiz;

        if (wizardData == null)
        {
            Debug.LogWarning("[WizardAbilityController] WizardData is NULL!");
            return;
        }

        // Runes II/III are code abilities with their own cooldown
        var rune = RuneBook.Active(wizardData, activeRune);
        if (activeRune > 0 && rune != null && rune.abilityType != null)
        {
            ActiveRune = activeRune;
            var go = new GameObject($"Rune {RuneBook.Numeral(activeRune)}: {rune.name}");
            go.transform.SetParent(transform, false);
            abilityInstance = go.AddComponent(rune.abilityType) as IActiveAbility;
            cooldown = go.AddComponent<AbilityCooldown>();
            cooldown.cooldownTime = rune.cooldown;
            cooldown.ui = ui;
            isInitialized = true;
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
        if (!isInitialized || gamepad == null || GamePause.InputBlocked) return;

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            if (cooldown != null && cooldown.IsOnCooldown())
                return;

            abilityInstance?.Activate(gameObject);

            // No target (e.g. Soul Swap with nobody in reach): no cooldown, no fanfare
            if (abilityInstance is IAbilityOutcome outcome && outcome.Fizzled)
                return;

            Rumble.WizardAbility(gameObject);
            OnAbilityActivated?.Invoke();

            if (cooldown != null)
                cooldown.TriggerCooldown();
        }
    }
}
