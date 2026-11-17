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

    public enum PromptAction { None, Beer, Interact, Push, Jump }

    private InputDevice lastInputDevice;
    
    public Vector2 moveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool IsSprinting { get; private set; }

    /// <summary>While true, beer, interact and push presses are ignored, e.g. so a tutorial prompt can be read first.</summary>
    public bool ActionsBlocked { get; set; }

    private void OnEnable()
    {
        LookInput.action.Enable();
        MoveInput.action.Enable();
        JumpInput.action.Enable();
        if (SprintInput) SprintInput.action.Enable();
        if (BeerInput) BeerInput.action.Enable();
        if (InteractInput) InteractInput.action.Enable();
        if (PushInput) PushInput.action.Enable();

        MoveInput.action.performed += RememberMoveDevice;
        LookInput.action.performed += HandleLookInput;
        LookInput.action.canceled += HandleLookInput;
    }

    private void OnDisable()
    {
        MoveInput.action.performed -= RememberMoveDevice;
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
        if (JumpPressed) lastInputDevice = JumpInput.action.activeControl?.device ?? lastInputDevice;
        if (SprintInput && SprintInput.action.WasPressedThisFrame())
            IsSprinting = !IsSprinting;

        if (ActionsBlocked) return;

        // WasPressedThisFrame reports the press even if the action has a Hold interaction
        if (BeerInput && BeerInput.action.WasPressedThisFrame())
        {
            lastInputDevice = BeerInput.action.activeControl?.device ?? lastInputDevice;
            OnBeerPressed?.Invoke();
        }

        if (InteractInput && InteractInput.action.WasPressedThisFrame())
        {
            lastInputDevice = InteractInput.action.activeControl?.device ?? lastInputDevice;
            OnInteractPressed?.Invoke();
        }

        var pushAction = PushInput ? PushInput.action : null;
        var pushPressed = pushAction?.WasPressedThisFrame() == true;
        var fallbackPush = GetPressedPushShortcut(pushAction == null);
        if (pushPressed || fallbackPush != null)
        {
            lastInputDevice = (pushPressed ? pushAction.activeControl?.device : fallbackPush.device) ?? lastInputDevice;
            OnPushPressed?.Invoke();
        }
    }

    public void ResetSprint()
    {
        IsSprinting = false;
    }

    public string GetPromptBindingDisplay(PromptAction prompt, out string layout, out string controlPath)
    {
        layout = null;
        controlPath = null;
        var reference = prompt switch
        {
            PromptAction.Beer => BeerInput,
            PromptAction.Interact => InteractInput,
            PromptAction.Push => PushInput,
            PromptAction.Jump => JumpInput,
            _ => null
        };
        var action = reference ? reference.action : null;
        if (action == null)
            return prompt == PromptAction.Push
                ? GetPushShortcutDisplay(true, out layout, out controlPath)
                : string.Empty;

        const InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontIncludeInteractions;
        var bestIndex = -1;
        var bestPriority = -1;
        foreach (var control in action.controls)
        {
            var index = action.GetBindingIndexForControl(control);
            if (index < 0 || action.bindings[index].isPartOfComposite) continue;

            var device = control.device;
            var preferKeyboardMouse = lastInputDevice == null || lastInputDevice is Keyboard || lastInputDevice is Mouse;
            var priority = preferKeyboardMouse
                ? (device is Keyboard || device is Mouse ? 1 : 0)
                : (device == lastInputDevice ? 2 : device is Gamepad && lastInputDevice is Gamepad ? 1 : 0);
            if (priority < bestPriority || (priority == bestPriority && index >= bestIndex)) continue;

            bestIndex = index;
            bestPriority = priority;
        }

        if (prompt == PromptAction.Push && bestPriority < 1)
        {
            var shortcut = GetPushShortcutDisplay(false, out layout, out controlPath);
            if (!string.IsNullOrEmpty(shortcut)) return shortcut;
        }

        return bestIndex < 0
            ? action.GetBindingDisplayString(options)
            : action.GetBindingDisplayString(bestIndex, out layout, out controlPath, options);
    }

    private static InputControl GetPressedPushShortcut(bool useMouseFallback)
    {
        var mouse = Mouse.current?.rightButton;
        if (useMouseFallback && mouse?.wasPressedThisFrame == true) return mouse;
        var key = Keyboard.current?.fKey;
        if (key?.wasPressedThisFrame == true) return key;
        var button = Gamepad.current?.buttonWest;
        return button?.wasPressedThisFrame == true ? button : null;
    }

    private string GetPushShortcutDisplay(bool useMouseFallback, out string layout, out string controlPath)
    {
        InputControl control = null;
        if (lastInputDevice is Gamepad gamepad)
            control = gamepad.buttonWest;
        else if (lastInputDevice == null || lastInputDevice is Keyboard || lastInputDevice is Mouse)
            control = useMouseFallback && Mouse.current != null
                ? Mouse.current.rightButton
                : Keyboard.current?.fKey;

        layout = control?.device.layout;
        controlPath = control?.name;
        return control?.shortDisplayName ?? control?.displayName ?? string.Empty;
    }

    private void RememberMoveDevice(InputAction.CallbackContext context)
    {
        if (context.ReadValue<Vector2>().sqrMagnitude > 0.01f)
            lastInputDevice = context.control.device;
    }

    private void HandleLookInput(InputAction.CallbackContext context)
    {
        var look = context.ReadValue<Vector2>();
        if (context.performed && look != Vector2.zero) lastInputDevice = context.control.device;
        OnLookInputChanged?.Invoke(look, context.control.device is Gamepad);
    }
}