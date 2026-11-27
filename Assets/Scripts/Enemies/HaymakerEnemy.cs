using System.Collections.Generic;
using UnityEngine;

/// <summary>A slow brawler. His raised fist and locked facing survive a beer parry.</summary>
public class HaymakerEnemy : EnemyBrain
{

    [Header("Weapon")]
    public GameObject weaponPrefab;
    [Tooltip("Mount point under the hand bone — adjust position/rotation here to tune the grip.")]
    public Transform handHoldPoint;
    [Tooltip("Swing pivot — weapon re-parents here during the attack arc.")]
    public Transform weaponHoldPoint;

    private GameObject _weaponInstance;
    // Set by HaymakerState: true only during the swing arc (phase 2), not windup.
    internal bool IsSwingActive;

    protected override void Awake()
    {
        base.Awake();
        if (weaponPrefab)
        {
            var spawnParent = handHoldPoint != null ? handHoldPoint : weaponHoldPoint;
            if (spawnParent)
            {
                _weaponInstance = Instantiate(weaponPrefab, spawnParent);
                _weaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdateWalkSpeed(moveSpeed);
    }

    private void LateUpdate()
    {
        if (_weaponInstance == null || handHoldPoint == null) return;
        if (_weaponInstance.transform.parent == handHoldPoint) return;
        _weaponInstance.transform.SetParent(handHoldPoint, false);
        _weaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var found = FindDeep(c, name);
            if (found) return found;
        }
        return null;
    }

    public float attackRange = 2.8f;
    public float swingRadius = 3.3f;
    [Range(1f, 360f)] public float swingArc = 150f;
    [Min(0.01f)] public float windupSeconds = 1.2f;
    [Min(0.01f)] public float swingSeconds = 0.5f;
    public Transform swingPivot;

    public override void ReturnToDefaultState() => SwitchState(new HaymakerState(this));

    private sealed class HaymakerState : EnemyState
    {
        private readonly HaymakerEnemy brawler;
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        // The hand finishes winding back at 40% and crosses the target by 68%.
        private const float SwingStart = 0.4f;
        private const float SwingEnd = 0.68f;
        private int phase;
        private float elapsed;
        private bool aimLocked;
        public override bool IsAttacking => phase == 1 || phase == 2;
        public HaymakerState(HaymakerEnemy enemy) : base(enemy) { brawler = enemy; }
        public override void PrepareToResume() { aimLocked = true; }
        public override void Enter()
        {
            brawler.IsSwingActive = phase == 2;
            brawler.SetBodyColor(phase == 0 ? new Color(0.6f, 0.25f, 0.12f) : Color.yellow);
            if (phase == 0) brawler.Animator?.Play("Walk", 0, 0f);
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
                brawler.PlayPose("Swing");
                brawler.SetBodyColor(Color.yellow);
            }
            elapsed += phase == 1 ? brawler.WindupDeltaTime :
                phase == 2 ? brawler.AttackDeltaTime : brawler.RecoveryDeltaTime;
            if (phase == 1)
            {
                if (!aimLocked) brawler.LookAtTarget();
                brawler.SetPoseTime(SwingStart * Mathf.Clamp01(elapsed / brawler.windupSeconds));
                Pose(-brawler.swingArc * 0.5f);
                if (elapsed < brawler.windupSeconds) return;
                phase = 2; elapsed = 0f;
                brawler.IsSwingActive = true;
                brawler.SetBodyColor(new Color(1f, 0.3f, 0.1f));
            }
            if (phase == 2)
            {
                var progress = Mathf.Clamp01(elapsed / brawler.swingSeconds);
                brawler.SetPoseTime(Mathf.Lerp(SwingStart, SwingEnd, progress));
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
                brawler.IsSwingActive = false;
                brawler.SetBodyColor(Color.gray);
            }
            if (phase == 3)
            {
                brawler.SetPoseTime(Mathf.Lerp(SwingEnd, 1f, elapsed / Mathf.Max(0.001f, brawler.attackCooldown)));
                if (elapsed >= brawler.attackCooldown) brawler.ReturnToDefaultState();
            }
        }
        private void Pose(float angle)
        {
            if (brawler.swingPivot) brawler.swingPivot.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
