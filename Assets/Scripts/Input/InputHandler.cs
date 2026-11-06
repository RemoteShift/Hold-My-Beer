using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class InputHandler : Singleton<InputHandler>
{
    [Header("Action References")] 
    [SerializeField] private InputActionReference LookInput;
    [SerializeField] private InputActionReference MoveInput;
    [SerializeField] private InputActionReference JumpInput;
    [SerializeField] private InputActionReference SprintInput;
    [SerializeField] private InputActionReference BeerInput;
    [SerializeField] private InputActionReference InteractInput;
    [SerializeField] private InputActionReference PushInput;

    public event Action<Vector2, bool> OnLookInputChanged;
    public event Action OnBeerPressed;
    public event Action OnInteractPressed;
    public event Action OnPushPressed;
    public event Action<bool> OnLeftClickInput;
    
    public Vector2 moveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool IsSprinting { get; private set; }

    private void OnEnable()
    {
        LookInput.action.Enable();
        MoveInput.action.Enable();
        JumpInput.action.Enable();
        if (SprintInput) SprintInput.action.Enable();
        if (BeerInput) BeerInput.action.Enable();
        if (InteractInput) InteractInput.action.Enable();
        if (PushInput) PushInput.action.Enable();

        LookInput.action.performed += HandleLookInput;
        LookInput.action.canceled += HandleLookInput;
    }

    private void OnDisable()
    {
        LookInput.action.performed -= HandleLookInput;
        LookInput.action.canceled -= HandleLookInput;

        LookInput.action.Disable();
        MoveInput.action.Disable();
        JumpInput.action.Disable();
        if (SprintInput) SprintInput.action.Disable();
        if (BeerInput) BeerInput.action.Disable();
        if (InteractInput) InteractInput.action.Disable();
        if (PushInput) PushInput.action.Disable();
        ResetSprint();
        moveInput = Vector2.zero;
        JumpPressed = false;
    }

    private void Update()
    {
        moveInput = MoveInput.action.ReadValue<Vector2>();
        
        JumpPressed  = JumpInput.action.WasPressedThisFrame();
        if (SprintInput && SprintInput.action.WasPressedThisFrame())
            IsSprinting = !IsSprinting;

        // WasPressedThisFrame reports the press even if the action has a Hold interaction
        if (BeerInput && BeerInput.action.WasPressedThisFrame())
            OnBeerPressed?.Invoke();

        if (InteractInput && InteractInput.action.WasPressedThisFrame())
            OnInteractPressed?.Invoke();

        if (PushInput && PushInput.action.WasPressedThisFrame())
            OnPushPressed?.Invoke();
    }

    public void ResetSprint()
    {
        IsSprinting = false;
    }

    private void HandleLookInput(InputAction.CallbackContext context)
    {
        OnLookInputChanged?.Invoke(context.ReadValue<Vector2>(), context.control.device is Gamepad);
    }
}