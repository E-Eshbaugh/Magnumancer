using UnityEngine;
using UnityEngine.AI;

public class GoblinChaseNav : MonoBehaviour
{
    public float chaseRange = 50f;
    private NavMeshAgent agent;
    private Animator anim;
    private Transform target;

    [Tooltip("Seconds between nearest-player searches (tag lookups are slow with many goblins)")]
    public float retargetInterval = 0.25f;
    private float retargetTimer;

    private string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        retargetTimer = Random.Range(0f, retargetInterval); // spread searches across frames
    }

    void Update()
    {
        retargetTimer -= Time.deltaTime;
        // Retarget on the timer, or right away if our current target just died
        if (retargetTimer <= 0f || (target != null && !target.gameObject.activeInHierarchy))
        {
            retargetTimer = retargetInterval;
            FindNearestPlayer();
        }

        if (target != null)
        {
            agent.SetDestination(target.position);

            if (anim)
                anim.SetFloat("Speed", agent.velocity.magnitude);
        }
        else
        {
            if (anim)
                anim.SetFloat("Speed", 0f);
        }
    }

    void FindNearestPlayer()
    {
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (string tag in playerTags)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(tag);

            foreach (GameObject player in players)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < closestDist && dist < chaseRange)
                {
                    closestDist = dist;
                    closest = player.transform;
                }
            }
        }

        target = closest;
    }
}
