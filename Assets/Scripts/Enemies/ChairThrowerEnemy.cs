using UnityEngine;
using UnityEngine.AI;

public class ChairThrowerEnemy : EnemyBrain
{
    public Transform chairHoldPoint;
    public ThrowableFurniture startingChair;
    [Tooltip("Search radius: how far away furniture can be noticed and approached.")]
    [Min(0f)] public float pickupRadius = 15f;
    [Tooltip("Grab reach in metres from the enemy centre to the nearest furniture collider surface, not the model pivot.")]
    [Min(0f)] public float pickupReach = 2f;
    [Tooltip("Distance around the current position to choose roaming destinations while searching for furniture.")]
    [Min(1f)] public float roamRadius = 10f;
    public float throwRange = 14f;
    [Min(0.1f)] public float chargeSeconds = 1.5f;
    [Min(0.1f)] public float flightSeconds = 0.8f;
    public ThrowableFurniture HeldChair { get; private set; }
    /// <summary>The current attack aim, retained while beered for an accurate trajectory gizmo.</summary>
    public Vector3 ThrowAimOffset { get; private set; }

    protected override void Start()
    {
        if (startingChair && startingChair.TryHold(this, chairHoldPoint)) HeldChair = startingChair;
        base.Start();
    }
    public override void ReturnToDefaultState() => SwitchState(new ThrowState(this));
    public override void Die()
    {
        DropHeldChair();
        base.Die();
    }

    public override void Despawn()
    {
        DropHeldChair();
        base.Despawn();
    }

    private void DropHeldChair()
    {
        if (HeldChair) HeldChair.Release(Vector3.zero, EffectiveDamage);
        HeldChair = null;
    }
    private sealed class ThrowState : EnemyState
    {
        private readonly ChairThrowerEnemy thrower;
        private int phase;
        private float elapsed;
        private bool aimLocked;
        private float normalStoppingDistance;
        private bool entered;
        private readonly NavMeshPath pickupPath = new NavMeshPath();
        private float nextPickupRepath;
        private Vector3 lastPickupGoal;
        private bool roaming;
        private float nextRoamAttempt;
        private float roamDeadline;
        private readonly NavMeshPath roamPath = new NavMeshPath();
        public override bool IsAttacking => phase == 1;
        public ThrowState(ChairThrowerEnemy enemy) : base(enemy) { thrower = enemy; }
        public override void PrepareToResume() { aimLocked = true; }
        public override void Enter()
        {
            if (!entered) { normalStoppingDistance = thrower.agent.stoppingDistance; entered = true; }
            thrower.SetBodyColor(phase == 1 ? Color.yellow : new Color(0.3f, 0.5f, 0.7f));
        }
        public override void Exit() { RestoreStoppingDistance(); }
        private void RestoreStoppingDistance() { thrower.agent.stoppingDistance = normalStoppingDistance; }

        private bool CanGrab(ThrowableFurniture chair, Vector3 position)
        {
            var point = chair.GetPickupPoint(position);
            return (point - position).sqrMagnitude <= thrower.pickupReach * thrower.pickupReach &&
                CombatQuery.IsClear(position, point, thrower.transform, chair.transform);
        }

        private bool TryGetPickupGoal(ThrowableFurniture chair, out Vector3 goal)
        {
            goal = default;
            if (!thrower.CanNavigate || thrower.IsBeingPushed) return false;
            var point = chair.GetPickupPoint(thrower.transform.position);
            if (!NavMesh.SamplePosition(point, out var hit, Mathf.Max(thrower.pickupReach, thrower.agent.height), thrower.agent.areaMask)) return false;
            if (!thrower.agent.CalculatePath(hit.position, pickupPath) || pickupPath.status == NavMeshPathStatus.PathInvalid) return false;
            var corners = pickupPath.corners;
            if (corners.Length == 0) return false;
            goal = corners[corners.Length - 1];
            // Reject props whose reachable path endpoint still cannot get within grab reach.
            // This also avoids camping at a wall or an unreachable piece of furniture.
            return CanGrab(chair, goal + Vector3.up * thrower.agent.baseOffset);
        }
        private void Roam()
        {
            if (!thrower.CanNavigate || thrower.IsBeingPushed) return;
            var agent = thrower.agent;
            agent.stoppingDistance = 0f;
            agent.isStopped = false;

            // Keep travelling while scanning for furniture; replace finished or stuck routes.
            if (roaming && Time.time < roamDeadline &&
                (agent.pathPending || (agent.hasPath && !agent.isPathStale &&
                    agent.pathStatus == NavMeshPathStatus.PathComplete && agent.remainingDistance > 0.5f)))
            {
                var direction = agent.desiredVelocity;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                    thrower.transform.rotation = Quaternion.Slerp(thrower.transform.rotation,
                        Quaternion.LookRotation(direction), Time.deltaTime * thrower.turnSpeed * thrower.Buffs.turnSpeed);
                return;
            }

            if (Time.time < nextRoamAttempt) return;
            thrower.StopPathfinding();
            roaming = false;
            nextRoamAttempt = Time.time + 1f;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var offset = Random.insideUnitCircle * Mathf.Max(1f, thrower.roamRadius);
                var candidate = thrower.transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, agent.areaMask)) continue;
                if ((hit.position - agent.nextPosition).sqrMagnitude < 1f) continue;
                if (!agent.CalculatePath(hit.position, roamPath) || roamPath.status != NavMeshPathStatus.PathComplete) continue;
                if (!agent.SetPath(roamPath)) continue;
                agent.isStopped = false;
                roaming = true;
                roamDeadline = Time.time + 15f;
                break;
            }
        }

        public override void Update()
        {
            if (phase == 2)
            {
                elapsed += thrower.RecoveryDeltaTime;
                if (elapsed >= thrower.attackCooldown) thrower.ReturnToDefaultState();
                return;
            }
            if (!thrower.HeldChair)
            {
                phase = 0;
                ThrowableFurniture nearest = null;
                var distance = thrower.pickupRadius;
                var goal = Vector3.zero;
                foreach (var chair in Object.FindObjectsByType<ThrowableFurniture>(FindObjectsSortMode.None))
                {
                    if (!chair.IsAvailableForPickup) continue;
                    var point = chair.GetPickupPoint(thrower.transform.position);
                    var d = Vector3.Distance(thrower.transform.position, point);
                    if (d > distance) continue;
                    var candidateGoal = Vector3.zero;
                    if (!CanGrab(chair, thrower.transform.position) && !TryGetPickupGoal(chair, out candidateGoal)) continue;
                    distance = d; nearest = chair; goal = candidateGoal;
                }
                if (!nearest) { Roam(); return; }
                if (roaming)
                {
                    roaming = false;
                    thrower.StopPathfinding();
                    nextPickupRepath = 0f;
                }
                if (CanGrab(nearest, thrower.transform.position))
                {
                    if (nearest.TryHold(thrower, thrower.chairHoldPoint))
                    {
                        thrower.HeldChair = nearest;
                        thrower.StopPathfinding();
                        RestoreStoppingDistance();
                    }
                }
                else if (thrower.CanNavigate && !thrower.IsBeingPushed)
                {
                    // Pickup uses 3D surface reach; the normal combat stopping distance may
                    // stop outside that reach, especially for low stools or off-centre pivots.
                    thrower.agent.stoppingDistance = 0f;
                    thrower.agent.isStopped = false;
                    if (Time.time >= nextPickupRepath || (goal - lastPickupGoal).sqrMagnitude > 0.0625f)
                    {
                        nextPickupRepath = Time.time + 0.15f;
                        lastPickupGoal = goal;
                        thrower.agent.SetDestination(goal);
                    }
                }
                return;
            }
            if (phase == 0)
            {
                if (!thrower.CanSeeTarget()) { thrower.StopPathfinding(); return; }
                thrower.LookAtTarget();
                if (Vector3.Distance(thrower.transform.position, thrower.target.position) > thrower.throwRange ||
                    !CombatQuery.IsClear(thrower.transform.position, thrower.target.position, thrower.transform, thrower.target))
                { thrower.PathfindToTarget(); return; }
                thrower.StopPathfinding(); phase = 1; elapsed = 0f;
                thrower.SetBodyColor(Color.yellow);
            }
            if (!aimLocked && thrower.target)
            {
                thrower.LookAtTarget();
                thrower.ThrowAimOffset = thrower.target.position - thrower.chairHoldPoint.position;
            }
            elapsed += thrower.WindupDeltaTime;
            if (elapsed < thrower.chargeSeconds) return;
            var duration = Mathf.Max(0.1f, thrower.flightSeconds) / thrower.Buffs.attackSpeed;
            // Aim is an offset: shoving a frozen thrower translates the locked trajectory.
            var velocity = thrower.ThrowAimOffset / duration - Physics.gravity * (duration * 0.5f);
            thrower.HeldChair.Release(velocity, thrower.EffectiveDamage);
            thrower.HeldChair = null; phase = 2; elapsed = 0f;
            thrower.SetBodyColor(Color.gray);
        }
    }
}
