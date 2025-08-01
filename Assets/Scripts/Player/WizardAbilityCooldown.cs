using UnityEngine;

public class AbilityCooldown : MonoBehaviour
{
    public float cooldownTime = 10f;
    public CircleAbilityUI ui;

    private float timer = 0f;
    private bool coolingDown = false;

    public bool IsOnCooldown() => coolingDown;

    public void TriggerCooldown()
    {
        timer = cooldownTime;
        coolingDown = true;
    }

    void Start()
    {
        // Trigger initial cooldown when scene starts
        TriggerCooldown();

        // Start UI from empty
        if (ui != null)
            ui.SetCooldownFill(0f);
    }

    void Update()
    {
        if (!coolingDown) return;

        timer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - (timer / cooldownTime));

        if (ui != null)
            ui.SetCooldownFill(t);

        if (timer <= 0f)
        {
            coolingDown = false;
            if (ui != null)
                ui.SetCooldownFill(1f);  // ensure pulse activates
        }
    }
}
