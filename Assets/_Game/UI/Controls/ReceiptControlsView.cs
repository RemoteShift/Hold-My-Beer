using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEngine.EventSystems;

/// <summary>The physical receipt stays on the bar; only its Back cell receives input.</summary>
public sealed class ReceiptControlsView : MonoBehaviour
{
    public GreenWellMenu menu;
    public CinemachineCamera receiptCamera;
    public Canvas receiptCanvas;
    public CanvasGroup interaction;
    public Button backButton;
    public bool IsOpen { get; private set; }
    public static Camera ReadableReceiptCamera { get; private set; }

    private bool returning;
    private int openedFrame;
    private CanvasGroup menuOptions;
    private bool menuWasInteractable, menuBlockedRaycasts;
    private CinemachineBlendDefinition previousBlend;
    private bool blendOverridden;

    private void Awake()
    {
        backButton.onClick.AddListener(Close);
        SetInteraction(false);
        receiptCamera.Priority = 5;
    }

    public void Open()
    {
        if (IsOpen || returning || menu.cameraBrain.IsBlending) return;
        IsOpen = true;
        ReadableReceiptCamera = menu.menuCamera;
        openedFrame = Time.frameCount;
        menuOptions = menu.options.GetComponent<CanvasGroup>();
        if (!menuOptions) menuOptions = menu.options.AddComponent<CanvasGroup>();
        menuWasInteractable = menuOptions.interactable;
        menuBlockedRaycasts = menuOptions.blocksRaycasts;
        menuOptions.interactable = menuOptions.blocksRaycasts = false;
        previousBlend = menu.cameraBrain.DefaultBlend;
        blendOverridden = true;
        menu.cameraBrain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            menu.audioMenu ? menu.audioMenu.travelSeconds : .45f);
        menu.controls.SetActive(false);
        menu.quit.SetActive(false);
        receiptCanvas.worldCamera = menu.menuCamera;
        SetInteraction(false);
        receiptCamera.Priority = 30;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public void Close()
    {
        if (!IsOpen || menu.cameraBrain.IsBlending) return;
        IsOpen = false;
        returning = true;
        openedFrame = Time.frameCount;
        SetInteraction(false);
        receiptCamera.Priority = 5;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    private void LateUpdate()
    {
        if (Time.frameCount <= openedFrame + 1 || menu.cameraBrain.IsBlending) return;
        if (IsOpen) SetInteraction(true);
        else if (returning)
        {
            returning = false;
            ReadableReceiptCamera = null;
            menuOptions.interactable = menuWasInteractable;
            menuOptions.blocksRaycasts = menuBlockedRaycasts;
            RestoreBlend();
        }
    }

    private void SetInteraction(bool active)
    {
        interaction.interactable = active;
        interaction.blocksRaycasts = active;
    }

    private void OnDestroy()
    {
        RestoreBlend();
        if (menu && ReadableReceiptCamera == menu.menuCamera) ReadableReceiptCamera = null;
        if (backButton) backButton.onClick.RemoveListener(Close);
    }

    private void RestoreBlend()
    {
        if (!blendOverridden || !menu || !menu.cameraBrain) return;
        menu.cameraBrain.DefaultBlend = previousBlend;
        blendOverridden = false;
    }
}
