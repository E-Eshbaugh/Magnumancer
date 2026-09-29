using System.Collections;
using UnityEngine;

/// <summary>
/// Tidebound — Undercurrent: dash cooldown is reduced by 30%.
/// After dashing, gain a brief movement boost.
/// </summary>
public class UndercurrentPassive : WizardPassive
{
    public float dashCooldownMultiplier = 0.7f;
    public float boostMultiplier = 1.3f;
    public float boostDuration = 1.5f;

    Coroutine boost;

    public override void Init(WizardData data)
    {
        base.Init(data);
        if (movement == null) return;
        movement.dashCooldownMultiplier = dashCooldownMultiplier;
        movement.OnDash += HandleDash;
    }

    void OnDestroy()
    {
        if (movement != null) movement.OnDash -= HandleDash;
    }

    void OnDisable()
    {
        if (movement != null) movement.ClearSpeedModifier("undercurrent");
        boost = null;
    }

    void HandleDash()
    {
        if (boost != null) StopCoroutine(boost);
        boost = StartCoroutine(Boost());
    }

    IEnumerator Boost()
    {
        movement.SetSpeedModifier("undercurrent", boostMultiplier);
        yield return new WaitForSeconds(boostDuration);
        movement.ClearSpeedModifier("undercurrent");
        boost = null;
    }
}
