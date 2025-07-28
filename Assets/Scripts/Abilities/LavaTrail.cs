using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LavaTrail : MonoBehaviour
{
    [Header("Landing Behavior")]
    [SerializeField] float fallSpeed = 8f;
    [SerializeField] float groundCheckDistance = 10f;
    [SerializeField] float offsetY = 0.02f;
    [SerializeField] LayerMask groundMask;

    [Header("Effect Settings")]
    [SerializeField] int damagePerTick = 10;
    [SerializeField] float tickInterval = 1f;
    [SerializeField] float slowMultiplier = 0.5f;
    [SerializeField] float lifetime = 10f;

    [Header("Target Settings")]
    [SerializeField] string playerTag = "Player";

    private HashSet<GameObject> affectedPlayers = new();
    private bool hasLanded = false;
    private bool firstFramePassed = false;

    void Start()
    {
        Destroy(gameObject, lifetime); // automatic cleanup
        StartCoroutine(DamageLoop());
    }

    void Update()
    {
        if (!firstFramePassed)
        {
            firstFramePassed = true;
            return;
        }

        if (hasLanded) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
        {
            transform.position = hit.point + Vector3.up * offsetY;
            hasLanded = true;
            return;
        }

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }

    IEnumerator DamageLoop()
    {
        while (true)
        {
            foreach (var player in affectedPlayers)
            {
                var health = player.GetComponent<PlayerHealthControl>();
                if (health != null)
                    health.TakeDamage(damagePerTick);
            }

            yield return new WaitForSeconds(tickInterval);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.tag.StartsWith(playerTag)) return;
        if (affectedPlayers.Contains(other.gameObject)) return;

        affectedPlayers.Add(other.gameObject);

        var move = other.GetComponent<PlayerMovement3D>();
        if (move != null)
            move.currentMoveSpeed *= slowMultiplier;

        var gamepad = move?.gamepad;
        gamepad?.SetMotorSpeeds(0.1f, 0.2f);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.tag.StartsWith(playerTag)) return;
        if (!affectedPlayers.Contains(other.gameObject)) return;

        affectedPlayers.Remove(other.gameObject);

        var move = other.GetComponent<PlayerMovement3D>();
        if (move != null)
            move.currentMoveSpeed /= slowMultiplier;

        var gamepad = move?.gamepad;
        gamepad?.SetMotorSpeeds(0f, 0f);
    }

    void OnDestroy()
    {
        foreach (var player in affectedPlayers)
        {
            var move = player.GetComponent<PlayerMovement3D>();
            if (move != null)
                move.currentMoveSpeed /= slowMultiplier;

            var gamepad = move?.gamepad;
            gamepad?.SetMotorSpeeds(0f, 0f);
        }

        affectedPlayers.Clear();
    }
}
