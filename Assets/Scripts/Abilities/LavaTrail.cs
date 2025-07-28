using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class LavaTrail : MonoBehaviour
{
    public int damage = 1;
    public float tickRate = 1f;
    public float lifetime = 10f;
    public LayerMask enemyLayer;
    HashSet<GameObject> damagedEnemies = new();

    void Start()
    {
        Destroy(gameObject, lifetime); // Optional safety cleanup
        StartCoroutine(DamageOverTime());
    }

    IEnumerator DamageOverTime()
    {
        while (true)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 1f, enemyLayer);
            foreach (var hit in hits)
            {
                var enemyHealth = hit.GetComponent<PlayerHealthControl>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                }
            }
            yield return new WaitForSeconds(tickRate);
        }
    }
}
