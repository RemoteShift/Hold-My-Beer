using UnityEngine;

public class DashState : PlayerLocomState
{
    private float _dashTimer;
    private Vector3 _dashDir;
    
    private Vector3 _initialVelocity;

    public DashState(LocomotionController playerLocomotion, Rigidbody rb) 
        : base(playerLocomotion, rb) { }

    public override void Enter()
    {
        base.Enter();
        
        _dashDir = GetMoveDirection();
        _dashTimer = player.dashDuration;
        _initialVelocity = Rb.linearVelocity;
        
        // Uncomment next line if you want to disable collisions between player and damageables during dash
        // Physics.IgnoreLayerCollision(player.gameObject.layer, LayerMask.NameToLayer("Damageable"), true);
    }

    public override void Update()
    {
        if(player.input.SlidePressed && !player.GroundCheck.IsGrounded)
        {
            player.SwitchState(new GroundSlamState(player, Rb));
            return;
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 1. Check the actual velocity resulting from LAST frame's physics/collision resolution
        // (Skip this check on the first frame of the dash using a timer buffer)
        if (_dashTimer < player.dashDuration - Time.fixedDeltaTime)
        {
            Vector3 actualHorizontalVel = new Vector3(Rb.linearVelocity.x, 0f, Rb.linearVelocity.z);

            // If physics reduced our velocity significantly (e.g. hitting a wall), cancel dash!
            if (actualHorizontalVel.magnitude < player.dashSpeed * 0.5f)
            {
                player.ReturnToDefaultState();
                return;
            }
        }

        _dashTimer -= Time.fixedDeltaTime;

        // 2. NOW apply the dash velocity for THIS frame
        Rb.linearVelocity = new Vector3(_dashDir.x * player.dashSpeed, 0f, _dashDir.z * player.dashSpeed);

        if (_dashTimer <= 0f)
        {
            player.ReturnToDefaultState();
        }
    }

    public override void Exit()
    {
        base.Exit();
        // Zero out velocity so player doesn't keep sliding after hitting wall/ending dash
        Rb.linearVelocity = new Vector3(_initialVelocity.x, 0f, _initialVelocity.z);
        
        // Uncomment next line if you want to re-enable collisions between player and damageables after dash
        // Physics.IgnoreLayerCollision(player.gameObject.layer, LayerMask.NameToLayer("Damageable"), false);
    }
}