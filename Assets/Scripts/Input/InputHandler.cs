using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class InputHandler : Singleton<InputHandler>
{
    [Header("Action References")] 
    [SerializeField] private InputActionReference LookInput;
    [SerializeField] private InputActionReference MoveInput;
    [SerializeField] private InputActionReference JumpInput;
    [SerializeField] private InputActionReference DashInput;
    [SerializeField] private InputActionReference SlideInput;
    
    public event Action<Vector2, bool> OnLookInputChanged;
    public event Action<bool> OnLeftClickInput;
    
    public Vector2 moveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SlidePressed { get; private set; }
    public bool IsHoldingSlide { get; private set; } // Track continuous press state
    public bool DashPressed { get; private set; }

    private void OnEnable()
    {
        LookInput.action.Enable();
        MoveInput.action.Enable();
        JumpInput.action.Enable();
        DashInput.action.Enable();
        SlideInput.action.Enable();

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
        DashInput.action.Disable();
        SlideInput.action.Disable();
    }

    private void Update()
    {
        moveInput = MoveInput.action.ReadValue<Vector2>();
        
        JumpPressed  = JumpInput.action.WasPressedThisFrame();
        DashPressed  = DashInput.action.WasPressedThisFrame();
        SlidePressed = SlideInput.action.WasPressedThisFrame();
        
        // Is user holding the key right now?
        IsHoldingSlide = SlideInput.action.IsPressed(); 
    }

    private void HandleLookInput(InputAction.CallbackContext context)
    {
        OnLookInputChanged?.Invoke(context.ReadValue<Vector2>(), context.control.device is Gamepad);
    }
}