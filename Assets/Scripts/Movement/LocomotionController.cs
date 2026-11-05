using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck))]
public class LocomotionController : Singleton<LocomotionController>
{
    private PlayerLocomState currentState;
    public PlayerLocomState CurrentState => currentState;
    
    [SerializeField] private string currentStateName;
    
    public Rigidbody rb;
    public GroundCheck GroundCheck;
    
    public InputHandler input => InputHandler.Instance;

    [Header("Audio")]
    [SerializeField] public AudioSource audioSource;
    [SerializeField] public AudioClip jumpSound;
    [SerializeField] public AudioClip walkSound;

    #region Movement Settings

    [Header("Movement Settings")]
    public float moveSpeed = 1.0f;

    public float jumpSpeed = 0.8f;
    [Min(1f)] public float sprintMultiplier = 1.5f;
    public float MovementSpeed => moveSpeed * (input.IsSprinting ? sprintMultiplier : 1f);
    public float AirAcc = 0.1f;

    #endregion

    public bool canMove;
    
    private void Start()
    {
        ReturnToDefaultState();
    }

    private void Update()
    {
        if (!canMove) return;
        
        currentState?.Update();
    }

    private void FixedUpdate()
    {
        if(canMove)
            currentState?.FixedUpdate();
    }

    public void ResetPositionRotation(Vector3 startPosition, Quaternion startRotation)
    {
        input.ResetSprint();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
        
        Physics.SyncTransforms();
        rb.Sleep();
        
        var euler = startRotation.eulerAngles;
            
        CameraLook.Instance.ResetRotation(targetYaw: euler.y, targetPitch: euler.x);

        GroundCheck.SetGroundedState(true);
        ReturnToDefaultState();
    }
    
    public void ResetPositionRotation(Vector3 startPosition, Vector3 startEulerAngles)
    {
        ResetPositionRotation(startPosition, Quaternion.Euler(startEulerAngles));
    }

    public void Jump()
    {
        GroundCheck.SetGroundedState(false);
        StopAudio(walkSound);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpSpeed, rb.linearVelocity.z);
        if (jumpSound && audioSource)
            audioSource.PlayOneShot(jumpSound);
    }

    public void StopAudio(AudioClip clip = null)
    {
        if (audioSource?.isPlaying != true || !clip || audioSource.clip == clip)
            audioSource.Stop();
    }
    
    public void SwitchState(PlayerLocomState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
        
        currentStateName = currentState != null ? currentState.GetType().Name : "None";
    }
    
    public void ReturnToDefaultState()
    {
        if (GroundCheck.IsGrounded)
        {
            SwitchState(new GroundedState(this, rb));
        }
        else
        {
            SwitchState(new AirborneState(this, rb));
        }
    }

}
