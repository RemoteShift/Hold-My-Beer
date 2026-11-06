public class ChargerChaseState : EnemyState
{
    private readonly ChargerEnemy charger;
    private float originalStoppingDistance;

    public ChargerChaseState(ChargerEnemy charger) : base(charger)
    {
        this.charger = charger;
    }

    public override void Enter()
    {
        originalStoppingDistance = charger.agent.stoppingDistance;
        charger.SetBodyColor(charger.chaseColor);
    }

    public override void Exit()
    {
        charger.agent.stoppingDistance = originalStoppingDistance;
    }

    public override void Update()
    {
        base.Update();

        if (!charger.CanSeeTarget())
        {
            charger.StopPathfinding();
            return;
        }

        if (charger.CanStartCharge())
        {
            charger.SwitchState(new ChargerWindupState(charger));
            return;
        }

        // Inspector tuning must not allow navigation to stop outside attack range.
        charger.agent.stoppingDistance = UnityEngine.Mathf.Min(originalStoppingDistance,
            UnityEngine.Mathf.Max(0f, charger.attackRange * 0.8f));
        charger.PathfindToTarget();
    }
}
