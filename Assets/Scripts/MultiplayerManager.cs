using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class MultiplayerManager : MonoBehaviour
{
    [Tooltip("Drag in your Player GameObjects here (root or prefab instances).")]
    public GameObject[] players;

    [Tooltip("How many controllers to support.")]
    public int numPlayers = 2;
    public CircleAbilityUI[] uiControllers;
    public WeaponData[] player1Loadout;
    public WizardData player1Wizard;

    void Start()
    {
        //loadout assignment
        player1Loadout = DataManager.Instance.p1_loadout;
        player1Wizard = DataManager.Instance.GetWizard(0);

        Debug.Log("=== MultiplayerManager Setup ===");

        // 1) Deactivate all slots
        for (int i = 0; i < players.Length; i++)
        {
            players[i].SetActive(false);
            Debug.Log($"Slot {i} deactivated: {players[i].name}");
        }

        // 2) Grab connected pads
        var pads = Gamepad.all;
        Debug.Log($"Found {pads.Count} gamepads");

        var uis  = FindObjectsByType<CircleAbilityUI>(FindObjectsSortMode.None);
        for (int i = 0; i < uis.Length; i++)
        {
            var ui  = uis[i];
            var pad = (i < pads.Count) ? pads[i] : null;
            ui.gamepad = pad;
            Debug.Log($"[Multiplayer] Assigned pad {(pad != null ? pad.displayName : "None")} to UI {ui.name}");
        }

        // 3) Decide how many to activate
        int count = Mathf.Min(numPlayers, players.Length, pads.Count);
        Debug.Log($"Activating {count} player(s)");

        for (int i = 0; i < count; i++)
        {
            var pad = pads[i];
            var go = players[i];
            go.SetActive(true);
            Debug.Log($"Slot {i} activated: {go.name}");

            // Optional pairing
            var user = InputUser.CreateUserWithoutPairedDevices();
            InputUser.PerformPairingWithDevice(pad, user);

            // Movement
            var move = go.GetComponentInChildren<PlayerMovement3D>();
            if (move != null)
            {
                move.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to PlayerMovement3D on {move.name}");
            }
            else Debug.LogWarning($"No PlayerMovement3D on {go.name} or its children!");

            // Weapon swap
            var swap = go.GetComponentInChildren<GunSwapControl>();
            if (swap != null)
            {
                swap.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to GunSwapControl on {swap.name}");
            }
            else Debug.LogWarning($"No GunSwapControl on {go.name} or its children!");

            // Firing
            var fire = go.GetComponentInChildren<FireController3D>();
            if (fire != null)
            {
                fire.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to FireController3D on {fire.name}");
            }
            else Debug.LogWarning($"No FireController3D on {go.name} or its children!");

            // Ammo UI
            var ammo = go.GetComponentInChildren<AmmoControl>();
            if (ammo != null)
            {
                ammo.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to AmmoControl on {ammo.name}");
                ammo.guns = player1Loadout;
                Debug.Log($" → Assigned array length {player1Loadout.Length} to ammo.guns");
            }
            else Debug.LogWarning($"No AmmoControl on {go.name} or its children!");

            // Orbit
            var orbit = go.GetComponentInChildren<GunOrbitController>();
            if (orbit != null)
            {
                orbit.gamepad = pad;
                orbit.player = go.transform;
                Debug.Log($" → Assigned pad {i} to GunOrbitController on {orbit.name}");
            }
            else Debug.LogWarning($"No GunOrbitController on {go.name} or its children!");

            var over = go.GetComponentInChildren<OverClock>();
            if (over != null)
            {
                over.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to OverClock on {over.name}");
            }

            var laser = go.GetComponentInChildren<LaserScope>();
            if (laser != null)
            {
                laser.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to LaserScope on {laser.name}");
            }
            else
            {
                Debug.LogWarning($"No LaserScope on {go.name} or its children!");
            }

            var akimbo = go.GetComponentInChildren<AkimboController>();
            if (akimbo != null)
            {
                akimbo.gamepad = pad;
                Debug.Log($" → Assigned pad {i} to AkimboController on {akimbo.name}");
            }
            else
            {
                Debug.LogWarning($"No AkimboController on {go.name} or its children!");
            }

            // Attempt to use the player's tag, or fallback to a default
            if (i < uiControllers.Length && uiControllers[i] != null)
            {
                uiControllers[i].gamepad = pad;
                uiControllers[i].crestImage.sprite = player1Wizard.factionEmblem;
                Debug.Log($" → Assigned pad {i} to CircleAbilityUI on {uiControllers[i].gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"No uiControllers[{i}] assigned in the inspector!");
            }

            var wizAbility = go.GetComponentInChildren<WizardAbilityController>();
            if (wizAbility != null)
            {
                wizAbility.gamepad = pad;
                wizAbility.wizard = player1Wizard;
                Debug.Log($" → Assigned pad {i} to WizardAbilityController on {wizAbility.name}");
            }
            else
            {
                Debug.LogWarning($"No WizardAbilityController on {go.name} or its children!");
            }

        }

        Debug.Log("=== Setup Complete ===");
    }
}
