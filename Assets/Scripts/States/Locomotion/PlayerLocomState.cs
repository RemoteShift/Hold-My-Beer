using UnityEngine;

public abstract class PlayerLocomState : IState
{
    protected readonly LocomotionController player;
    protected Rigidbody Rb;
    protected Vector3 CachedCameraMoveDirection { get; private set; } = Vector3.zero;

    protected PlayerLocomState(LocomotionController player, Rigidbody rb)
    {
        this.player = player;
        Rb = rb;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }

    public virtual void Update()
    {
        CachedCameraMoveDirection = GetCameraRelativeMoveDirection();

    }

    public virtual void FixedUpdate() {}

    protected Vector3 GetCameraRelativeMoveDirection()
    {
        Vector2 input = player.input.moveInput;

        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (Camera.main)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0;
            camRight.y = 0;

            // Combine AND normalize the 3D direction
            Vector3 moveDir = (camRight.normalized * input.x) + (camForward.normalized * input.y);
        
            // Returning normalized ensures total vector length is ALWAYS 1.0 (or 0)
            return moveDir.normalized; 
        }

        return Vector3.zero;
    }

}
