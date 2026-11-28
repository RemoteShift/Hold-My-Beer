using UnityEngine;
using Unity.Cinemachine;

/// <summary>Shared world-space settings that remain active independently of the title menu.</summary>
public sealed class SharedSettingsMenus : MonoBehaviour
{
    public Camera mainMenuCamera;
    public Camera menuCamera;
    public CinemachineBrain cameraBrain;
    public PianoAudioMenu audioMenu;
    public ReceiptControlsView receiptControls;
    [Tooltip("Optional: blocks title-menu interactions while shared settings are open.")]
    public CanvasGroup mainMenuOptions;
    private bool returningToMainMenu;
    private CinemachineBrain mainMenuBrain;
    private bool mainMenuBrainWasEnabled;

    public void OpenAudioFromMainMenu()
    {
        BeginMainMenuVisit();
        audioMenu.Open();
    }

    public void OpenControlsFromMainMenu()
    {
        BeginMainMenuVisit();
        receiptControls.Open();
    }

    private void BeginMainMenuVisit()
    {
        if (returningToMainMenu || !mainMenuCamera) return;
        returningToMainMenu = true;
        mainMenuBrain = mainMenuCamera.GetComponent<CinemachineBrain>();
        mainMenuBrainWasEnabled = mainMenuBrain && mainMenuBrain.enabled;
        // Only the visible settings brain should follow the receipt-camera priorities.
        if (mainMenuBrain) mainMenuBrain.enabled = false;
        menuCamera.transform.SetPositionAndRotation(mainMenuCamera.transform.position, mainMenuCamera.transform.rotation);
        menuCamera.fieldOfView = mainMenuCamera.fieldOfView;
        menuCamera.nearClipPlane = mainMenuCamera.nearClipPlane;
        menuCamera.farClipPlane = mainMenuCamera.farClipPlane;
        SetCameraActive(mainMenuCamera, false);
        SetCameraActive(menuCamera, true);
    }

    private void LateUpdate()
    {
        if (!returningToMainMenu || audioMenu.IsOpen || audioMenu.IsTransitioning ||
            receiptControls.IsOpen || receiptControls.IsTransitioning) return;
        returningToMainMenu = false;
        // Hand over the completed view before reactivating the title camera. Its brain
        // must choose the chalkboard afresh rather than resume a separate, longer blend.
        mainMenuCamera.transform.SetPositionAndRotation(menuCamera.transform.position, menuCamera.transform.rotation);
        mainMenuCamera.fieldOfView = menuCamera.fieldOfView;
        mainMenuCamera.nearClipPlane = menuCamera.nearClipPlane;
        mainMenuCamera.farClipPlane = menuCamera.farClipPlane;
        if (mainMenuBrain)
        {
            mainMenuBrain.ResetState();
            mainMenuBrain.enabled = mainMenuBrainWasEnabled;
        }
        SetCameraActive(menuCamera, false);
        SetCameraActive(mainMenuCamera, true);
    }

    private static void SetCameraActive(Camera camera, bool active)
    {
        if (!camera) return;
        camera.enabled = active;
        var listener = camera.GetComponent<AudioListener>();
        if (listener) listener.enabled = active;
    }
}
