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

    [Tooltip("Gap between each wizard's arrival bolt at round start")]
    public float arrivalStagger = 0.2f;

    void Start()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogError("[MPM] No DataManager found. Start from the MainMenu scene so players, wizards and loadouts get set up.");
            return;
        }

        int numPlayers = Mathf.Clamp(DataManager.Instance.NumPlayers, 1, players.Length);

        // Deactivate all
        for (int i = 0; i < players.Length; i++) players[i].SetActive(false);

        // Shades already worn, per wizard (two players on one wizard never look alike)
        var shadesTaken = new System.Collections.Generic.Dictionary<WizardData, System.Collections.Generic.List<int>>();

        for (int i = 0; i < numPlayers; i++)
        {
            var go      = players[i];
            var device  = DataManager.Instance.GetDevice(i);
            var pad     = device as Gamepad;
            var wizard  = DataManager.Instance.GetWizard(i);
            var loadout = DataManager.Instance.GetLoadout(i);

            Debug.Log($"[MPM] Player {i} wizard: {(wizard != null ? wizard.wizardName : "NULL")}");

            // === UI SETUP ===
            if (i < uiControllers.Length && uiControllers[i] != null)
            {
                if (wizard != null)
                    uiControllers[i].Setup(pad, wizard.factionEmblem);
                else
                    Debug.LogWarning($"[MPM] WizardData missing for player {i}, cannot assign crest sprite.");
            }

            go.SetActive(true);

            // === SETUP SEQUENCE ===

            go.GetComponentInChildren<PlayerMovement3D>()?.Setup(i, pad, wizard);

            // The active rune picks the wizard's shade: before anything reads the color
            var shadeHost = go.GetComponentInChildren<PlayerMovement3D>();
            if (shadeHost != null && wizard != null)
            {
                if (!shadesTaken.TryGetValue(wizard, out var taken)) shadesTaken[wizard] = taken = new System.Collections.Generic.List<int>();
                taken.Add(WizardShade.Apply(shadeHost.gameObject, wizard, DataManager.Instance.GetActiveRune(i), taken).shade);
            }
            go.GetComponentInChildren<GunSwapControl>()?.Setup(pad, loadout);
            go.GetComponentInChildren<AmmoControl>()?.Setup(pad, loadout, wizard);
            go.GetComponentInChildren<FireController3D>()?.Setup(pad);
            go.GetComponentInChildren<GunOrbitController>()?.Setup(pad, go.transform);
            go.GetComponentInChildren<OverClock>()?.Setup(pad);
            go.GetComponentInChildren<LaserScope>()?.Setup(pad);
            go.GetComponentInChildren<AkimboController>()?.Setup(pad);
            int activeRune = DataManager.Instance.GetActiveRune(i);
            int passiveRune = DataManager.Instance.GetPassiveRune(i);
            go.GetComponentInChildren<WizardAbilityController>()?.Setup(pad, wizard, uiControllers[i], activeRune);
            go.GetComponentInChildren<ArAbilityController>()?.Setup(pad);

            // Emberblast's Remote Fuse lives next to the other gun abilities
            var ammo = go.GetComponentInChildren<AmmoControl>();
            if (ammo != null)
            {
                var detonator = ammo.GetComponent<RemoteDetonator>();
                if (detonator == null) detonator = ammo.gameObject.AddComponent<RemoteDetonator>();
                detonator.Setup(pad);
            }

            // Appearance
            var appearance = go.GetComponentInChildren<PlayerAppearance>();
            if (appearance != null) appearance.Setup(wizard);

            // Wizard hearts scale one health pool, plus the wizard's passive
            var health = go.GetComponentInChildren<PlayerHealthControl>();
            if (health != null && wizard != null)
            {
                health.SetHealthStat(Mathf.Max(1, wizard.heartCount));
                WizardPassive.AddTo(health.gameObject, wizard, passiveRune);
            }
            if (health != null) WizardLifeRing.AddTo(health);
            if (health != null && (ZombiesPoints.Active || FindAnyObjectByType<GoblinSpawner>() != null))
                PlayerPointsDisplay.Bind(health, i < uiControllers.Length ? uiControllers[i] : null);


            // Initial arrival effect; revives happen where the wizard fell
            var spawnRoot = health != null ? health.transform : go.transform;
            WizardSpawnEffect.Play(spawnRoot.gameObject, wizard, i * arrivalStagger);

            // Ability charge shown as flames on the wizard; dashes leave a lightning trail
            var mover = go.GetComponentInChildren<PlayerMovement3D>();
            if (mover != null)
            {
                WizardChargeAura.AddTo(mover.gameObject, wizard);
                WizardDashTrail.AddTo(mover.gameObject, wizard);
                WeaponSynergy.AddTo(mover.gameObject, wizard);
            }

            Debug.Log($"Player {i} wired. Pad: {pad?.displayName ?? "None"}, Wizard: {wizard?.wizardName ?? "NULL"}, Guns: {loadout?.Length ?? 0}");
        }

        // Some prototype arenas (including Zombies) have no placed match controller.
        if (FindAnyObjectByType<WinManager>() == null)
            new GameObject("WinManager").AddComponent<WinManager>();

        Debug.Log("=== Multiplayer Setup Complete ===");
    }
}
