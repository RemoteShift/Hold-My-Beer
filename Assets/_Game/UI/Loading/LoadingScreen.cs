using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A minimal loading display with an F7 preview that does not change game state.</summary>
[DefaultExecutionOrder(500)]
[RequireComponent(typeof(Canvas))]
public sealed class LoadingScreen : MonoBehaviour
{
    [SerializeField] private RectTransform loadingCircle;
    [SerializeField, Min(0f)] private float clockwiseDegreesPerSecond = 60f;
    [SerializeField] private UnityEngine.UI.Graphic[] filmGraphics;

    public static LoadingScreen Instance { get; private set; }
    public bool IsShowing { get; private set; }
    private Canvas display;
    private float angle;
    private Material[] runtimeFilmMaterials;
    private static readonly int FilmTime = Shader.PropertyToID("_FilmTime");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance) return;
        var prefab = Resources.Load<GameObject>("Loading Screen");
        if (prefab) Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        display = GetComponent<Canvas>();
        display.enabled = false;
        runtimeFilmMaterials = new Material[filmGraphics == null ? 0 : filmGraphics.Length];
        for (int i = 0; i < runtimeFilmMaterials.Length; i++)
        {
            var graphic = filmGraphics[i];
            if (!graphic || !graphic.material) continue;
            var material = new Material(graphic.material) { name = "Loading Film (Runtime)" };
            runtimeFilmMaterials[i] = material;
            graphic.material = material;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
            TogglePreview();

        if (!IsShowing || !loadingCircle) return;
        foreach (var material in runtimeFilmMaterials)
            if (material) material.SetFloat(FilmTime, Time.unscaledTime);
        angle = Mathf.Repeat(angle - clockwiseDegreesPerSecond * Time.unscaledDeltaTime, 360f);
        loadingCircle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void LateUpdate()
    {
        // Keep the preview available over menus that manage other screen-space Canvases.
        display.enabled = IsShowing;
    }

    public void TogglePreview() => SetVisible(!IsShowing);
    public void Show() => SetVisible(true);
    public void Hide() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (!display) return;
        if (visible && !IsShowing)
        {
            angle = 0f;
            if (loadingCircle) loadingCircle.localRotation = Quaternion.identity;
        }
        IsShowing = visible;
        display.enabled = visible;
    }

    private void OnDestroy()
    {
        if (runtimeFilmMaterials != null)
            foreach (var material in runtimeFilmMaterials)
                if (material) Destroy(material);
        if (Instance == this) Instance = null;
    }
}
