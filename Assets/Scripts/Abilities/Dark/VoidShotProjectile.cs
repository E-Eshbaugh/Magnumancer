using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class VoidShotProjectile : MonoBehaviour
{
    public int damage = 50;
    public GameObject caster;
    [Tooltip("Seconds before the shot is cleaned up (it pierces, so it never stops on its own)")]
    public float lifetime = 3f;

    // To prevent double-hitting the same player
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Monsters (waves mode) just take the hit — they can't carry a curse
        if (other.CompareTag("Monster"))
        {
            if (alreadyHit.Add(other.gameObject) && other.TryGetComponent<GoblinHealth>(out var goblin))
                goblin.TakeDamage(damage);
            return;
        }

        if (!other.CompareTag("Player1") &&
            !other.CompareTag("Player2") &&
            !other.CompareTag("Player3") &&
            !other.CompareTag("Player4"))
            return;

        // Prevent repeat hits
        if (alreadyHit.Contains(other.gameObject) || other.transform.tag == caster.transform.tag)
            return;

        alreadyHit.Add(other.gameObject);

        // Mark and damage
        CursedPlayer curseHandler = other.GetComponent<CursedPlayer>();
        if (curseHandler != null)
        {
            curseHandler.ApplyCurse();
            curseHandler.ApplyDamage(damage);
        }

        // ⚠️ Do NOT destroy the projectile — it pierces!
    }
}
