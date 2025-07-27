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
            var pad     = device as Gamepad;
            var wizard  = DataManager.Instance.GetWizard(i);
            var loadout = DataManager.Instance.GetLoadout(i);

            Debug.Log($"[MPM] Player {i} wizard: {(wizard != null ? wizard.wizardName : "NULL")}");

            go.SetActive(true);

            // === SETUP SEQUENCE ===

            go.GetComponentInChildren<PlayerMovement3D>()?.Setup(i, pad, wizard);
            go.GetComponentInChildren<GunSwapControl>()?.Setup(pad, loadout);
            go.GetComponentInChildren<AmmoControl>()?.Setup(pad, loadout);
            go.GetComponentInChildren<FireController3D>()?.Setup(pad);
            go.GetComponentInChildren<GunOrbitController>()?.Setup(pad, go.transform);
            go.GetComponentInChildren<OverClock>()?.Setup(pad);
            go.GetComponentInChildren<LaserScope>()?.Setup(pad);
            go.GetComponentInChildren<AkimboController>()?.Setup(pad);
            go.GetComponentInChildren<WizardAbilityController>()?.Setup(pad, wizard);
            go.GetComponentInChildren<ArAbilityController>()?.Setup(pad);

            // Appearance
            var appearance = go.GetComponentInChildren<PlayerAppearance>();
            if (appearance != null) appearance.Setup(wizard);

            // === UI SETUP ===
            if (i < uiControllers.Length && uiControllers[i] != null)
            {
                if (wizard != null)
                    uiControllers[i].Setup(pad, wizard.factionEmblem);
                else
                    Debug.LogWarning($"[MPM] WizardData missing for player {i}, cannot assign crest sprite.");
            }

            Debug.Log($"Player {i} wired. Pad: {pad?.displayName ?? "None"}, Wizard: {wizard?.wizardName ?? "NULL"}, Guns: {loadout?.Length ?? 0}");
        }

        Debug.Log("=== Multiplayer Setup Complete ===");
    }
}
