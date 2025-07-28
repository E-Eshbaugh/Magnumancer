using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LavaHazard : MonoBehaviour
{
    [SerializeField] int damagePerTick = 10;
    [SerializeField] float tickInterval = 1f;
    [SerializeField] float slowMultiplier = 0.5f;
    [SerializeField] string playerTag = "Player";
    [SerializeField] float rumbleLow = 0.1f;
    [SerializeField] float rumbleHigh = 0.2f;

    private Dictionary<GameObject, Coroutine> affectedPlayers = new();

    void OnTriggerEnter(Collider other)
    {
        if (!other.tag.StartsWith("Player")) return;

        if (!affectedPlayers.ContainsKey(other.gameObject))
        {
            Coroutine co = StartCoroutine(ApplyBurn(other.gameObject));
            affectedPlayers.Add(other.gameObject, co);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.tag.StartsWith("Player")) return;

        if (affectedPlayers.TryGetValue(other.gameObject, out Coroutine co))
        {
            StopCoroutine(co);
            ResetSpeed(other.gameObject);
            StopRumble(other.gameObject);
            affectedPlayers.Remove(other.gameObject);
        }
    }

    IEnumerator ApplyBurn(GameObject player)
    {
        var health = player.GetComponent<PlayerHealthControl>();
        var move = player.GetComponent<PlayerMovement3D>();
        var gamepad = player.GetComponent<PlayerMovement3D>()?.gamepad;

        // Apply slow
        if (move != null) move.currentMoveSpeed *= slowMultiplier;

        // Soft rumble while inside
        if (gamepad != null)
            gamepad.SetMotorSpeeds(rumbleLow, rumbleHigh);

        while (true)
        {
            if (health != null)
                health.TakeDamage(damagePerTick);

            yield return new WaitForSeconds(tickInterval);
        }
    }

    void ResetSpeed(GameObject player)
    {
        var move = player.GetComponent<PlayerMovement3D>();
        if (move != null)
            move.currentMoveSpeed /= slowMultiplier;
    }

    void StopRumble(GameObject player)
    {
        var gamepad = player.GetComponent<PlayerMovement3D>()?.gamepad;
        if (gamepad != null)
            gamepad.SetMotorSpeeds(0f, 0f);
    }

    void OnDestroy()
    {
        foreach (var pair in affectedPlayers)
        {
            if (pair.Key != null)
            {
                ResetSpeed(pair.Key);
                StopRumble(pair.Key);
            }
        }
        affectedPlayers.Clear();
    }
}
