using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>A real mesh card stack rendered over the paused world, with unscaled motion.</summary>
[DefaultExecutionOrder(-250)]
public sealed class PauseCardMenu : MonoBehaviour
{
    public const int CardLayer = 29;
    public SharedSettingsMenus settingsMenus;
    public LevelRestarter restarter;
    public Transform stage;
    public Camera cardCamera;
    public Transform[] cards;
    [Header("Presentation")]
    [Range(.04f, .3f)] public float browseSmoothSeconds = .075f;
    [Range(.15f, .6f)] public float openSeconds = .34f;
    [Range(.15f, .7f)] public float browseRepeatSeconds = .30f;
    [Range(.1f, .5f)] public float continuousStartDelay = .24f;
    [Range(.06f, .2f)] public float fastBrowseRepeatSeconds = .095f;
    [Range(.05f, .3f)] public float browseAccelerationSeconds = .14f;
    [Range(.02f, .10f)] public float browseDeadZoneHeight = .045f;
    [Range(.08f, .3f)] public float continuousBrowseStartHeight = .16f;
    [Range(.12f, .4f)] public float fullSpeedDistanceHeight = .28f;
    [Range(0f, .65f)] public float backdropOpacity = .52f;
    [Range(2f, 16f)] public float fanDegrees = 10f;
    public Shader backgroundBlurShader;

    public bool IsOpen { get; private set; }
    public int SelectedIndex => Mathf.Clamp(Mathf.RoundToInt(browsePosition), 0, cards.Length - 1);
    public float BrowsePosition => browsePosition;
    public string CurrentView => view.ToString();

    private enum View { Closed, Cards, Transition, Audio, Controls }
    private enum CardAction { Controls, Audio, Resume, Restart, MainMenu, Quit }
    private View view;
    private readonly List<CameraState> cameraStates = new List<CameraState>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private CanvasGroup overlay;
    private GameObject overlayRoot;
    private UnityEngine.UI.RawImage output;
    private RenderTexture outputTexture;
    private UnityEngine.UI.RawImage blurredBackground;
    private RenderTexture backgroundTexture;
    private Material blurMaterial;
    private float targetBrowse, browsePosition, browseVelocity, visibility;
    private Vector2 tilt, tiltVelocity, previousPointer;
    private int hoverDirection;
    private float continuousRepeatProgress, browseStrength;
    private bool isContinuousBrowsing, hasRepeatedContinuousBrowse;
    private int pendingActivationIndex = -1;
    private float pendingActivationAt;
    private bool settleArmed;
    private float settleElapsed = -1, settleDirection;
    private float edgeElapsed = -1, edgeDirection, wheelRemainder, clickLift;
    private bool edgeFeedbackLatched, wheelOwnsBrowsing;
    private Vector2 wheelPointerAnchor;
    private int liftedCardIndex = -1;
    private float previousTimeScale;
    private bool previousAudioPause, previousCanLook, previousActionsBlocked, previousPauseInput;
    private bool previousOptionsInteractable, previousOptionsRaycasts;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private int openedFrame;
    private CameraLook playerLook;
    private InputHandler playerInput;
    private CanvasGroup menuOptions;
    private GameOverController gameOver;
    private BottleResultsScreen results;

    private sealed class CameraState
    {
        public Camera camera;
        public bool enabled, listenerEnabled, brainEnabled, ignoreTimeScale;
        public Vector3 position;
        public Quaternion rotation;
        public float fov, near, far;
        public AudioListener listener;
        public CinemachineBrain brain;

        public CameraState(Camera value)
        {
            camera = value; enabled = value.enabled;
            position = value.transform.position; rotation = value.transform.rotation;
            fov = value.fieldOfView; near = value.nearClipPlane; far = value.farClipPlane;
            listener = value.GetComponent<AudioListener>();
            listenerEnabled = listener && listener.enabled;
            brain = value.GetComponent<CinemachineBrain>();
            brainEnabled = brain && brain.enabled;
            ignoreTimeScale = brain && brain.IgnoreTimeScale;
        }

        public void Restore(bool paused)
        {
            if (!camera) return;
            if (brain) { brain.enabled = false; brain.IgnoreTimeScale = ignoreTimeScale; }
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov; camera.nearClipPlane = near; camera.farClipPlane = far;
            camera.enabled = enabled;
            if (listener) listener.enabled = listenerEnabled;
            if (brain) brain.enabled = !paused && brainEnabled;
        }
    }

    private void Awake()
    {
        settingsMenus.receiptControls.ClosedFromPause += ControlsClosedFromPause;
        stage.gameObject.SetActive(false);
        cardCamera.enabled = false;
        BuildOverlay();
        gameOver = FindFirstObjectByType<GameOverController>(FindObjectsInactive.Include);
        results = FindFirstObjectByType<BottleResultsScreen>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            HandleEscape();
        if (!IsOpen) return;
        if (view == View.Audio && !settingsMenus.audioMenu.IsOpen && !settingsMenus.audioMenu.IsTransitioning)
            StartCoroutine(ReturnToCards());
        else if (view == View.Controls && !settingsMenus.receiptControls.IsOpen && !settingsMenus.receiptControls.IsTransitioning)
            StartCoroutine(ReturnToCards());
        if (view != View.Cards || Time.frameCount <= openedFrame + 1) return;
        // A click commits once to its raycast hit, even while cards are moving.
        if (pendingActivationIndex >= 0) return;

        if (Mouse.current != null)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                int hit = PickCard(pointer);
                if (hit >= 0) CommitActivation(hit);
                return;
            }
            if (BrowseFromWheel(Mouse.current.scroll.y.ReadValue(), pointer)) return;
            if (wheelOwnsBrowsing && Vector2.Distance(pointer, wheelPointerAnchor) > Screen.height * .015f)
                wheelOwnsBrowsing = false;
            if (!wheelOwnsBrowsing) BrowseFromPointer(pointer);
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) { SetBrowsePosition(Mathf.Round(targetBrowse) + 1); PositionPointerForCard(); }
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) { SetBrowsePosition(Mathf.Round(targetBrowse) - 1); PositionPointerForCard(); }
            if (Keyboard.current.enterKey.wasPressedThisFrame) CommitActivation(Mathf.RoundToInt(targetBrowse));
        }
    }

    private void LateUpdate()
    {
        if (!IsOpen || !stage.gameObject.activeSelf) return;
        ResizeOutput();
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        float smoothTime = pendingActivationIndex >= 0 ? .045f : Mathf.Lerp(browseSmoothSeconds, .05f, browseStrength);
        browsePosition = Mathf.SmoothDamp(browsePosition, targetBrowse, ref browseVelocity, smoothTime, Mathf.Infinity, dt);
        if (settleArmed && Mathf.Abs(browsePosition - targetBrowse) < .12f && Mathf.Abs(browseVelocity) < 2f)
        {
            settleArmed = false;
            settleElapsed = 0;
        }
        // Damped pose offsets keep the logical selection independent of the flourish.
        float settling = 0;
        if (settleElapsed >= 0)
        {
            settleElapsed += dt;
            settling = Mathf.Sin(settleElapsed * 22f) * Mathf.Exp(-settleElapsed * 10f) * settleDirection;
            if (settleElapsed > .55f) settleElapsed = -1;
        }
        var pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * .5f;
        bool pointerInView = pointer.x >= 0 && pointer.x <= Screen.width && pointer.y > Screen.height * .05f && pointer.y < Screen.height * .95f;
        var desiredTilt = pointerInView ? new Vector2(
            Mathf.Clamp((pointer.x - Screen.width * .5f) / Mathf.Max(1, Screen.height * .3f), -1, 1) * 10f,
            Mathf.Clamp((pointer.y - Screen.height * .5f) / Mathf.Max(1, Screen.height * .3f), -1, 1) * 7f) : Vector2.zero;
        tilt = Vector2.SmoothDamp(tilt, desiredTilt, ref tiltVelocity, .10f, Mathf.Infinity, dt);
        float flow = Mathf.Clamp(browseVelocity / 12f, -1, 1);
        float edgeMotion = 0;
        if (edgeElapsed >= 0)
        {
            edgeElapsed += dt;
            edgeMotion = edgeDirection * Mathf.Sin(Mathf.Clamp01(edgeElapsed / .32f) * Mathf.PI) * Mathf.Exp(-edgeElapsed * 5f);
            if (edgeElapsed >= .32f) edgeElapsed = -1;
        }
        if (pendingActivationIndex >= 0) clickLift = Mathf.SmoothStep(0, 1, (Time.unscaledTime - pendingActivationAt) / .10f);
        for (int i = 0; i < cards.Length; i++)
        {
            float offset = i - browsePosition;
            float spread = Mathf.Clamp(offset, -2.8f, 2.8f);
            float distance = Mathf.Abs(offset);
            float signed = offset / Mathf.Sqrt(offset * offset + .015f);
            float peel = distance < 1 ? Mathf.Sin(distance * Mathf.PI) : 0;
            float focus = Mathf.Exp(-distance * distance * 2.4f);
            float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01(visibility * 1.21f - i * .035f));
            float entrance = 1 - reveal;
            float lift = i == liftedCardIndex ? clickLift : 0;
            Quaternion rotation = Quaternion.Euler(-7f - tilt.y + peel * 10f - Mathf.Abs(flow) * 4f + entrance * 32f + settling * focus * .65f - lift * 2f,
                -tilt.x + spread * 5f * reveal + flow * peel * 18f + entrance * (48f + i * 5f),
                (-spread * fanDegrees - signed * peel * 20f) * reveal - entrance * (22f - i * 2f) + settling * focus * 1.4f - edgeMotion * .8f);
            // Each mesh grips the same point near its lower edge; cards fan around it rather than sliding flat.
            var grip = new Vector3(0, -.225f, 0);
            var pivot = new Vector3((spread * .02f + signed * peel * .045f) * reveal + edgeMotion * .035f,
                -.825f - distance * .015f + peel * .025f - entrance * .65f + lift * .045f,
                .12f + distance * .16f - focus * .40f - peel * .50f - signed * peel * flow * .28f + entrance * .9f + i * .001f + settling * focus * .012f - lift * .08f);
            cards[i].localPosition = pivot - rotation * grip;
            cards[i].localRotation = rotation;
        }
        if (view == View.Cards && pendingActivationIndex >= 0 &&
            Time.unscaledTime - pendingActivationAt >= .12f &&
            ((Mathf.Abs(browsePosition - targetBrowse) < .035f && Mathf.Abs(browseVelocity) < .6f) ||
             Time.unscaledTime - pendingActivationAt >= .22f))
        {
            int committedIndex = pendingActivationIndex;
            pendingActivationIndex = -1;
            ActivateCard(committedIndex);
        }
    }

    public void HandleEscape()
    {
        if (!IsOpen)
        {
            // Existing settings retain their usual Back/Esc behavior outside the pause menu.
            if (settingsMenus.audioMenu && (settingsMenus.audioMenu.IsOpen || settingsMenus.audioMenu.IsTransitioning)) return;
            if (settingsMenus.receiptControls && settingsMenus.receiptControls.IsOpen) { settingsMenus.receiptControls.Close(); return; }
            Open();
        }
        else if (view == View.Cards) Resume();
        else if (view == View.Audio && !settingsMenus.audioMenu.IsTransitioning) settingsMenus.audioMenu.Close();
        else if (view == View.Controls) settingsMenus.receiptControls.Close();
    }

    public void Open()
    {
        if (IsOpen || (gameOver && gameOver.IsGameOver) || (results && results.IsShowing)) return;
        // Gameplay can start through the intro or directly with the player's objects enabled.
        var activePlayerInput = FindFirstObjectByType<InputHandler>();
        if (!activePlayerInput || !activePlayerInput.isActiveAndEnabled) return;
        PlayerParryFeedback.CancelActiveFreeze();
        previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
        previousPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        playerLook = FindFirstObjectByType<CameraLook>(FindObjectsInactive.Include);
        playerInput = activePlayerInput;
        if (playerLook) { previousCanLook = playerLook.canLook; playerLook.canLook = false; }
        if (playerInput)
        {
            previousActionsBlocked = playerInput.ActionsBlocked; previousPauseInput = playerInput.PauseInput;
            playerInput.ActionsBlocked = true; playerInput.SetPauseInput(true);
        }
        menuOptions = settingsMenus.mainMenuOptions;
        if (menuOptions)
        {
            previousOptionsInteractable = menuOptions.interactable;
            previousOptionsRaycasts = menuOptions.blocksRaycasts;
            menuOptions.interactable = menuOptions.blocksRaycasts = false;
        }
        hiddenCanvases.Clear();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.enabled || canvas.renderMode == RenderMode.WorldSpace || canvas.transform.IsChildOf(transform) || canvas.GetComponent<FilmCursor>()) continue;
            hiddenCanvases.Add(canvas); canvas.enabled = false;
        }
        cameraStates.Clear();
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera == cardCamera || camera.targetTexture || !camera.gameObject.activeInHierarchy) continue;
            var state = new CameraState(camera); cameraStates.Add(state);
            if (state.brain) state.brain.enabled = false;
        }
        Time.timeScale = 0; AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        ConfigureFilmLook();
        targetBrowse = browsePosition = (int)CardAction.Resume;
        browseVelocity = 0;
        clickLift = 0; liftedCardIndex = -1; wheelRemainder = 0; wheelOwnsBrowsing = false;
        ResetCardMotion();
        IsOpen = true;
        PositionPointerForCard();
        StartCoroutine(ShowCards());
    }

    public void SetBrowsePosition(float index)
    {
        float next = Mathf.Clamp(index, 0, cards.Length - 1);
        if (Mathf.Approximately(next, targetBrowse))
        {
            if (index != next && !edgeFeedbackLatched)
            {
                edgeDirection = Mathf.Sign(index - next); edgeElapsed = 0; edgeFeedbackLatched = true;
            }
            return;
        }
        edgeFeedbackLatched = false;
        edgeElapsed = -1;
        settleDirection = Mathf.Sign(next - targetBrowse);
        settleArmed = true;
        settleElapsed = -1;
        targetBrowse = next;
    }

    private void CommitActivation(int index)
    {
        if (view != View.Cards || pendingActivationIndex >= 0 || index < 0 || index >= cards.Length) return;
        pendingActivationIndex = index;
        liftedCardIndex = index; clickLift = 0;
        pendingActivationAt = Time.unscaledTime;
        SetBrowsePosition(index);
        ResetPointerBrowsing();
    }

    public void ActivateSelected() => CommitActivation(SelectedIndex);

    private void ActivateCard(int index)
    {
        if (view != View.Cards) return;
        ResetCardMotion();
        var action = (CardAction)index;
        if (action == CardAction.Resume) Resume();
        else if (action == CardAction.Audio || action == CardAction.Controls) StartCoroutine(OpenSetting(action == CardAction.Audio));
        else
        {
            StopAllCoroutines(); HideImmediate(); RestoreWorld();
            if (action == CardAction.Restart) restarter.TryAgain();
            else if (action == CardAction.MainMenu) restarter.GoToMainMenu();
            else if (action == CardAction.Quit) QuitGame();
        }
    }

    public void Resume()
    {
        if (!IsOpen || view != View.Cards) return;
        ResetCardMotion();
        StartCoroutine(ResumeRoutine());
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator ResumeRoutine()
    {
        view = View.Transition;
        yield return AnimateVisibility(false);
        HideImmediate(); RestoreWorld();
    }

    private IEnumerator ShowCards()
    {
        view = View.Transition;
        clickLift = 0; liftedCardIndex = -1;
        ResizeOutput();
        // Capture only the paused world, before either the cards or their overlay is visible.
        FilmCursor.SetCaptureHidden(true);
        try
        {
            yield return new WaitForEndOfFrame();
            CaptureBlurredBackground();
        }
        finally { FilmCursor.SetCaptureHidden(false); }
        overlayRoot.SetActive(true); stage.gameObject.SetActive(true); cardCamera.enabled = true;
        yield return AnimateVisibility(true);
        view = View.Cards; openedFrame = Time.frameCount;
    }

    private IEnumerator AnimateVisibility(bool show)
    {
        float start = visibility, finish = show ? 1 : 0;
        float duration = openSeconds * Mathf.Abs(finish - start);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            visibility = Mathf.Lerp(start, finish, Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, duration)));
            overlay.alpha = visibility;
            yield return null;
        }
        visibility = overlay.alpha = finish;
    }

    private IEnumerator OpenSetting(bool audio)
    {
        view = View.Transition;
        yield return AnimateVisibility(false);
        overlayRoot.SetActive(false); cardCamera.enabled = false; stage.gameObject.SetActive(false);
        ReleaseBackground();
        CameraState visible = null;
        foreach (var state in cameraStates)
        {
            if (!state.camera) continue;
            if (state.enabled && (visible == null || state.camera.depth > visible.camera.depth)) visible = state;
            state.camera.enabled = false;
            if (state.listener) state.listener.enabled = false;
        }
        var menuCamera = settingsMenus.menuCamera;
        if (visible != null)
        {
            menuCamera.transform.SetPositionAndRotation(visible.position, visible.rotation);
            menuCamera.fieldOfView = visible.fov;
            menuCamera.nearClipPlane = visible.near;
            menuCamera.farClipPlane = visible.far;
        }
        settingsMenus.cameraBrain.enabled = false;
        menuCamera.enabled = true;
        var listener = menuCamera.GetComponent<AudioListener>(); if (listener) listener.enabled = true;
        settingsMenus.cameraBrain.IgnoreTimeScale = true;
        if (audio) { settingsMenus.audioMenu.Open(); view = View.Audio; }
        else { settingsMenus.receiptControls.OpenFromPause(); view = View.Controls; }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if ((audio && !settingsMenus.audioMenu.IsOpen) || (!audio && !settingsMenus.receiptControls.IsOpen))
            yield return ReturnToCards();
    }

    private IEnumerator ReturnToCards()
    {
        view = View.Transition;
        foreach (var state in cameraStates) state.Restore(true);
        if (menuOptions) menuOptions.interactable = menuOptions.blocksRaycasts = false;
        PositionPointerForCard();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        yield return ShowCards();
    }

    private void ControlsClosedFromPause()
    {
        if (IsOpen && view == View.Controls) StartCoroutine(ReturnToCards());
    }

    private void PositionPointerForCard()
    {
        ResetPointerBrowsing();
        wheelOwnsBrowsing = false; wheelRemainder = 0;
        if (Mouse.current != null) Mouse.current.WarpCursorPosition(new Vector2(Screen.width * .5f, Screen.height * .35f));
    }

    private void ResetPointerBrowsing()
    {
        hoverDirection = 0; browseStrength = 0; isContinuousBrowsing = false;
        hasRepeatedContinuousBrowse = false; continuousRepeatProgress = 0;
        edgeFeedbackLatched = false;
    }

    private void ResetCardMotion()
    {
        pendingActivationIndex = -1; settleArmed = false; settleElapsed = -1;
        edgeElapsed = -1;
        ResetPointerBrowsing();
    }

    private void BrowseFromPointer(Vector2 pointer)
    {
        int direction = 0;
        bool inside = pointer.x >= 0 && pointer.x <= Screen.width && pointer.y > Screen.height * .1f && pointer.y < Screen.height * .9f;
        float edge = Screen.height * browseDeadZoneHeight;
        float delta = pointer.x - Screen.width * .5f;
        float distance = Mathf.Abs(delta);
        if (inside)
        {
            // A smaller release zone prevents tiny movements around the entry edge from re-triggering a step.
            float releaseEdge = edge * .55f;
            if (hoverDirection == 1 && delta > releaseEdge) direction = 1;
            else if (hoverDirection == -1 && delta < -releaseEdge) direction = -1;
            else direction = delta > edge ? 1 : delta < -edge ? -1 : 0;
        }
        if (direction == 0)
        {
            ResetPointerBrowsing();
            return;
        }
        if (direction != hoverDirection)
        {
            // One step per nudge. Centering the pointer re-arms the next nudge.
            ResetPointerBrowsing(); hoverDirection = direction;
            SetBrowsePosition(Mathf.Round(targetBrowse) + direction);
        }
        float continuousEdge = Screen.height * continuousBrowseStartHeight;
        float releaseContinuousEdge = continuousEdge * .85f;
        bool inContinuousZone = distance >= (isContinuousBrowsing ? releaseContinuousEdge : continuousEdge);
        if (!inContinuousZone)
        {
            isContinuousBrowsing = false; browseStrength = 0; continuousRepeatProgress = 0;
            return;
        }
        float fullSpeedEdge = Mathf.Max(continuousEdge + Screen.height * .01f, Screen.height * fullSpeedDistanceHeight);
        float desiredStrength = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(continuousEdge, fullSpeedEdge, distance));
        // Ease acceleration only; moving inward slows or stops browsing immediately.
        float acceleration = 1 - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(.01f, browseAccelerationSeconds));
        browseStrength = desiredStrength <= browseStrength ? desiredStrength : Mathf.Lerp(browseStrength, desiredStrength, acceleration);
        if (!isContinuousBrowsing)
        {
            isContinuousBrowsing = true;
            hasRepeatedContinuousBrowse = false; continuousRepeatProgress = 0;
            return;
        }
        float interval = hasRepeatedContinuousBrowse
            ? Mathf.Lerp(browseRepeatSeconds, fastBrowseRepeatSeconds, browseStrength)
            : Mathf.Lerp(continuousStartDelay, fastBrowseRepeatSeconds * 1.25f, browseStrength);
        continuousRepeatProgress += Time.unscaledDeltaTime / Mathf.Max(.01f, interval);
        if (continuousRepeatProgress < 1) return;
        SetBrowsePosition(Mathf.Round(targetBrowse) + direction);
        continuousRepeatProgress = Mathf.Min(continuousRepeatProgress - 1, .5f);
        hasRepeatedContinuousBrowse = true;
    }

    private bool BrowseFromWheel(float scroll, Vector2 pointer)
    {
        if (Mathf.Abs(scroll) < .001f) return false;
        if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
            (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)) scroll /= 120f;
        if (wheelRemainder * scroll < 0) wheelRemainder = 0;
        wheelRemainder += scroll;
        ResetPointerBrowsing(); wheelOwnsBrowsing = true; wheelPointerAnchor = pointer;
        int notches = Mathf.FloorToInt(Mathf.Abs(wheelRemainder) + .0001f);
        int steps = Mathf.Min(notches, cards.Length);
        int direction = wheelRemainder > 0 ? -1 : 1;
        for (int i = 0; i < steps; i++) SetBrowsePosition(Mathf.Round(targetBrowse) + direction);
        wheelRemainder -= Mathf.Sign(wheelRemainder) * notches;
        return true;
    }

    private void ConfigureFilmLook()
    {
        Camera source = settingsMenus.menuCamera;
        foreach (var state in cameraStates)
            if (state.camera && state.enabled && (!source || !source.enabled || state.camera.depth > source.depth)) source = state.camera;
        var data = cardCamera.GetComponent<UniversalAdditionalCameraData>();
        var sourceData = source ? source.GetComponent<UniversalAdditionalCameraData>() : null;
        data.renderPostProcessing = true;
        data.volumeLayerMask = sourceData ? sourceData.volumeLayerMask : (LayerMask)1;
        data.volumeTrigger = sourceData && sourceData.volumeTrigger ? sourceData.volumeTrigger : source ? source.transform : cardCamera.transform;
        cardCamera.allowHDR = source ? source.allowHDR : true;
    }

    private int PickCard(Vector2 pointer)
    {
        if (!outputTexture) return -1;
        pointer = new Vector2(pointer.x * outputTexture.width / Mathf.Max(1, Screen.width),
            pointer.y * outputTexture.height / Mathf.Max(1, Screen.height));
        Physics.SyncTransforms();
        if (!Physics.Raycast(cardCamera.ScreenPointToRay(pointer), out var hit, 10, 1 << CardLayer)) return -1;
        for (int i = 0; i < cards.Length; i++) if (hit.transform.IsChildOf(cards[i])) return i;
        return -1;
    }

    private void ResizeOutput()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (outputTexture && outputTexture.width == width && outputTexture.height == height) return;
        if (outputTexture) { cardCamera.targetTexture = null; outputTexture.Release(); Destroy(outputTexture); }
        outputTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        { name = "Pause Card Menu Output", antiAliasing = 4, filterMode = FilterMode.Bilinear };
        outputTexture.Create(); cardCamera.targetTexture = outputTexture; output.texture = outputTexture;
    }

    private void BuildOverlay()
    {
        overlayRoot = new GameObject("Pause Cards Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
        overlayRoot.transform.SetParent(transform, false);
        var canvas = overlayRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = overlayRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        overlay = overlayRoot.GetComponent<CanvasGroup>();
        blurredBackground = Rect("Gaussian Blurred World", overlayRoot.transform).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        Stretch(blurredBackground.rectTransform); blurredBackground.raycastTarget = false;
        var backdrop = Rect("Paused World Backdrop", overlayRoot.transform);
        Stretch(backdrop); var bg = backdrop.gameObject.AddComponent<UnityEngine.UI.Image>(); bg.color = new Color(0, 0, 0, backdropOpacity); bg.raycastTarget = false;
        output = Rect("3D Cards", overlayRoot.transform).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        Stretch(output.rectTransform); output.raycastTarget = true;
        overlayRoot.SetActive(false);
    }

    private void CaptureBlurredBackground()
    {
        ReleaseBackground();
        if (!backgroundBlurShader) return;
        if (!blurMaterial) blurMaterial = new Material(backgroundBlurShader) { hideFlags = HideFlags.HideAndDontSave };
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        Texture2D source = null;
        var scratch = RenderTexture.GetTemporary(Mathf.Max(1, width / 2), Mathf.Max(1, height / 2), 0, RenderTextureFormat.ARGB32);
        scratch.filterMode = FilterMode.Bilinear;
        scratch.wrapMode = TextureWrapMode.Clamp;
        backgroundTexture = new RenderTexture(scratch.width, scratch.height, 0, RenderTextureFormat.ARGB32)
        { name = "Pause Gaussian Background", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        backgroundTexture.Create();
        var previous = RenderTexture.active;
        bool previousSRGB = GL.sRGBWrite;
        try
        {
            // Capture display-oriented pixels: direct GPU captures can invert Y when the
            // gameplay camera uses an intermediate target but the menu camera does not.
            source = ScreenCapture.CaptureScreenshotAsTexture();
            source.filterMode = FilterMode.Bilinear; source.wrapMode = TextureWrapMode.Clamp;
            // The screenshot contains display colors, even when its texture format is linear.
            // Copy those bytes without encoding them a second time, then blur in linear space.
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear && source.isDataSRGB;
            Graphics.Blit(source, backgroundTexture);
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < 2; i++)
            {
                blurMaterial.SetVector("_Direction", new Vector4(2.5f / scratch.width, 0, 0, 0));
                Graphics.Blit(backgroundTexture, scratch, blurMaterial);
                blurMaterial.SetVector("_Direction", new Vector4(0, 2.5f / scratch.height, 0, 0));
                Graphics.Blit(scratch, backgroundTexture, blurMaterial);
            }
            blurredBackground.texture = backgroundTexture;
        }
        finally
        {
            RenderTexture.active = previous; GL.sRGBWrite = previousSRGB;
            if (source) Destroy(source);
            RenderTexture.ReleaseTemporary(scratch);
        }
    }

    private void ReleaseBackground()
    {
        if (blurredBackground) blurredBackground.texture = null;
        if (backgroundTexture) { backgroundTexture.Release(); Destroy(backgroundTexture); backgroundTexture = null; }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); return rect;
    }

    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    private void HideImmediate()
    {
        ResetCardMotion();
        clickLift = 0; liftedCardIndex = -1;
        ReleaseBackground();
        view = View.Closed; visibility = 0;
        if (overlayRoot) overlayRoot.SetActive(false);
        if (cardCamera) cardCamera.enabled = false;
        if (stage) stage.gameObject.SetActive(false);
    }

    private void RestoreWorld()
    {
        if (!IsOpen) return;
        foreach (var state in cameraStates) state.Restore(false);
        cameraStates.Clear();
        foreach (var canvas in hiddenCanvases) if (canvas) canvas.enabled = true;
        hiddenCanvases.Clear();
        if (menuOptions) { menuOptions.interactable = previousOptionsInteractable; menuOptions.blocksRaycasts = previousOptionsRaycasts; }
        if (playerLook) playerLook.canLook = previousCanLook;
        if (playerInput) { playerInput.ActionsBlocked = previousActionsBlocked; playerInput.SetPauseInput(previousPauseInput); }
        Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
        if (Mouse.current != null && previousCursorLock != CursorLockMode.Locked) Mouse.current.WarpCursorPosition(previousPointer);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        IsOpen = false;
    }

    private void OnDisable()
    { StopAllCoroutines(); HideImmediate(); RestoreWorld(); }

    private void OnDestroy()
    {
        if (settingsMenus && settingsMenus.receiptControls)
            settingsMenus.receiptControls.ClosedFromPause -= ControlsClosedFromPause;
        RestoreWorld();
        if (outputTexture) { outputTexture.Release(); Destroy(outputTexture); }
        ReleaseBackground();
        if (blurMaterial) Destroy(blurMaterial);
    }
}
