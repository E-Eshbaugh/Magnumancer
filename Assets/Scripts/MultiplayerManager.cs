using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class MultiplayerManager : MonoBehaviour
{
    [Tooltip("Root player objects in spawn/order (size ≥ max players).")]
    public GameObject[] players;

    [Tooltip("Per‑player UI controllers (same order as players).")]
    public CircleAbilityUI[] uiControllers;

    [Tooltip("Pair devices to users (InputSystem users).")]
    public bool pairDevicesToUsers = true;
    public ControllerConnectScript controllerConnectScript;

    void Start()
    {
        int numPlayers = Mathf.Clamp(DataManager.Instance.NumPlayers, 1, players.Length);

        // Deactivate all
        for (int i = 0; i < players.Length; i++) players[i].SetActive(false);

        for (int i = 0; i < numPlayers; i++)
        {
            var go      = players[i];
            var device  = DataManager.Instance.GetDevice(i);
            var pad     = device as Gamepad;        // could be null if keyboard
            var wizard  = DataManager.Instance.GetWizard(i);
            var loadout = DataManager.Instance.GetLoadout(i);

            go.SetActive(true);

            // Movement
            var move = go.GetComponentInChildren<PlayerMovement3D>();
            if (move) move.Setup(i, pad, wizard);

            var swap = go.GetComponentInChildren<GunSwapControl>();
            if (swap) swap.Setup(pad, loadout);

            var ammo = go.GetComponentInChildren<AmmoControl>();
            if (ammo) ammo.Setup(pad, loadout);

            // Fire
            var fire = go.GetComponentInChildren<FireController3D>();
            if (fire) fire.gamepad = pad;

            // Orbit, misc abilities
            var orbit = go.GetComponentInChildren<GunOrbitController>();
            if (orbit)
            {
                orbit.gamepad = pad;
                orbit.player  = go.transform;
            }

            var over = go.GetComponentInChildren<OverClock>();
            if (over) over.gamepad = pad;

            var laser = go.GetComponentInChildren<LaserScope>();
            if (laser) laser.gamepad = pad;

            var akimbo = go.GetComponentInChildren<AkimboController>();
            if (akimbo) akimbo.gamepad = pad;

            var wizAbility = go.GetComponentInChildren<WizardAbilityController>();
            if (wizAbility)
            {
                wizAbility.gamepad = pad;
                wizAbility.wizard  = wizard;
            }

            var arAbility = go.GetComponentInChildren<ArAbilityController>();
            if (arAbility) arAbility.gamepadOverride = pad;

            // Per‑player ability UI
            if (i < uiControllers.Length && uiControllers[i] != null)
            {
                uiControllers[i].gamepad = pad;
                if (wizard) uiControllers[i].crestImage.sprite = wizard.factionEmblem;
            }

            Debug.Log($"Player {i} wired. Pad: {pad?.displayName ?? "None"}, Wizard: {wizard?.wizardName ?? "NULL"}, Guns: {loadout?.Length ?? 0}");
        }

        Debug.Log("=== Setup Complete ===");
    }
}
