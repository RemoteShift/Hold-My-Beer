using System.Collections.Generic;
using UnityEngine;

/// <summary>A slow brawler. His raised fist and locked facing survive a beer parry.</summary>
public class HaymakerEnemy : EnemyBrain
{
    public float attackRange = 2.8f;
    public float swingRadius = 3.3f;
    [Range(1f, 360f)] public float swingArc = 150f;
    [Min(0.01f)] public float windupSeconds = 1.2f;
    [Min(0.01f)] public float swingSeconds = 0.5f;
    public Transform swingPivot;

    public override void ReturnToDefaultState() => SwitchState(new HaymakerState(this));

    /// <summary>True if a swing from where he's facing now would reach the other enemy.</summary>
    public bool IsInSwing(EnemyBrain other)
    {
        if (!other || other == this || other.IsDead) return false;

        var body = other.GetComponent<Collider>();
        if (!body) return false;

        var center = body.bounds.center;
        var reach = body.ClosestPoint(transform.position) - transform.position;
        reach.y = 0f;
        if (reach.magnitude > swingRadius) return false;

        var offset = center - transform.position;
        offset.y = 0f;
        if (Mathf.Abs(Vector3.SignedAngle(transform.forward, offset, Vector3.up)) > swingArc * 0.5f) return false;

        return CombatQuery.IsClear(transform.position, center, transform, other.transform);
    }

    private sealed class HaymakerState : EnemyState
    {
        private readonly HaymakerEnemy brawler;
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private int phase;
        private float elapsed;
        private bool aimLocked;
        public override bool IsAttacking => phase == 1 || phase == 2;
        public HaymakerState(HaymakerEnemy enemy) : base(enemy) { brawler = enemy; }
        public override void PrepareToResume() { aimLocked = true; }
        public override void Enter()
        {
            brawler.SetBodyColor(phase == 0 ? new Color(0.6f, 0.25f, 0.12f) : Color.yellow);
        }
        public override void Update()
        {
            if (phase == 0)
            {
                if (!brawler.CanSeeTarget()) { brawler.StopPathfinding(); return; }
                brawler.LookAtTarget();
                if (Vector3.Distance(brawler.transform.position, brawler.target.position) > brawler.attackRange)
                { brawler.PathfindToTarget(); return; }
                brawler.StopPathfinding();
                phase = 1;
                brawler.SetBodyColor(Color.yellow);
            }
            elapsed += phase == 1 ? brawler.WindupDeltaTime :
                phase == 2 ? brawler.AttackDeltaTime : brawler.RecoveryDeltaTime;
            if (phase == 1)
            {
                if (!aimLocked) brawler.LookAtTarget();
                Pose(-brawler.swingArc * 0.5f);
                if (elapsed < brawler.windupSeconds) return;
                phase = 2; elapsed = 0f;
                brawler.SetBodyColor(new Color(1f, 0.3f, 0.1f));
            }
            if (phase == 2)
            {
                var progress = Mathf.Clamp01(elapsed / brawler.swingSeconds);
                Pose(Mathf.Lerp(-brawler.swingArc * 0.5f, brawler.swingArc * 0.5f, progress));
                // Sweeping cone grows with the visible fist, instead of hitting the whole arc at once.
                var angle = -brawler.swingArc * 0.5f + brawler.swingArc * progress;
                foreach (var c in Physics.OverlapSphere(brawler.transform.position, brawler.swingRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var d = c.GetComponentInParent<IDamageable>();
                    if (d == null || ReferenceEquals(d, brawler) || hitTargets.Contains(d)) continue;
                    var offset = c.bounds.center - brawler.transform.position; offset.y = 0f;
                    var bearing = Vector3.SignedAngle(brawler.transform.forward, offset, Vector3.up);
                    if (bearing < -brawler.swingArc * 0.5f || bearing > angle) continue;
                    if (!CombatQuery.IsClear(brawler.transform.position, c.bounds.center, brawler.transform, c.transform)) continue;
                    hitTargets.Add(d); d.TakeDamage(brawler.EffectiveDamage);
                    if (brawler.IsDead) return;
                }
                if (progress < 1f) return;
                phase = 3; elapsed = 0f;
                brawler.SetBodyColor(Color.gray);
            }
            if (phase == 3 && elapsed >= brawler.attackCooldown) brawler.ReturnToDefaultState();
        }
        private void Pose(float angle)
        {
            if (brawler.swingPivot) brawler.swingPivot.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
