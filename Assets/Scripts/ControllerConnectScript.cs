using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ControllerConnectScript : MonoBehaviour
{
    [Header("UI slots in join order (P1..Pn)")]
    public Image[] playerConnected;

    [Header("Options")]
    public bool allowKeyboardJoin = false;
    public Key keyboardJoinKey = Key.Enter;
    public bool autoRemoveOnDisconnect = true;
    public bool autoJoinAllExistingPads = false; // if true, any pads already plugged in join at Start too

    // Internal
    readonly List<InputDevice> joinedDevices = new();
    const float checkInterval = 0.1f;
    float nextCheck;

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void Start()
    {
        // 1) Always reserve Player 1 for the "current" controller (or first pad found)
        var firstPad = Gamepad.current ?? (Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
        if (firstPad != null) JoinDevice(firstPad);

        // 2) Optionally auto-join any other pads already connected
        if (autoJoinAllExistingPads)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pad != firstPad && !joinedDevices.Contains(pad))
                    JoinDevice(pad);
            }
        }

        // 3) Optional keyboard as last resort (P? slot)
        if (allowKeyboardJoin && Keyboard.current != null && !joinedDevices.Contains(Keyboard.current))
        {
            // Comment this out if you want keyboard to press a key instead of auto-join
            // JoinDevice(Keyboard.current);
        }

        UpdateUI();
    }

    void Update()
    {
        // Throttle cleanup checks
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + checkInterval;
            CleanupMissingDevices();
        }

        // Join on X (buttonWest) for pads not yet joined
        foreach (var pad in Gamepad.all)
        {
            if (pad == null || joinedDevices.Contains(pad)) continue;
            if (pad.buttonWest.wasPressedThisFrame)
                JoinDevice(pad);
        }

        // Optional keyboard join
        if (allowKeyboardJoin && Keyboard.current != null && !joinedDevices.Contains(Keyboard.current))
        {
            if (Keyboard.current[keyboardJoinKey].wasPressedThisFrame)
                JoinDevice(Keyboard.current);
        }
    }

    void JoinDevice(InputDevice device)
    {
        if (joinedDevices.Count >= playerConnected.Length) return; // no slots left
        joinedDevices.Add(device);
        UpdateUI();
        Debug.Log($"[Join] {device.displayName}");
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
            if (playerConnected[i] != null)
                playerConnected[i].gameObject.SetActive(active);
        }
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!autoRemoveOnDisconnect) return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Removed:
                RemoveDevice(device);
                break;
        }
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

    // Helper if you need to map device -> player index elsewhere
    public int GetPlayerIndex(InputDevice device) => joinedDevices.IndexOf(device); // 0-based
    public InputDevice GetDeviceForPlayer(int index) =>
        (index >= 0 && index < joinedDevices.Count) ? joinedDevices[index] : null;
}
