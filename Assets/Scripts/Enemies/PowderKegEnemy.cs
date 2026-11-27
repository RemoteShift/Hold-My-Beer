using System.Collections.Generic;
using UnityEngine;

public class PowderKegEnemy : EnemyBrain
{
    [Tooltip("Approach without lighting the fuse until this close to the player. Keep within the player's beer reach.")]
    [Min(0.1f)] public float fuseStartRange = 2.5f;
    [Min(0.1f)] public float fuseSeconds = 4f;
    [Min(0.01f)] public float explosionRadius = 5f;
    [Tooltip("Velocity added to loose furniture at the blast centre, in metres/second. Independent of furniture mass.")]
    [Min(0f)] public float furnitureBlastSpeed = 14f;
    [Tooltip("Upward bias so furniture lifts away from the floor.")]
    [Min(0f)] public float furnitureBlastLift = 0.6f;
    [Tooltip("1 = linear distance falloff. Higher values concentrate the blast near its centre.")]
    [Min(0.1f)] public float furnitureBlastFalloff = 1f;
    public Transform fuseVisual;
    public Light fuseLight;
    public float FuseRemaining { get; private set; }
    public bool IsLit { get; private set; }
    private bool exploded;
    private TMPro.TextMeshPro fuseStatus;

    protected override void Awake()
    {
        base.Awake();
        var label = new GameObject("Keg fuse status");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = Vector3.up * 2.1f;
        fuseStatus = label.AddComponent<TMPro.TextMeshPro>();
        fuseStatus.fontSize = 3f;
        fuseStatus.alignment = TMPro.TextAlignmentOptions.Center;
        fuseStatus.rectTransform.sizeDelta = new Vector2(4.5f, 1.2f);
    }

    private void LateUpdate()
    {
        if (!fuseStatus) return;
        var camera = Camera.main;
        if (camera) fuseStatus.transform.rotation = camera.transform.rotation;
        if (IsDead || IsLeaving) { fuseStatus.text = ""; return; }
        if (IsBeered)
        {
            fuseStatus.color = new Color(.4f, .8f, 1f);
            fuseStatus.text = $"FUSE PAUSED  {FuseRemaining:0.0}s";
        }
        else if (!IsLit)
        {
            fuseStatus.color = Color.gray;
            fuseStatus.text = "FUSE UNLIT";
        }
        else
        {
            var urgent = FuseRemaining <= 1.5f;
            fuseStatus.color = urgent ? Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 6f, 1f)) : Color.yellow;
            fuseStatus.text = (CanBeBeered ? "[LMB] BEER ME" : "FUSE BURNING") +
                $"\n{(urgent ? "EXPLODING" : "EXPLODES IN")}  {FuseRemaining:0.0}s";
        }
    }


    public override void ReturnToDefaultState() => SwitchState(new FuseState(this));

    public override void Die() => Explode();

    public void Explode()
    {
        if (exploded || IsDead) return;
        // Beer pauses the fuse, but lethal damage and chain reactions still detonate the keg.
        exploded = true;
        CombatVfx.Explosion(this, explosionRadius);
        var blastDamage = EffectiveDamage;
        // Mark dead before damaging neighbours: chain reactions must not re-enter this explosion.
        base.Die();
        CombatQuery.DamageArea(this, transform.position, explosionRadius, blastDamage,
            new HashSet<IDamageable>(), 360f, 20f);
        var pushedBodies = new HashSet<Rigidbody>();
        foreach (var c in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            var body = c.attachedRigidbody;
            if (!body || !pushedBodies.Add(body)) continue;
            var pushable = body.GetComponent<Pushable>();
            if (pushable && !body.GetComponent<ThrowableFurniture>().IsHeld) pushable.ReleaseToPhysics();
            if (body.isKinematic) continue;
            if (body.GetComponentInParent<EnemyBrain>() || body.GetComponentInParent<PlayerHealth>()) continue;
            var offset = body.worldCenterOfMass - transform.position;
            var falloff = Mathf.Pow(Mathf.Clamp01(1f - offset.magnitude / Mathf.Max(0.01f, explosionRadius)), furnitureBlastFalloff);
            offset.y = 0f;
            var direction = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.up;
            direction = (direction + Vector3.up * furnitureBlastLift).normalized;
            var velocityChange = direction * (furnitureBlastSpeed * falloff);
            var furniture = body.GetComponent<ThrowableFurniture>();
            if (furniture) furniture.ApplyBlast(velocityChange);
            else { body.WakeUp(); body.AddForce(velocityChange, ForceMode.VelocityChange); }
        }
        // The pack explosion spawned above outlives this enemy.
    }

    protected override void Update()
    {
        if (PlayerParryFeedback.IsPlayerFrozen) return;
        var wasBeered = IsBeered;
        base.Update();
        if (!IsBeered && CurrentState is FuseState)
        {
            Animator?.SetBool("IsMoving", IsLit && NavigationSpeed > 0.05f);
            UpdateWalkSpeed(moveSpeed);
        }
        // Do not charge an entire frame against the fuse when beer expires mid-update.
        if (wasBeered) return;
        if (!IsLit || IsBeered || IsDead) return;
        // Stuns interrupt movement, but only beer pauses an already lit fuse.
        FuseRemaining = Mathf.Max(0f, FuseRemaining - Time.deltaTime);
        if (fuseVisual)
            fuseVisual.localScale = new Vector3(1f, Mathf.Max(0.02f, FuseRemaining / fuseSeconds), 1f);
        if (fuseLight) fuseLight.intensity = 1f + Mathf.PingPong(FuseRemaining * 12f, 3f);
        if (FuseRemaining <= 0f) { Explode(); return; }
    }

    private sealed class FuseState : EnemyState
    {
        private readonly PowderKegEnemy keg;
        public override bool IsAttacking => keg.IsLit;
        public FuseState(PowderKegEnemy enemy) : base(enemy) { keg = enemy; }
        public override void Enter()
        {
            keg.SetBodyColor(new Color(0.8f, 0.15f, 0.1f));
        }
        public override void Update()
        {
            if (!keg.target) { keg.StopPathfinding(); return; }
            if (!keg.IsLit)
            {
                if (!keg.CanSeeTarget()) return;
                // Detection starts the approach, not the countdown. The player must have
                // a chance to hand over a beer before the fuse starts burning.
                keg.LookAtTarget();
                keg.PathfindToTarget();
                if (Vector3.Distance(keg.transform.position, keg.target.position) > keg.fuseStartRange) return;
                keg.IsLit = true; keg.FuseRemaining = keg.fuseSeconds;
            }
            keg.LookAtTarget(); keg.PathfindToTarget();
        }
    }
}
