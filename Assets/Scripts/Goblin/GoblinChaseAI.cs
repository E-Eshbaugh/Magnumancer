using UnityEngine;
using UnityEngine.AI;

public class GoblinChaseNav : MonoBehaviour
{
    public float chaseRange = 50f;
    private NavMeshAgent agent;
    private Animator anim;
    private Transform target;

    private string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        FindNearestPlayer();

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
