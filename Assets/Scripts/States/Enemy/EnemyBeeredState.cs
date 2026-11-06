using UnityEngine;

/// <summary>
/// The enemy stands frozen mid-attack, drinking from the glass in their hand.
/// When the glass runs dry it is lost and the interrupted attack carries on from where it stopped.
/// The player can take the glass back early to keep what's left, which also resumes the attack.
/// </summary>
public class EnemyBeeredState : EnemyState
{
    private const float EmptyGlassHeight = 0.35f;

    private readonly EnemyState interruptedAttack;
    private GameObject heldGlass;
    private Vector3 fullGlassScale;
    private Vector3 slideDirection;
    private float slideDistanceLeft;

    /// <summary>Beer left in the glass, from 0 (empty) to 1 (full).</summary>
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

        heldGlass = enemy.SpawnHeldGlass();
        if (heldGlass) fullGlassScale = heldGlass.transform.localScale;

        ShowFillLevel();
    }

    public override void Exit()
    {
        if (heldGlass) Object.Destroy(heldGlass);
    }

    // Skips the base look-at on purpose: a beered enemy holds its pose.
    public override void Update()
    {
        Fill -= Time.deltaTime / enemy.fullBeerFreezeSeconds;

        if (Fill <= 0f)
        {
            Fill = 0f;
            ResumeAttack();
            return;
        }

        ShowFillLevel();
        Slide();
    }

    /// <summary>Starts a slide across the floor. The enemy's pose and aim don't change.</summary>
    public void Shove(Vector3 direction, float distance)
    {
        slideDirection = direction;
        slideDistanceLeft = distance;
    }

    private void Slide()
    {
        if (slideDistanceLeft <= 0f) return;

        if (enemy.StunEnemiesAhead(slideDirection))
        {
            slideDistanceLeft = 0f;
            return;
        }

        var speed = SlideMotion.SpeedToStopWithin(slideDistanceLeft, enemy.pushSlideDeceleration);
        var step = Mathf.Min(speed * Time.deltaTime, slideDistanceLeft);
        var before = enemy.transform.position;

        if (enemy.agent.Raycast(before + slideDirection * step, out _))
        {
            slideDistanceLeft = 0f;
            return;
        }

        enemy.agent.Move(slideDirection * step);

        var moved = Vector3.Distance(before, enemy.transform.position);
        slideDistanceLeft = SlideMotion.IsBlocked(moved, step) ? 0f : slideDistanceLeft - moved;
    }

    /// <summary>Unbeers the enemy: their attack resumes and the caller gets the remaining beer.</summary>
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

    private void ShowFillLevel()
    {
        if (!heldGlass) return;

        var height = Mathf.Lerp(EmptyGlassHeight, 1f, Fill);
        heldGlass.transform.localScale = new Vector3(fullGlassScale.x, fullGlassScale.y * height, fullGlassScale.z);
    }
}
