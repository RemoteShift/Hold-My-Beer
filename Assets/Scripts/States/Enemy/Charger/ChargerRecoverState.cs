using UnityEngine;

/// <summary>Cooldown after a charge, leaving the player a window to reposition.</summary>
public class ChargerRecoverState : EnemyState
{
    private readonly ChargerEnemy charger;
    private readonly bool hitPlayer;
    private float remaining;

    public ChargerRecoverState(ChargerEnemy charger, bool hitPlayer = false) : base(charger)
    {
        this.charger = charger;
        this.hitPlayer = hitPlayer;
        remaining = charger.attackCooldown;
    }

    public override void Enter()
    {
        charger.StopPathfinding();
        charger.SetBodyColor(charger.recoverColor);
        charger.Animator?.SetBool("IsCharging", false);
        if (hitPlayer)
            charger.Animator?.SetTrigger("HitPlayer");
        else
            charger.Animator?.SetTrigger("HitWall");
    }

    // Skips the base look-at on purpose: the charger stays facing the way it ran.
    public override void Update()
    {
        remaining -= charger.RecoveryDeltaTime;

        if (remaining <= 0f)
        {
            charger.ReturnToDefaultState();
        }
    }
}
