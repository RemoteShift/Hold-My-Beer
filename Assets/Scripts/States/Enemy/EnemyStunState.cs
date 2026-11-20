using UnityEngine;

/// <summary>Knocked out of whatever they were doing. Any attack in progress is lost.</summary>
public class EnemyStunState : EnemyState
{
    private float remaining;
    private readonly EnemyState nextState;

    public EnemyStunState(EnemyBrain enemy, float duration, EnemyState nextState = null) : base(enemy)
    {
        remaining = duration;
        this.nextState = nextState;
    }

    public override void Enter()
    {
        enemy.StopPathfinding();
        enemy.SetBodyColor(enemy.stunnedColor);
    }

    // Skips the base look-at on purpose: a stunned enemy isn't tracking the player.
    public override void Update()
    {
        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            if (nextState != null) enemy.SwitchState(nextState);
            else enemy.ReturnToDefaultState();
        }
    }
}
