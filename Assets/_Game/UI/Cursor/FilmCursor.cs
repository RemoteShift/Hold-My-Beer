using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Screen-pixel cursor rendered through the active scene's film settings.</summary>
[DefaultExecutionOrder(10000)]
public sealed class FilmCursor : MonoBehaviour
{
    public const int CursorLayer = 28;
    public Texture2D artwork;
    public Shader artworkShader;
    public Vector2 hotspot = new Vector2(9, 6);
    private static FilmCursor instance;
    private Camera cursorCamera;
    private UniversalAdditionalCameraData cameraData;
    private UnityEngine.UI.RawImage pointer;
    private RenderTexture output;
    private Texture2D transparentCursor;
    private Material artworkMaterial;
    private Mesh quadMesh;
    private PauseCardMenu pauseMenu;
    private SharedSettingsMenus settingsMenus;
    private bool captureHidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance) return;
        var prefab = Resources.Load<GameObject>("Film Cursor");
        if (prefab) Instantiate(prefab);
    }

    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; canvas.pixelPerfect = true;
        var image = new GameObject("Film Cursor Pointer", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        image.transform.SetParent(transform, false);
        pointer = image.GetComponent<UnityEngine.UI.RawImage>(); pointer.raycastTarget = false;
        var rect = pointer.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(hotspot.x / 32f, 1 - hotspot.y / 32f);
        rect.sizeDelta = new Vector2(32, 32);
        pointer.enabled = false;

        var stage = new GameObject("Cursor Film Stage"); stage.transform.SetParent(transform, false);
        stage.transform.position = new Vector3(0, -6500, 0); stage.layer = CursorLayer;
        var cameraObject = new GameObject("Cursor Film Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -5);
        cursorCamera = cameraObject.GetComponent<Camera>();
        cursorCamera.orthographic = true; cursorCamera.orthographicSize = 16;
        cursorCamera.nearClipPlane = .01f; cursorCamera.farClipPlane = 10;
        cursorCamera.cullingMask = 1 << CursorLayer; cursorCamera.depth = 10000;
        cursorCamera.clearFlags = CameraClearFlags.SolidColor; cursorCamera.backgroundColor = Color.clear;
        cursorCamera.allowMSAA = false; cursorCamera.enabled = false;
        cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true; cameraData.renderShadows = false;
        cameraData.requiresColorOption = cameraData.requiresDepthOption = CameraOverrideOption.Off;
        output = new RenderTexture(32, 32, 16, RenderTextureFormat.ARGB32)
        { name = "Film Cursor Output", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        output.Create(); cursorCamera.targetTexture = output; pointer.texture = output;

        quadMesh = new Mesh { name = "Cursor Artwork Quad" };
        quadMesh.vertices = new[] { new Vector3(-16,-16,0), new Vector3(16,-16,0), new Vector3(16,16,0), new Vector3(-16,16,0) };
        quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quadMesh.RecalculateBounds();
        var model = new GameObject("Cursor Artwork", typeof(MeshFilter), typeof(MeshRenderer));
        model.layer = CursorLayer; model.transform.SetParent(stage.transform, false);
        model.GetComponent<MeshFilter>().sharedMesh = quadMesh;
        artworkMaterial = new Material(artworkShader) { name = "Cursor Original Artwork", hideFlags = HideFlags.HideAndDontSave };
        artworkMaterial.mainTexture = artwork;
        var renderer = model.GetComponent<MeshRenderer>(); renderer.sharedMaterial = artworkMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;

        transparentCursor = new Texture2D(32, 32, TextureFormat.RGBA32, false) { alphaIsTransparency = true };
        transparentCursor.SetPixels32(new Color32[32 * 32]); transparentCursor.Apply(false, false);
        Cursor.SetCursor(transparentCursor, Vector2.zero, CursorMode.Auto);
        SceneManager.sceneLoaded += SceneLoaded;
        RefreshSources();
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSources();

    private void RefreshSources()
    {
        pauseMenu = FindFirstObjectByType<PauseCardMenu>(FindObjectsInactive.Include);
        settingsMenus = FindFirstObjectByType<SharedSettingsMenus>(FindObjectsInactive.Include);
    }

    private Camera SourceCamera()
    {
        if (pauseMenu && pauseMenu.cardCamera.enabled && pauseMenu.cardCamera.gameObject.activeInHierarchy) return pauseMenu.cardCamera;
        if (settingsMenus && settingsMenus.menuCamera.enabled && settingsMenus.menuCamera.gameObject.activeInHierarchy) return settingsMenus.menuCamera;
        var main = Camera.main;
        if (main && main.enabled && main.gameObject.activeInHierarchy) return main;
        Camera source = null;
        foreach (var candidate in Camera.allCameras)
            if (candidate != cursorCamera && !candidate.targetTexture && (!source || candidate.depth > source.depth)) source = candidate;
        return source;
    }

    private void LateUpdate() => UpdatePointer();

    private void UpdatePointer()
    {
        if (!pointer || !cursorCamera) return;
        var mouse = Mouse.current;
        var position = mouse != null ? mouse.position.ReadValue() : new Vector2(-1, -1);
        bool visible = !captureHidden && mouse != null && mouse.enabled && Cursor.visible && Cursor.lockState != CursorLockMode.Locked &&
            position.x >= 0 && position.y >= 0 && position.x <= Screen.width && position.y <= Screen.height;
        pointer.enabled = cursorCamera.enabled = visible;
        if (!visible) return;
        pointer.rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        var source = SourceCamera();
        var sourceData = source ? source.GetComponent<UniversalAdditionalCameraData>() : null;
        cameraData.renderPostProcessing = sourceData && sourceData.renderPostProcessing;
        cameraData.volumeLayerMask = sourceData ? sourceData.volumeLayerMask : (LayerMask)1;
        cameraData.volumeTrigger = sourceData && sourceData.volumeTrigger ? sourceData.volumeTrigger : source ? source.transform : transform;
        cursorCamera.allowHDR = source && source.allowHDR;
    }

    public static void SetCaptureHidden(bool hidden)
    {
        if (!instance) return;
        instance.captureHidden = hidden; instance.UpdatePointer();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused && transparentCursor) Cursor.SetCursor(transparentCursor, Vector2.zero, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        SceneManager.sceneLoaded -= SceneLoaded;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (output) { output.Release(); Destroy(output); }
        if (transparentCursor) Destroy(transparentCursor);
        if (artworkMaterial) Destroy(artworkMaterial);
        if (quadMesh) Destroy(quadMesh);
    }
}
