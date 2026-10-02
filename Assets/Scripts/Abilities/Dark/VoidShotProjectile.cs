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
        var target = DamageEvents.RootOf(other);
        if (!DamageEvents.IsEnemy(target, caster) || !DamageEvents.IsAlive(target) || !alreadyHit.Add(target)) return;

        // Mark before damage: a lethal beam hit must still trigger Soulfracture.
        StatusEffects.Of(target).MarkVoid(caster);
        target.GetComponent<CursedPlayer>()?.ApplyCurse(caster);
        DamageEvents.Deal(target, damage, caster);
        // The beam pierces combatants, hitting each root once.
    }
}
