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
    public event Action OnUnbeerPressed;
    public event Action OnPushPressed;
    public event Action<bool> OnLeftClickInput;

    public enum PromptAction { None, Beer, Interact, Push, Jump, Unbeer }

    private InputDevice lastInputDevice;
    private const float ParryFollowUpWindow = 0.1f;
    private float parryJumpUntil = -1f;
    private float parryPushUntil = -1f;
    
    public Vector2 moveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool IsSprinting { get; private set; }

    /// <summary>While true, beer, unbeer, interact and push presses are ignored, e.g. so a tutorial prompt can be read first.</summary>
    public bool ActionsBlocked { get; set; }

    public bool PauseInput { get; private set; }

    public void SetPauseInput(bool paused)
    {
        PauseInput = paused;
        if (!paused) return;
        moveInput = Vector2.zero;
        JumpPressed = false;
        ClearParryFollowUpInputs();
        OnLookInputChanged?.Invoke(Vector2.zero, lastInputDevice is Gamepad);
    }

    private InputAction unbeerInput;
    private CocktailBrewer brewer;

    private void OnEnable()
    {
        LookInput.action.Enable();
        MoveInput.action.Enable();
        JumpInput.action.Enable();
        if (SprintInput) SprintInput.action.Enable();
        if (BeerInput) BeerInput.action.Enable();
        if (InteractInput) InteractInput.action.Enable();
        if (PushInput) PushInput.action.Enable();
        unbeerInput = InteractInput ? InteractInput.action.actionMap.FindAction("Unbeer") : null;
        unbeerInput?.Enable();

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
        unbeerInput?.Disable();
        ResetSprint();
        ClearParryFollowUpInputs();
        moveInput = Vector2.zero;
        JumpPressed = false;
    }

    private void Update()
    {
        if (PauseInput) { moveInput = Vector2.zero; JumpPressed = false; return; }
        moveInput = MoveInput.action.ReadValue<Vector2>();
        if (PlayerParryFeedback.IsPlayerFrozen)
        {
            JumpPressed = false;
            CaptureParryFollowUpInputs();
            return;
        }
        if (Time.timeScale <= 0f || ActionsBlocked || AudioListener.pause) ClearParryFollowUpInputs();
        bool bufferedJump = ConsumeParryFollowUp(ref parryJumpUntil);
        bool bufferedPush = ConsumeParryFollowUp(ref parryPushUntil);
        
        JumpPressed = JumpInput.action.WasPressedThisFrame() || bufferedJump;
        if (JumpPressed) lastInputDevice = JumpInput.action.activeControl?.device ?? lastInputDevice;
        if (SprintInput && SprintInput.action.WasPressedThisFrame())
            IsSprinting = !IsSprinting;

        if (!brewer) brewer = FindFirstObjectByType<CocktailBrewer>();
        var brewing = brewer && brewer.PollInput(ActionsBlocked);
        if (ActionsBlocked || brewing) return;

        // WasPressedThisFrame reports the press even if the action has a Hold interaction
        if (BeerInput && BeerInput.action.WasPressedThisFrame())
        {
            lastInputDevice = BeerInput.action.activeControl?.device ?? lastInputDevice;
            OnBeerPressed?.Invoke();
            if (PlayerParryFeedback.IsPlayerFrozen) { CaptureParryFollowUpInputs(); return; }
        }

        if (InteractInput && InteractInput.action.WasPressedThisFrame())
        {
            lastInputDevice = InteractInput.action.activeControl?.device ?? lastInputDevice;
            OnInteractPressed?.Invoke();
        }

        var unbeerPressed = unbeerInput != null ? unbeerInput.WasPressedThisFrame() : Mouse.current?.rightButton.wasPressedThisFrame == true;
        if (unbeerPressed)
        {
            lastInputDevice = unbeerInput?.activeControl?.device ?? (InputDevice)Mouse.current ?? lastInputDevice;
            OnUnbeerPressed?.Invoke();
        }

        var pushAction = PushInput ? PushInput.action : null;
        var pushPressed = pushAction?.WasPressedThisFrame() == true || bufferedPush;
        var fallbackPush = GetPressedPushShortcut(pushAction == null);
        if (pushPressed || fallbackPush != null)
        {
            lastInputDevice = (pushPressed ? pushAction?.activeControl?.device : fallbackPush.device) ?? lastInputDevice;
            OnPushPressed?.Invoke();
        }
    }

    // Only parry hit-stop queues these follow-ups; it never buffers beer or interact.
    private void CaptureParryFollowUpInputs()
    {
        if (ActionsBlocked || AudioListener.pause) { ClearParryFollowUpInputs(); return; }
        float until = Time.realtimeSinceStartup + ParryFollowUpWindow;
        if (JumpInput.action.WasPressedThisFrame())
        {
            parryJumpUntil = until;
            lastInputDevice = JumpInput.action.activeControl?.device ?? lastInputDevice;
        }
        var action = PushInput ? PushInput.action : null;
        bool pressed = action?.WasPressedThisFrame() == true;
        var fallback = GetPressedPushShortcut(action == null);
        if (pressed || fallback != null)
        {
            parryPushUntil = until;
            lastInputDevice = (pressed ? action?.activeControl?.device : fallback.device) ?? lastInputDevice;
        }
    }

    private bool ConsumeParryFollowUp(ref float until)
    {
        bool valid = until >= Time.realtimeSinceStartup;
        until = -1f;
        return valid;
    }

    public void ClearParryFollowUpInputs()
    {
        parryJumpUntil = -1f;
        parryPushUntil = -1f;
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
        var action = prompt == PromptAction.Unbeer ? unbeerInput : reference ? reference.action : null;
        if (action == null && prompt == PromptAction.Unbeer)
        {
            layout = "Mouse";
            controlPath = "rightButton";
            return "RMB";
        }
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

    private static InputControl GetPressedPushShortcut(bool useKeyboardFallback)
    {
        var key = Keyboard.current?.eKey;
        if (useKeyboardFallback && key?.wasPressedThisFrame == true) return key;
        var button = Gamepad.current?.buttonWest;
        return button?.wasPressedThisFrame == true ? button : null;
    }

    private string GetPushShortcutDisplay(bool useKeyboardFallback, out string layout, out string controlPath)
    {
        InputControl control = null;
        if (lastInputDevice is Gamepad gamepad)
            control = gamepad.buttonWest;
        else if (lastInputDevice == null || lastInputDevice is Keyboard || lastInputDevice is Mouse)
            control = useKeyboardFallback ? Keyboard.current?.eKey : null;

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
        var look = PauseInput ? Vector2.zero : context.ReadValue<Vector2>();
        if (context.performed && look != Vector2.zero) lastInputDevice = context.control.device;
        OnLookInputChanged?.Invoke(look, context.control.device is Gamepad);
    }
}
