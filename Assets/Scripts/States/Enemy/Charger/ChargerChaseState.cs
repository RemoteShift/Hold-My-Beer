public class ChargerChaseState : EnemyState
{
    private readonly ChargerEnemy charger;

    public ChargerChaseState(ChargerEnemy charger) : base(charger)
    {
        this.charger = charger;
    }

    public override void Enter()
    {
        charger.SetBodyColor(charger.chaseColor);
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

        charger.PathfindToTarget();
    }
}
