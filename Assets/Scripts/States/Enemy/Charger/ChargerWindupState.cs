using UnityEngine;

/// <summary>Telegraph before the charge. The timer lives on the state so it can pick up where it left off.</summary>
public class ChargerWindupState : EnemyState
{
    private readonly ChargerEnemy charger;
    private float remaining;
    private bool aimLocked;

    public override bool IsAttacking => true;
    public override void Retarget() { aimLocked = false; }

    // After a beer the charger keeps its frozen aim, so the player can step aside and let it hit something else.
    public override void PrepareToResume()
    {
        aimLocked = true;
    }

    public ChargerWindupState(ChargerEnemy charger) : base(charger)
    {
        this.charger = charger;
        remaining = charger.windupDuration;
    }

    public override void Enter()
    {
        charger.StopPathfinding();
        charger.SetBodyColor(charger.windupColor);
        charger.PlayPose("Windup", Progress);
    }

    private float Progress => 1f - remaining / Mathf.Max(0.001f, charger.windupDuration);

    public override void Update()
    {
        if (!aimLocked) base.Update();

        remaining -= charger.WindupDeltaTime;
        charger.SetPoseTime(Progress);

        if (remaining <= 0f)
        {
            charger.SwitchState(new ChargerChargeState(charger));
        }
    }
}
