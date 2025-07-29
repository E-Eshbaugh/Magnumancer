using UnityEngine;
using Magnumancer.Abilities;
using UnityEngine.InputSystem;
using System.Collections;

public class LightningTeleportAbility : MonoBehaviour, IActiveAbility
{
    [SerializeField] float teleportDistance = 10f;
    [SerializeField] float isoYaw = 45f;
    [SerializeField] GameObject lightningStrikeEffectPrefab;
    [SerializeField] LayerMask groundMask;
    public AudioClip thunderSound;
    public AudioSource audioSource;

    public void Activate(GameObject caster)
    {
        //damage at start
        var blast = caster.GetComponent<LightningBlastDamage>();
        if (blast != null)
        {
            blast.TriggerBlast(caster.transform.position, caster);
        }
        Debug.Log("LightningTeleport activated!");
        var playerInput = caster.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            foreach (var device in playerInput.devices)
            {
                if (device is Gamepad gamepad)
                {
                    gamepad.SetMotorSpeeds(0.6f, 1.0f); // ⚡ Light + heavy motor
                    caster.GetComponent<MonoBehaviour>().StartCoroutine(StopRumble(gamepad));
                    break; // only trigger the first matching gamepad
                }
            }
        }

        audioSource = caster.GetComponent<AudioSource>();
        if (audioSource && thunderSound)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(thunderSound);
        }
        else if (!audioSource)
        {
            Debug.LogWarning("LightningTeleport: No AudioSource found on caster.");
            if (!thunderSound)
                Debug.LogWarning("LightningTeleport: Thunder sound will not play because AudioClip is missing.");
        }

        // 🦶 Grab the feet transform from the caster
        Transform feet = caster.transform.Find("PlayerFeetPos");
        Vector3 feetStart = feet ? feet.position : caster.transform.position;
        Debug.Log("Player position start: " + caster.transform.position);
        Debug.Log("Feet position start: " + feetStart);

        // 🎮 Read stick input
        Vector2 stick = Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;
        Vector3 dir3 = new Vector3(stick.x, 0f, stick.y);

        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = caster.transform.forward;
        else
        {
            if (Mathf.Abs(isoYaw) > 0.01f)
                dir3 = Quaternion.Euler(0f, isoYaw, 0f) * dir3;
            dir3.Normalize();
        }

        caster.transform.forward = dir3;

        // 🎯 Predict teleport destination and raycast ground
        Vector3 rawTarget = feetStart + dir3 * teleportDistance;
        Vector3 groundCheckOrigin = rawTarget + Vector3.up * 10f;
        Vector3 feetEnd = rawTarget;

        if (Physics.Raycast(groundCheckOrigin, Vector3.down, out RaycastHit hit, 20f, groundMask))
            feetEnd = hit.point;

        // ⚡ Strike at feetStart
        if (lightningStrikeEffectPrefab)
            Instantiate(lightningStrikeEffectPrefab, feetStart, Quaternion.identity);

        // 🧍 Move player
        CharacterController cc = caster.GetComponent<CharacterController>();
        if (cc)
        {
            cc.enabled = false;
            caster.transform.position = feetEnd;
            cc.enabled = true;
        }
        else
            caster.transform.position = feetEnd;

        // ✅ Strike at feetEnd directly
        if (lightningStrikeEffectPrefab)
            Instantiate(lightningStrikeEffectPrefab, feetEnd, Quaternion.identity);

        Debug.Log("Player position end: " + caster.transform.position);
        Debug.Log("Feet position end: " + feetEnd);

        //damage at end part
        if (blast != null)
        {
            blast.TriggerBlast(caster.transform.position, caster);
        }

    }

    IEnumerator StopRumble(Gamepad gamepad)
    {
        yield return new WaitForSeconds(0.2f);
        if (gamepad != null)
            gamepad.SetMotorSpeeds(0f, 0f);
    }

}

