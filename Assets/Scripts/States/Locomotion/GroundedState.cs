using UnityEngine;

public class GroundedState : PlayerLocomState
{
    private const float GroundSnapForce = 30f;

    public GroundedState(LocomotionController player, Rigidbody rb) : base(player, rb)
    { }
    
    public override void Update()
    {
        base.Update();
        if (!player.GroundCheck.IsGrounded)
        {
            player.SwitchState(new AirborneState(player, Rb));
            return;
        }
        
        if(player.input.JumpPressed)
        {
            player.Jump();
            player.SwitchState(new AirborneState(player, Rb));
            return;
        }

        if (player.input.SlidePressed)
        {
            player.SwitchState(new SlideState(player, Rb));
            return;
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        
        var dir = CachedCameraMoveDirection;
        var groundNormal = player.GroundCheck.IsGrounded ? player.GroundCheck.GroundNormal : Vector3.up;
        var moveDirection = Vector3.ProjectOnPlane(dir, groundNormal);
        var currentVelocity = Rb.linearVelocity;
        var verticalVelocity = Vector3.Project(currentVelocity, groundNormal);
        
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            moveDirection.Normalize();
            Rb.linearVelocity = moveDirection * player.moveSpeed + verticalVelocity;
        }
        else
        {
            var groundedVelocity = Vector3.ProjectOnPlane(currentVelocity, groundNormal) * 0.15f;
            Rb.linearVelocity = groundedVelocity + verticalVelocity;
        }

        if (player.GroundCheck.IsGrounded && Vector3.Dot(currentVelocity, groundNormal) <= 0f)
        {
            Rb.AddForce(-groundNormal * GroundSnapForce, ForceMode.Acceleration);
        }
    }
}
