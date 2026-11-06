using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Straight-line run along the direction the charger faced when it began.
/// Direction, distance left and targets hit live on the state so the charge can resume where it stopped.
/// </summary>
public class ChargerChargeState : EnemyState
{
    private const float BlockedDistanceRatio = 0.25f;

    private readonly ChargerEnemy charger;
    private readonly Vector3 direction;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    private float distanceLeft;

    public override bool IsAttacking => true;

    public ChargerChargeState(ChargerEnemy charger) : base(charger)
    {
        this.charger = charger;
        direction = charger.transform.forward;
        distanceLeft = charger.chargeDistance;
    }

    public override void Enter()
    {
        charger.StopPathfinding();
        charger.SetBodyColor(charger.chargeColor);
    }

    // Skips the base look-at on purpose: a charge never steers.
    public override void Update()
    {
        if (!charger.CanNavigate || charger.IsBeingPushed) return;

        var step = charger.chargeSpeed * charger.AttackDeltaTime;
        var before = charger.transform.position;

        // Checked before moving: agent.Move would otherwise slide along an angled wall
        var hitObstacle = charger.agent.Raycast(before + direction * step, out _);

        if (!hitObstacle) charger.agent.Move(direction * step);

        var moved = Vector3.Distance(before, charger.transform.position);
        distanceLeft -= moved;

        if (charger.HitTargetsInFront(alreadyHit))
        {
            charger.SwitchState(new ChargerRecoverState(charger));
        }
        else if (hitObstacle)
        {
            var hitPushable = charger.IsPushableAhead();

            charger.SwitchState(new ChargerRecoverState(charger));

            if (!hitPushable) charger.TakeDamage(charger.wallSlamDamage);
        }
        else if (moved < step * BlockedDistanceRatio || distanceLeft <= 0f)
        {
            charger.SwitchState(new ChargerRecoverState(charger));
        }
    }
}
