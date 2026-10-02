using UnityEngine;

public class SlideState : PlayerLocomState
{
    private Vector3 _slideDir;
    
    public SlideState(LocomotionController playerLocomotion, Rigidbody rb) 
        : base(playerLocomotion, rb) { }

    public override void Enter()
    {
        base.Enter();
        
        player.SetSlideHeight();
        _slideDir = GetMoveDirection();
    }

    public override void Update()
    {
        base.Update();
        
        
        if(player.input.JumpPressed && player.GroundCheck.IsGrounded)
        {
            if (Camera.main)
            {
                Vector3 lookDir = Camera.main.transform.forward;
                player.BulletJump(lookDir);
            }

            return;
        }
    }
    
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        Rb.linearVelocity = new Vector3(
            _slideDir.x * player.slideSpeed,
            Rb.linearVelocity.y,
            _slideDir.z * player.slideSpeed
        );

        if (!player.input.IsHoldingSlide)
        {
            player.ReturnToDefaultState();
        }
    }

    public override void Exit()
    {
        base.Exit();
        player.SetStandingHeight();
    }
}
