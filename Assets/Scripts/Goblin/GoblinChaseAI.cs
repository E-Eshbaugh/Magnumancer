using UnityEngine;
using UnityEngine.AI;

public class GoblinChaseNav : MonoBehaviour
{
    public float chaseRange = 50f;
    private NavMeshAgent agent;
    private Animator anim;
    private GoblinHealth health;
    private Transform target;
    private GoblinTraversal traversal;
    float repathTimer;
    Vector3 lastDestination;

    [Header("Navigation")]
    [Min(0.05f)] public float repathInterval = 0.2f;

    [Tooltip("Seconds between nearest-player searches (tag lookups are slow with many goblins)")]
    public float retargetInterval = 0.25f;
    private float retargetTimer;

    [Header("Melee")]
    [Min(0f)] public float attackDamage = 12f;
    [Tooltip("Maximum horizontal distance between feet at the start and impact")]
    [Min(0.1f)] public float attackRange = 1.8f;
    [Tooltip("Step away during this warning to dodge the committed swing")]
    [Min(0.05f)] public float attackWindup = 0.45f;
    [Min(0.05f)] public float attackRecovery = 0.9f;
    [Tooltip("Flat-ground height limit; grounded targets on connected stairs use slope-aware reach")]
    [Min(0f)] public float attackHeight = 1.1f;
    [Range(10f, 180f)] public float attackArc = 100f;

    enum AttackPhase { Chasing, Windup, Recovery }
    AttackPhase phase;
    Transform strikeTarget;
    Vector3 strikeDirection;
    float phaseEnds;
    GameObject warning;
    LineRenderer edge, closing;
    bool hasSpeed;
    static readonly Color WarningColor = new Color(1f, 0.3f, 0.12f);
    private readonly string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    public bool IsWindingUp => phase == AttackPhase.Windup;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<GoblinHealth>();
        anim = GetComponentInChildren<Animator>();
        traversal = GetComponent<GoblinTraversal>();
        if (traversal == null) traversal = gameObject.AddComponent<GoblinTraversal>();
        if (agent != null)
        {
            // Melee owns stopping. Braking on each moving destination causes the
            // stop/start shuffle, especially when the target is on another step.
            agent.autoBraking = false;
            agent.stoppingDistance = 0.1f;
            agent.acceleration = Mathf.Max(agent.acceleration, 24f);
            agent.angularSpeed = 540f;
            agent.avoidancePriority = 35 + Mathf.Abs(GetEntityId().GetHashCode() % 30);
        }
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var parameter in anim.parameters)
                if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) hasSpeed = true;
    }

    void OnEnable() => retargetTimer = repathTimer = 0f;

    bool Interrupted
    {
        get
        {
            var fx = GetComponent<StatusEffects>();
            return fx != null && (fx.StunRemaining > 0f || fx.IsRooted || fx.IsFrozen || fx.IsKnockedBack);
        }
    }

    void Update()
    {
        if (health == null || health.IsDead || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            if (traversal != null) traversal.CancelJump();
            CancelAttack();
            return;
        }

        if (Interrupted)
        {
            traversal.CancelJump();
            if (phase == AttackPhase.Windup) Recover();
            if (agent.hasPath) agent.ResetPath();
            SetSpeed(0f);
            return;
        }

        if (traversal.IsJumping)
        {
            traversal.TickJump();
            SetSpeed(0f);
            repathTimer = 0f;
            return;
        }

        // Keep navigation's next target current even while a swing is committed.
        // strikeTarget stays locked so a vanished decoy cannot redirect its hit.
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f || !CanTarget(target))
        {
            retargetTimer = retargetInterval;
            FindNearestPlayer();
        }

        if (phase == AttackPhase.Windup)
        {
            SetSpeed(0f);
            if (!CanTarget(strikeTarget)) { Recover(); return; }
            DrawWarning();
            if (Time.time >= phaseEnds)
            {
                Strike();
                Recover();
            }
            return;
        }
        if (phase == AttackPhase.Recovery)
        {
            SetSpeed(0f);
            if (Time.time < phaseEnds) return;
            phase = AttackPhase.Chasing;
            retargetTimer = 0f;
        }

        if (target == null)
        {
            if (agent.hasPath) agent.ResetPath();
            SetSpeed(0f);
            return;
        }

        if (InReach(target) && AbilityKit.ClearPath(AbilityKit.Chest(gameObject), target.gameObject, gameObject))
        {
            BeginAttack();
            return;
        }
        if (traversal.TryJump(target.position))
        {
            SetSpeed(0f);
            return;
        }
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f && !agent.pathPending)
        {
            repathTimer = Mathf.Max(0.05f, repathInterval);
            // Follow the ground under a jumping wizard. Keep the last good path
            // when there is no nearby navigation destination.
            if (traversal.TryDestination(target.position, out var destination) &&
                (!agent.hasPath || agent.isPathStale || (destination - lastDestination).sqrMagnitude > 0.04f))
            {
                if (agent.SetDestination(destination)) lastDestination = destination;
            }
        }
        Vector3 velocity = agent.velocity; velocity.y = 0f;
        SetSpeed(velocity.magnitude);
    }

    static bool CanTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy) return false;
        var clone = candidate.GetComponent<WaterCloneDecoy>();
        if (clone != null) return clone.CanLure;
        var player = candidate.GetComponent<PlayerHealthControl>();
        return player != null && player.IsStanding;
    }

    bool InReach(Transform candidate)
    {
        Vector3 delta = candidate.position - transform.position;
        if (Mathf.Abs(delta.y) > attackHeight && !traversal.SharesWalkableSlope(candidate.position, attackRange)) return false;
        delta.y = 0f;
        return delta.sqrMagnitude <= attackRange * attackRange;
    }

    void BeginAttack()
    {
        phase = AttackPhase.Windup;
        phaseEnds = Time.time + Mathf.Max(0.05f, attackWindup);
        strikeTarget = target;
        strikeDirection = target.position - transform.position;
        strikeDirection.y = 0f;
        if (strikeDirection.sqrMagnitude < 0.001f) strikeDirection = transform.forward;
        strikeDirection.Normalize();
        transform.rotation = Quaternion.LookRotation(strikeDirection);
        // Do not own isStopped: StatusEffects owns it while a shove is active.
        agent.ResetPath();
        SetSpeed(0f);
        warning = new GameObject("GoblinAttackWarning");
        warning.transform.SetParent(transform, false);
        edge = GlowLine.Make(warning.transform, "Reach", 24, 0.07f, AbilityKit.Glow());
        closing = GlowLine.Make(warning.transform, "Windup", 24, 0.1f, AbilityKit.Glow());
        DrawWarning();
    }

    void DrawWarning()
    {
        float progress = 1f - Mathf.Clamp01((phaseEnds - Time.time) / Mathf.Max(0.05f, attackWindup));
        Vector3 center = transform.position + Vector3.up * 0.08f;
        DrawArc(edge, center, attackRange);
        DrawArc(closing, center, Mathf.Lerp(0.15f, attackRange, progress));
        GlowLine.SetColor(edge, WarningColor, 0.65f);
        GlowLine.SetColor(closing, Color.Lerp(WarningColor, Color.white, progress), 0.9f);
    }

    void DrawArc(LineRenderer line, Vector3 center, float radius)
    {
        line.SetPosition(0, center);
        for (int i = 1; i < line.positionCount - 1; i++)
        {
            float f = (i - 1f) / (line.positionCount - 3f);
            Vector3 direction = Quaternion.Euler(0f, Mathf.Lerp(-attackArc * 0.5f, attackArc * 0.5f, f), 0f) * strikeDirection;
            line.SetPosition(i, center + direction * radius);
        }
        line.SetPosition(line.positionCount - 1, center);
    }

    void Strike()
    {
        // Re-check at impact. A target can dodge, teleport or get behind new cover.
        if (!CanTarget(strikeTarget) || !InReach(strikeTarget)) return;
        Vector3 to = strikeTarget.position - transform.position; to.y = 0f;
        if (Vector3.Angle(strikeDirection, to) > attackArc * 0.5f) return;
        if (!AbilityKit.ClearPath(AbilityKit.Chest(gameObject), strikeTarget.gameObject, gameObject)) return;

        // Retaliation can kill this goblin and clear strikeTarget during Deal.
        GameObject victim = strikeTarget.gameObject;
        Vector3 hit = AbilityKit.Chest(victim);
        var clone = victim.GetComponent<WaterCloneDecoy>();
        if (clone != null) clone.HitByMonster();
        else
        {
            DamageEvents.Deal(victim, attackDamage, gameObject);
            Rumble.Play(victim, 0.25f, 0.5f, 0.12f);
        }
        PowerFx.Sparks(hit, WarningColor, 8, 3f, 0.2f, 0.06f, 1f);
    }

    void Recover()
    {
        ClearWarning();
        strikeTarget = null;
        phase = AttackPhase.Recovery;
        phaseEnds = Time.time + Mathf.Max(0.05f, attackRecovery);
    }

    void ClearWarning()
    {
        if (warning != null) Destroy(warning);
        warning = null;
        edge = closing = null;
    }

    void CancelAttack()
    {
        ClearWarning();
        strikeTarget = null;
        phase = AttackPhase.Chasing;
    }

    void OnDisable()
    {
        if (traversal != null) traversal.CancelJump();
        CancelAttack();
        target = null;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        SetSpeed(0f);
    }

    void SetSpeed(float speed)
    {
        if (anim != null && hasSpeed) anim.SetFloat("Speed", speed, 0.12f, Time.deltaTime);
    }

    void FindNearestPlayer()
    {
        float closestDist = chaseRange;
        Transform closest = null;
        foreach (string tag in playerTags)
            foreach (var player in GameObject.FindGameObjectsWithTag(tag))
                Consider(player.transform, ref closest, ref closestDist);
        foreach (var clone in WaterCloneDecoy.Active)
            if (clone != null) Consider(clone.transform, ref closest, ref closestDist);
        target = closest;
    }

    void Consider(Transform candidate, ref Transform closest, ref float closestDist)
    {
        if (!CanTarget(candidate)) return;
        float distance = Vector3.Distance(transform.position, candidate.position);
        if (distance >= closestDist) return;
        closestDist = distance;
        closest = candidate;
    }
}
