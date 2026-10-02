using UnityEngine;

public class GroundSlamState : PlayerLocomState
{
    private float _startTime;
    
    public GroundSlamState(LocomotionController locomotionController, Rigidbody rb) : base(locomotionController, rb) { }

    public override void Enter()
    {
        base.Enter();
        _startTime = Time.time;
        // Ignores collisions between player and damageables during ground slam
        Physics.IgnoreLayerCollision(player.gameObject.layer, LayerMask.NameToLayer("Damageable"), true);
    }

    public override void Update()
    {
        base.Update();
        
        if (player.GroundCheck.IsGrounded)
        {
            OnSlamImpact();
            player.SwitchState(new GroundedState(player, Rb));
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        Rb.linearVelocity = new Vector3(0f, -player.slamSpeed, 0f);
    }

    public override void Exit()
    {
        Physics.IgnoreLayerCollision(player.gameObject.layer, LayerMask.NameToLayer("Damageable"), false);
    }
    
    private void OnSlamImpact()
    {
        // player.PlaySlamImpactEffects();
    }
}
