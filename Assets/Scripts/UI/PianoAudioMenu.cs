using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>World-space piano controls and a short camera trip from the main menu.</summary>
public sealed class PianoAudioMenu : MonoBehaviour
{
    public GreenWellMenu mainMenu;
    public Camera menuCamera;
    public CinemachineBrain cameraBrain;
    public Transform audioView;
    public CanvasGroup panel;
    public UnityEngine.UI.Slider musicSlider, sfxSlider;
    public UnityEngine.UI.Button backButton, openButton;
    [Min(0.05f)] public float travelSeconds = 0.45f;
    public bool IsOpen { get; private set; }
    public bool IsTransitioning { get; private set; }
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float homeFov, homeNear;
    private bool brainWasEnabled;
    private Coroutine travel;

    private void Awake()
    {
        panel.interactable = panel.blocksRaycasts = false;
        musicSlider.SetValueWithoutNotify(AudioVolumeChannel.MusicVolume);
        sfxSlider.SetValueWithoutNotify(AudioVolumeChannel.SfxVolume);
        musicSlider.onValueChanged.AddListener(AudioVolumeChannel.SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(AudioVolumeChannel.SetSfxVolume);
        backButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
    }

    public void Open()
    {
        if (IsOpen || IsTransitioning || !menuCamera || !audioView) return;
        homePosition = menuCamera.transform.position;
        homeRotation = menuCamera.transform.rotation;
        homeFov = menuCamera.fieldOfView;
        homeNear = menuCamera.nearClipPlane;
        brainWasEnabled = cameraBrain && cameraBrain.enabled;
        if (cameraBrain) cameraBrain.enabled = false;
        menuCamera.nearClipPlane = 0.03f;
        mainMenu.options.SetActive(false);
        IsOpen = true;
        BeginTravel(audioView.position, audioView.rotation, 60f, true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        PlayerPrefs.Save();
        panel.interactable = panel.blocksRaycasts = false;
        BeginTravel(homePosition, homeRotation, homeFov, false);
    }

    private void BeginTravel(Vector3 position, Quaternion rotation, float fov, bool opening)
    {
        if (travel != null) StopCoroutine(travel);
        travel = StartCoroutine(MoveCamera(position, rotation, fov, opening));
    }

    private IEnumerator MoveCamera(Vector3 position, Quaternion rotation, float fov, bool opening)
    {
        IsTransitioning = true;
        panel.interactable = panel.blocksRaycasts = false;
        var startPosition = menuCamera.transform.position;
        var startRotation = menuCamera.transform.rotation;
        float startFov = menuCamera.fieldOfView;
        for (float time = 0f; time < travelSeconds; time += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / travelSeconds);
            menuCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, position, t), Quaternion.Slerp(startRotation, rotation, t));
            menuCamera.fieldOfView = Mathf.Lerp(startFov, fov, t);
            yield return null;
        }
        menuCamera.transform.SetPositionAndRotation(position, rotation);
        menuCamera.fieldOfView = fov;
        panel.interactable = panel.blocksRaycasts = opening;
        if (!opening)
        {
            menuCamera.nearClipPlane = homeNear;
            if (cameraBrain) cameraBrain.enabled = brainWasEnabled;
            mainMenu.options.SetActive(true);
        }
        IsTransitioning = false;
        travel = null;
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(opening ? musicSlider.gameObject : openButton.gameObject);
    }

    private void OnDestroy()
    {
        if (musicSlider) musicSlider.onValueChanged.RemoveListener(AudioVolumeChannel.SetMusicVolume);
        if (sfxSlider) sfxSlider.onValueChanged.RemoveListener(AudioVolumeChannel.SetSfxVolume);
        if (backButton) backButton.onClick.RemoveListener(Close);
    }
}
