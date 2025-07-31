using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class VoidShotProjectile : MonoBehaviour
{
    public int damage = 50;
    public GameObject caster;

    // To prevent double-hitting the same player
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
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
