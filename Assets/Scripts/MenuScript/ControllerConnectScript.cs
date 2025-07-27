using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControllerConnectScript : MonoBehaviour
{
    [Header("UI slots in join order (P1..Pn)")]
    public Image[] playerConnected;

    [Header("Options")]
    public bool allowKeyboardJoin = false;
    public Key  keyboardJoinKey   = Key.Enter;
    public bool autoRemoveOnDisconnect = true;

    public int numPlayers => joinedDevices.Count;
    public IReadOnlyList<InputDevice> JoinedDevices => joinedDevices;

    readonly List<InputDevice> joinedDevices = new();
    readonly List<(InputDevice dev, double time)> pending = new();

    InputAction joinPadAction;
    InputAction joinKbAction;

    const float checkInterval = 0.1f;
    float nextCheck;

    void Awake()
    {
        // Pad join: strictly X / West button
        joinPadAction = new InputAction("JoinPad", binding: "<Gamepad>/buttonWest");
        joinPadAction.performed += ctx =>
        {
            var dev = ctx.control.device;
            if (!joinedDevices.Contains(dev))
                pending.Add((dev, ctx.time));
        };

        if (allowKeyboardJoin)
        {
            joinKbAction = new InputAction("JoinKB", binding: $"<Keyboard>/{keyboardJoinKey}");
            joinKbAction.performed += ctx =>
            {
                var dev = ctx.control.device;
                if (!joinedDevices.Contains(dev))
                    pending.Add((dev, ctx.time));
            };
        }
    }

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        joinPadAction.Enable();
        joinKbAction?.Enable();
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        joinPadAction.Disable();
        joinKbAction?.Disable();
    }

    void Start()
    {
        UpdateUI();
    }

    void Update()
    {
        // cleanup
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + checkInterval;
            CleanupMissingDevices();
        }

        // process one join per frame, oldest first
        if (pending.Count > 0)
        {
            pending.Sort((a, b) => a.time.CompareTo(b.time));
            var rec = pending[0];
            pending.RemoveAt(0);
            if (!joinedDevices.Contains(rec.dev))
                JoinDevice(rec.dev);
        }
    }
    public System.Action<InputDevice> OnFirstPlayerJoined;

    void JoinDevice(InputDevice device)
    {
        if (joinedDevices.Count >= playerConnected.Length) return;

        joinedDevices.Add(device);
        UpdateUI();
        Debug.Log($"[Join] {device.displayName} -> P{joinedDevices.Count}");

        if (joinedDevices.Count == 1)
        {
            DataManager.Instance.SetMasterDevice(device);   // store P1
            OnFirstPlayerJoined?.Invoke(device);            // notify listeners
        }
    }

    void RemoveDevice(InputDevice device)
    {
        if (joinedDevices.Remove(device))
        {
            UpdateUI();
            Debug.Log($"[Leave] {device?.displayName}");
        }
    }

    void UpdateUI()
    {
        for (int i = 0; i < playerConnected.Length; i++)
        {
            bool active = i < joinedDevices.Count;
            if (playerConnected[i])
                playerConnected[i].gameObject.SetActive(active);
        }
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!autoRemoveOnDisconnect) return;
        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
            RemoveDevice(device);
    }

    void CleanupMissingDevices()
    {
        for (int i = joinedDevices.Count - 1; i >= 0; i--)
        {
            var d = joinedDevices[i];
            if (d == null || !d.added)
                RemoveDevice(d);
        }
    }

    // Helpers
    public int GetPlayerIndex(InputDevice device) => joinedDevices.IndexOf(device);
    public InputDevice GetDeviceForPlayer(int index) =>
        (index >= 0 && index < joinedDevices.Count) ? joinedDevices[index] : null;

    public void SaveToDataManager()
    {
        DataManager.Instance.SetInputs(JoinedDevices);
    }
}
