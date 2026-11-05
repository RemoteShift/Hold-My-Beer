using UnityEngine;

public class EnemyState : IState
{
    protected readonly EnemyBrain enemy;

    /// <summary>True during an attack, which is the only time a beer can parry the enemy.</summary>
    public virtual bool IsAttacking => false;

    public EnemyState(EnemyBrain enemy)
    {
        this.enemy = enemy;
    }

    /// <summary>Called just before an attack carries on after a beer, whether it ran out or was taken back.</summary>
    public virtual void PrepareToResume() { }

    public virtual void Enter() { }

    public virtual void Exit() { }

    public virtual void Update()
    {
        enemy.LookAtTarget();
    }

    public virtual void FixedUpdate() { }
}
