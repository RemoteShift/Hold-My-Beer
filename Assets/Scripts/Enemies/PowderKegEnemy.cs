using System.Collections.Generic;
using UnityEngine;

public class PowderKegEnemy : EnemyBrain
{
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

    public override void ReturnToDefaultState() => SwitchState(new FuseState(this));

    public override void Die() => Explode();

    public void Explode()
    {
        if (exploded || IsDead) return;
        exploded = true;
        var blastDamage = EffectiveDamage;
        // Mark dead before damaging neighbours: chain reactions must not re-enter this explosion.
        base.Die();
        CombatQuery.DamageArea(this, transform.position, explosionRadius, blastDamage,
            new HashSet<IDamageable>(), 360f, 2f);
        var pushedBodies = new HashSet<Rigidbody>();
        foreach (var c in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            var body = c.attachedRigidbody;
            if (!body || body.isKinematic || !pushedBodies.Add(body)) continue;
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
        // Brief expanding flash remains after the patron is destroyed.
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Powder Keg blast";
        flash.transform.position = transform.position;
        flash.transform.localScale = Vector3.one * explosionRadius * 2f;
        flash.GetComponent<Collider>().enabled = false;
        var renderer = flash.GetComponent<Renderer>();
        renderer.material.color = new Color(1f, 0.4f, 0.05f);
        Destroy(flash, 0.12f);
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
            if (!keg.IsLit)
            {
                if (!keg.CanSeeTarget()) return;
                keg.IsLit = true; keg.FuseRemaining = keg.fuseSeconds;
            }
            // Real seconds, unaffected by music. Only this state burns the fuse; beer suspends it.
            keg.FuseRemaining = Mathf.Max(0f, keg.FuseRemaining - Time.deltaTime);
            if (keg.fuseVisual)
                keg.fuseVisual.localScale = new Vector3(1f, Mathf.Max(0.02f, keg.FuseRemaining / keg.fuseSeconds), 1f);
            if (keg.fuseLight) keg.fuseLight.intensity = 1f + Mathf.PingPong(keg.FuseRemaining * 12f, 3f);
            if (keg.FuseRemaining <= 0f) { keg.Explode(); return; }
            keg.LookAtTarget(); keg.PathfindToTarget();
        }
    }
}
