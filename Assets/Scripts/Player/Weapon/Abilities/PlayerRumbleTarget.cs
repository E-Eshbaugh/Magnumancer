using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRumbleTarget : MonoBehaviour
{
    public Gamepad gamepad;
    public void Setup(Gamepad pad)
    {
        gamepad = pad;
        if (gamepad == null)
        {
            Debug.LogWarning("[PlayerRumbleTarget] Gamepad is NULL in Setup()");
        }
    }

    private void OnEnable()
    {
        if (!players.Contains(this)) players.Add(this);
    }

    private void OnDisable()
    {
        players.Remove(this);
    }

    public static List<PlayerRumbleTarget> players = new();
}
