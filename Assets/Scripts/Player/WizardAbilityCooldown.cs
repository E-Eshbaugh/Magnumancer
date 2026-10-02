using UnityEngine;

public class AbilityCooldown : MonoBehaviour
{
    public float cooldownTime = 10f;
    public CircleAbilityUI ui;

    private float timer = 0f;
    private bool coolingDown = false;

    public bool IsOnCooldown() => coolingDown;

    /// Takes seconds off the current cooldown (Kindling)
    public void Reduce(float seconds)
    {
        if (!coolingDown) return;
        timer = Mathf.Max(0.01f, timer - seconds);
    }

    /// Makes the ability ready now
    public void Refresh()
    {
        timer = 0f;
        coolingDown = false;
        if (ui != null) ui.SetCooldownFill(1f);
    }

    /// 0 = just used, 1 = ready (drives the crest glow and the wizard's charge aura)
    public float Charge => coolingDown && cooldownTime > 0f ? Mathf.Clamp01(1f - timer / cooldownTime) : 1f;

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
