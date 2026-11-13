using UnityEngine;

/// <summary>
/// The last enemy standing gives up and runs for the exit, where it despawns.
/// It no longer attacks, so it can't be beered, but a stun or a blast still holds it up.
/// </summary>
public class EnemyLeaveState : EnemyState
{
    private const float ArriveDistance = 0.75f;
    private const float MinLeaveAcceleration = 40f;

    private readonly Vector3 exit;

    public EnemyLeaveState(EnemyBrain enemy, Vector3 exit) : base(enemy)
    {
        this.exit = exit;
    }

    public override void Enter()
    {
        enemy.SetBodyColor(enemy.leavingColor);

        if (!enemy.CanNavigate) return;

        // Attack states can leave a large stopping distance, which would halt the run short of the exit
        enemy.agent.stoppingDistance = 0f;

        // Bolts for the door instead of slowly building up speed. Never undone, since the enemy is leaving.
        enemy.agent.acceleration = Mathf.Max(enemy.agent.acceleration, MinLeaveAcceleration);
    }

    // Skips the base look-at on purpose: the enemy faces where it's running, not the player.
    public override void Update()
    {
        var toExit = exit - enemy.transform.position;
        toExit.y = 0f;

        if (toExit.sqrMagnitude <= ArriveDistance * ArriveDistance)
        {
            enemy.Despawn();
            return;
        }

        enemy.agent.speed *= enemy.leaveSpeedMultiplier;
        enemy.PathfindTo(exit);
        enemy.FaceDirection(enemy.agent.velocity);
    }
}
