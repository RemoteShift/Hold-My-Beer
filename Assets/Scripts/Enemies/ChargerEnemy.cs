using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baseline enemy: chases the player, winds up, then charges in a straight line.
/// The charge hurts anything damageable in its way, including other enemies.
/// </summary>
public class ChargerEnemy : EnemyBrain
{
    [Header("Charge Attack")]
    [Tooltip("Keep this within the player's hand range, or the wind-up can't be parried.")]
    public float attackRange = 3.5f;
    public float windupDuration = 0.8f;
    public float chargeSpeed = 14f;
    public float chargeDistance = 10f;
    public float damage = 20f;
    [Tooltip("Damage the charger takes for running into a wall or fixed obstacle. Pushable furniture does no damage.")]
    public float wallSlamDamage = 20f;

    [Header("Charge Hit Detection")]
    [SerializeField] private float hitRadius = 0.9f;
    [SerializeField] private float hitForwardOffset = 0.6f;
    [SerializeField] private float pushableCheckRadius = 0.35f;
    [SerializeField] private float pushableCheckForwardOffset = 0.65f;
    [SerializeField] private float pushableCheckHeight = 1.5f;

    [Header("Placeholder Colors")]
    public Color chaseColor = new Color(0.75f, 0.35f, 0.2f);
    public Color windupColor = new Color(1f, 0.8f, 0.1f);
    public Color chargeColor = new Color(1f, 0.25f, 0.1f);
    public Color recoverColor = new Color(0.45f, 0.45f, 0.5f);

    private static readonly Collider[] HitBuffer = new Collider[16];

    public override void ReturnToDefaultState()
    {
        SwitchState(new ChargerChaseState(this));
    }

    /// <summary>True when the player is close enough and nothing blocks a straight run at them.</summary>
    public bool CanStartCharge()
    {
        if (!target) return false;

        var toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > attackRange * attackRange) return false;

        return !agent.Raycast(target.position, out _);
    }

    /// <summary>Damages everything in front of the charger that has not already been hit.</summary>
    /// <returns>True if at least one new target was hit.</returns>
    public bool HitTargetsInFront(HashSet<IDamageable> alreadyHit)
    {
        var center = transform.position + transform.forward * hitForwardOffset;
        var count = Physics.OverlapSphereNonAlloc(center, hitRadius, HitBuffer, ~0, QueryTriggerInteraction.Ignore);

        var hitSomething = false;

        for (var i = 0; i < count; i++)
        {
            var damageable = HitBuffer[i].GetComponentInParent<IDamageable>();

            if (damageable == null || ReferenceEquals(damageable, this)) continue;
            if (!alreadyHit.Add(damageable)) continue;

            damageable.TakeDamage(damage);
            hitSomething = true;
        }

        return hitSomething;
    }

    /// <summary>True if the thing the charger just ran into is pushable furniture rather than something fixed.</summary>
    public bool IsPushableAhead()
    {
        var feet = transform.position - Vector3.up * agent.baseOffset;
        var bottom = feet + transform.forward * pushableCheckForwardOffset + Vector3.up * pushableCheckRadius;
        var top = bottom + Vector3.up * pushableCheckHeight;

        var count = Physics.OverlapCapsuleNonAlloc(bottom, top, pushableCheckRadius, HitBuffer, ~0,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < count; i++)
        {
            if (HitBuffer[i].GetComponentInParent<Pushable>()) return true;
        }

        return false;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * hitForwardOffset, hitRadius);
    }
}
