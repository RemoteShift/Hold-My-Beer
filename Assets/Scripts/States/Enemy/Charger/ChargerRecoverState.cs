using UnityEngine;

/// <summary>Cooldown after a charge, leaving the player a window to reposition.</summary>
public class ChargerRecoverState : EnemyState
{
    private readonly ChargerEnemy charger;
    private float remaining;

    public ChargerRecoverState(ChargerEnemy charger) : base(charger)
    {
        this.charger = charger;
        remaining = charger.attackCooldown;
    }

    public override void Enter()
    {
        charger.StopPathfinding();
        charger.SetBodyColor(charger.recoverColor);
    }

    // Skips the base look-at on purpose: the charger stays facing the way it ran.
    public override void Update()
    {
        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            charger.ReturnToDefaultState();
        }
    }
}
