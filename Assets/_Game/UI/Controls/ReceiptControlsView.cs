using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEngine.EventSystems;

/// <summary>The physical receipt stays on the bar; its Back cell and sensitivity slider receive input.</summary>
public sealed class ReceiptControlsView : MonoBehaviour
{
    public SharedSettingsMenus sharedMenus;
    public CinemachineCamera receiptCamera;
    public Canvas receiptCanvas;
    public CanvasGroup interaction;
    public Button backButton;
    public UnityEngine.UI.Slider sensitivitySlider;
    public bool IsOpen { get; private set; }
    public bool IsTransitioning => returning || pauseTravel != null || (!openedFromPause && IsOpen && sharedMenus && sharedMenus.cameraBrain && sharedMenus.cameraBrain.IsBlending);
    public static Camera ReadableReceiptCamera { get; private set; }
    public event System.Action ClosedFromPause;

    private bool returning;
    private int openedFrame;
    private CanvasGroup menuOptions;
    private bool menuWasInteractable, menuBlockedRaycasts;
    private CinemachineBlendDefinition previousBlend;
    private bool blendOverridden;
    private bool openedFromPause;
    private Coroutine pauseTravel;
    private Vector3 pauseHomePosition;
    private Quaternion pauseHomeRotation;
    private float pauseHomeFov, pauseHomeNear, pauseHomeFar;

    private void Awake()
    {
        backButton.onClick.AddListener(Close);
        if (sensitivitySlider)
        {
            sensitivitySlider.SetValueWithoutNotify(MouseSensitivitySettings.NormalizedValue);
            sensitivitySlider.onValueChanged.AddListener(MouseSensitivitySettings.SetNormalizedValue);
        }
        SetInteraction(false);
        receiptCamera.Priority = 5;
    }

    public void Open() => Open(false);

    public void OpenFromPause() => Open(true);

    private void Open(bool fromPause)
    {
        if (IsOpen || returning || (!fromPause && sharedMenus.cameraBrain.IsBlending)) return;
        openedFromPause = fromPause;
        IsOpen = true;
        if (sensitivitySlider) sensitivitySlider.SetValueWithoutNotify(MouseSensitivitySettings.NormalizedValue);
        ReadableReceiptCamera = sharedMenus.menuCamera;
        openedFrame = Time.frameCount;
        menuOptions = sharedMenus.mainMenuOptions;
        if (menuOptions)
        {
            menuWasInteractable = menuOptions.interactable;
            menuBlockedRaycasts = menuOptions.blocksRaycasts;
            menuOptions.interactable = menuOptions.blocksRaycasts = false;
        }
        receiptCanvas.worldCamera = sharedMenus.menuCamera;
        if (fromPause)
        {
            // Move between the player and receipt views, without waking the title-menu brain.
            sharedMenus.cameraBrain.enabled = false;
            var camera = sharedMenus.menuCamera;
            pauseHomePosition = camera.transform.position;
            pauseHomeRotation = camera.transform.rotation;
            pauseHomeFov = camera.fieldOfView;
            pauseHomeNear = camera.nearClipPlane;
            pauseHomeFar = camera.farClipPlane;
            var lens = receiptCamera.Lens;
            SetInteraction(false);
            pauseTravel = StartCoroutine(MoveFromPause(receiptCamera.transform.position,
                receiptCamera.transform.rotation * Quaternion.Euler(0, 0, lens.Dutch),
                lens.FieldOfView, lens.NearClipPlane, lens.FarClipPlane, true));
        }
        else
        {
            previousBlend = sharedMenus.cameraBrain.DefaultBlend;
            blendOverridden = true;
            sharedMenus.cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                sharedMenus.audioMenu ? sharedMenus.audioMenu.travelSeconds : .45f);
            SetInteraction(false);
            receiptCamera.Priority = 30;
        }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public void Close()
    {
        if (!IsOpen || IsTransitioning) return;
        IsOpen = false;
        MouseSensitivitySettings.Save();
        returning = true;
        openedFrame = Time.frameCount;
        SetInteraction(false);
        receiptCamera.Priority = 5;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (openedFromPause)
        {
            pauseTravel = StartCoroutine(MoveFromPause(pauseHomePosition, pauseHomeRotation,
                pauseHomeFov, pauseHomeNear, pauseHomeFar, false));
        }
    }

    private IEnumerator MoveFromPause(Vector3 position, Quaternion rotation, float fov,
        float nearClip, float farClip, bool opening)
    {
        var camera = sharedMenus.menuCamera;
        var startPosition = camera.transform.position;
        var startRotation = camera.transform.rotation;
        float startFov = camera.fieldOfView, startNear = camera.nearClipPlane, startFar = camera.farClipPlane;
        float duration = Mathf.Max(.05f, sharedMenus.audioMenu ? sharedMenus.audioMenu.travelSeconds : .45f);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, position, t),
                Quaternion.Slerp(startRotation, rotation, t));
            camera.fieldOfView = Mathf.Lerp(startFov, fov, t);
            camera.nearClipPlane = Mathf.Lerp(startNear, nearClip, t);
            camera.farClipPlane = Mathf.Lerp(startFar, farClip, t);
            yield return null;
        }
        camera.transform.SetPositionAndRotation(position, rotation);
        camera.fieldOfView = fov; camera.nearClipPlane = nearClip; camera.farClipPlane = farClip;
        pauseTravel = null;
        if (opening) SetInteraction(true);
        else
        {
            openedFromPause = false;
            FinishReturn();
            camera.enabled = false;
            var listener = camera.GetComponent<AudioListener>();
            if (listener) listener.enabled = false;
            ClosedFromPause?.Invoke();
        }
    }

    private void LateUpdate()
    {
        if (openedFromPause) return;
        if (Time.frameCount <= openedFrame + 1 || sharedMenus.cameraBrain.IsBlending) return;
        if (IsOpen) SetInteraction(true);
        else if (returning)
        {
            FinishReturn();
        }
    }

    private void FinishReturn()
    {
        returning = false;
        ReadableReceiptCamera = null;
        if (menuOptions)
        {
            menuOptions.interactable = menuWasInteractable;
            menuOptions.blocksRaycasts = menuBlockedRaycasts;
        }
        RestoreBlend();
    }

    private void SetInteraction(bool active)
    {
        interaction.interactable = active;
        interaction.blocksRaycasts = active;
    }

    private void OnDestroy()
    {
        RestoreBlend();
        if (sharedMenus && ReadableReceiptCamera == sharedMenus.menuCamera) ReadableReceiptCamera = null;
        if (backButton) backButton.onClick.RemoveListener(Close);
        if (sensitivitySlider) sensitivitySlider.onValueChanged.RemoveListener(MouseSensitivitySettings.SetNormalizedValue);
    }

    private void RestoreBlend()
    {
        if (!blendOverridden || !sharedMenus || !sharedMenus.cameraBrain) return;
        sharedMenus.cameraBrain.DefaultBlend = previousBlend;
        blendOverridden = false;
    }
}
