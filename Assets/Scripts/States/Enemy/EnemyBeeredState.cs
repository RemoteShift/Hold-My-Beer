using UnityEngine;

/// <summary>
/// Beer interrupts the current attack until the stamp timer runs out.
/// The remaining beer can still be reclaimed early, which resumes the attack.
/// No physical glass or drinking animation is required.
/// </summary>
public class EnemyBeeredState : EnemyState
{
    private readonly EnemyState interruptedAttack;

    /// <summary>Remaining beer effect, from 0 (expired) to 1 (full).</summary>
    public float Fill { get; private set; }

    public EnemyBeeredState(EnemyBrain enemy, float fill, EnemyState interruptedAttack) : base(enemy)
    {
        Fill = fill;
        this.interruptedAttack = interruptedAttack;
    }

    public override void Enter()
    {
        enemy.StopPathfinding();
        enemy.SetBodyColor(enemy.beeredColor);
    }

    // Skips the base look-at on purpose: a beered enemy holds its interrupted pose.
    public override void Update()
    {
        Fill -= Time.deltaTime / enemy.fullBeerFreezeSeconds;
        if (Fill <= 0f)
        {
            Fill = 0f;
            ResumeAttack();
        }
    }

    /// <summary>Ends the effect, resumes the attack, and returns the remaining beer.</summary>
    public float TakeGlassBack()
    {
        var remaining = Fill;
        ResumeAttack();
        return remaining;
    }

    private void ResumeAttack()
    {
        interruptedAttack.PrepareToResume();
        enemy.SwitchState(interruptedAttack);
    }
}
