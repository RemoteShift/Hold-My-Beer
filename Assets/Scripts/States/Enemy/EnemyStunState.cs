using UnityEngine;

/// <summary>Knocked out of whatever they were doing. Any attack in progress is lost.</summary>
public class EnemyStunState : EnemyState
{
    private float remaining;

    public EnemyStunState(EnemyBrain enemy, float duration) : base(enemy)
    {
        remaining = duration;
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
            enemy.ReturnToDefaultState();
        }
    }
}
