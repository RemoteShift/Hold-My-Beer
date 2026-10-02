using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck))]
public class LocomotionController : Singleton<LocomotionController>
{
    private PlayerLocomState currentState;
    public PlayerLocomState CurrentState => currentState;
    
    [SerializeField] private string currentStateName;
    
    public Rigidbody rb;
    public GroundCheck GroundCheck;
    
    public InputHandler input => InputHandler.Instance;

    #region Movement Settings

    [Header("Movement Settings")]
    public float moveSpeed = 1.0f;

    public float jumpSpeed = 0.8f;
    public float dashSpeed = 2.0f;
    public float dashDuration = 0.5f;
    public int maxDashCount = 3;
    public float dashRecoveryDuration = 1f;
    public float slideSpeed = 1.5f;
    public float AirAcc = 0.1f;
    public float slamSpeed = 1.5f;

    #endregion

    public bool canMove;
    
    private int _dashCount = 0;
    public int DashCount => _dashCount;
    [HideInInspector] public UnityEvent OnDashCountChanged;
    private float _dashIncTimer = 0;

    private void Awake()
    {
        _dashCount = maxDashCount;
        _dashIncTimer = dashRecoveryDuration;
    }

    private void Start()
    {
        ReturnToDefaultState();
    }

    private void Update()
    {
        if (!canMove) return;
        
        currentState?.Update();
        if(_dashCount < maxDashCount)
        {
            _dashIncTimer -= Time.deltaTime;
            if (_dashIncTimer <= 0)
            {
                _dashCount++;
                OnDashCountChanged.Invoke();
                _dashIncTimer = dashRecoveryDuration;
            }
        }
    }

    private void FixedUpdate()
    {
        if(canMove)
            currentState?.FixedUpdate();
    }

    public void ResetPositionRotation(Vector3 startPosition, Quaternion startRotation)
    {
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
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpSpeed, rb.linearVelocity.z);
    }
    
    public void BulletJump(Vector3 aimDirection)
    {
        Vector3 launchDir = aimDirection.normalized;
        
        if (launchDir.y < 0.2f)
        {
            launchDir.y = 0.2f;
            launchDir = launchDir.normalized;
        }

        var bulletJumpForce = jumpSpeed * 3f;
        
        transform.position += Vector3.up * 0.15f;
        
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(new Vector3(launchDir.x * bulletJumpForce, 
            Math.Max(Math.Min(launchDir.y * bulletJumpForce, jumpSpeed*2), jumpSpeed), launchDir.z * bulletJumpForce), ForceMode.VelocityChange);
    }

    public void Dash()
    {
        if (_dashCount > 0)
        {
            SwitchState(new DashState(this, rb));
            _dashCount--;
            OnDashCountChanged.Invoke();
        }
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

    #region Height Shrinking (for sliding)

    [Header("Height Shrinking (for sliding)")]
    [Header("References")]
    [SerializeField] private CapsuleCollider playerCollider;
    [SerializeField] private Transform cameraHolderTransform;
    
    [Header("Height settings")]
    [SerializeField] private float standingHeight = 2.0f;
    [SerializeField] private float slidingHeight = 1.0f;
    [SerializeField] private float standingCamY = 0.6f;
    [SerializeField]  private float slideCamY = -0.2f;

    public void SetSlideHeight()
    {
        playerCollider.height = slidingHeight;
        
        playerCollider.center = new Vector3(0f, -(standingHeight - slidingHeight) / 2f, 0f);

        var camPos = cameraHolderTransform.localPosition;
        cameraHolderTransform.localPosition = new Vector3(camPos.x, slideCamY, camPos.z);
    }
    
    public void SetStandingHeight()
    {
        playerCollider.height = standingHeight;
        playerCollider.center = Vector3.zero;
        
        var camPos = cameraHolderTransform.localPosition;
        cameraHolderTransform.localPosition = new Vector3(camPos.x, standingCamY, camPos.z);
    }

    #endregion
}
