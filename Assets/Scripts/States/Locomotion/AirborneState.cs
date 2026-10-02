using UnityEngine;

public class AirborneState : PlayerLocomState
{
    public AirborneState(LocomotionController locomotionController, Rigidbody rb) : base(locomotionController, rb) { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (player.GroundCheck.IsGrounded)
        {
            player.SwitchState(new GroundedState(player, Rb));
            return;
        }

        if (player.input.SlidePressed)
        {
            player.SwitchState(new GroundSlamState(player, Rb));
            return;
        }
    }
    
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        Vector3 dir = GetCameraRelativeMoveDirection();

        if (dir.sqrMagnitude > 0.01f)
        {
            // Apply acceleration in normalized direction
            Rb.AddForce(dir * player.AirAcc, ForceMode.Acceleration);
        }

        // --- HORIZONTAL SPEED CLAMPING ---
        Vector3 currentHorizontalVel = new Vector3(Rb.linearVelocity.x, 0f, Rb.linearVelocity.z);

        // If total horizontal speed exceeds maxAirSpeed (diagonally or straight), clamp total magnitude
        if (currentHorizontalVel.magnitude > player.moveSpeed)
        {
            Vector3 clampedVel = currentHorizontalVel.normalized * player.moveSpeed;
            Rb.linearVelocity = new Vector3(clampedVel.x, Rb.linearVelocity.y, clampedVel.z);
        }
    }
}
