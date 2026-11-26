using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Level victory presentation. Renders live type onto the existing curved label mesh.</summary>
public class BottleResultsScreen : MonoBehaviour
{
    [Header("Level completion (use the real level manager, not the tutorial)")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private RunStats stats;
    [Header("Art")]
    [SerializeField] private GameObject bottlePrefab;
    [SerializeField] private Texture2D emptyLabel;
    [SerializeField] private Texture2D checkStamp;
    [SerializeField] private TMP_FontAsset numberFont;
    [SerializeField] private Shader labelShader;
    [SerializeField] private Shader bottleShader;
    [SerializeField] private Shader celebrationShader;
    [SerializeField] private UnityEngine.Rendering.VolumeProfile filmLookProfile;
    [Header("Motion and pacing (real seconds)")]
    [SerializeField] private float fadeSeconds = .25f;
    [SerializeField] private float countSeconds = .85f;
    [SerializeField] private float rowGap = .16f;
    [SerializeField] private float fastForwardMultiplier = 6f;
    [SerializeField] private float bottleTilt = 9f;
    [SerializeField] private float rockingDegrees = 4f;
    [SerializeField] private float cameraDistance = .127f;
    [SerializeField] private float fieldOfView = 30f;
    [SerializeField, Min(.1f)] private float entranceSeconds = 1.2f;
    [SerializeField, Min(0f)] private float entranceDistance = .36f;
    [Header("Grade thresholds: both time and damage must qualify")]
    [SerializeField] private float gradeAMaxSeconds = 120f;
    [SerializeField] private float gradeAMaxDamage = 10f;
    [SerializeField] private float gradeBMaxSeconds = 240f;
    [SerializeField] private float gradeBMaxDamage = 40f;
    [SerializeField] private float gradeCMaxSeconds = 360f;
    [SerializeField] private float gradeCMaxDamage = 80f;
    [Header("Instant preview: F8 in Editor / Development Build")]
    [SerializeField] private RunResults previewResults = new RunResults
        { seconds = 84f, beersHeld = 8, cocktailsMixed = 2, bruisesEarned = 3, tripsToTap = 3, damageTaken = 8f };
    [Header("Continuation â€” wire the next part here when it exists")]
    public UnityEvent onFinished = new UnityEvent();

    public bool IsShowing { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsFastForwarding { get; private set; }
    public bool IsAwaitingNext { get; private set; }
    public RunResults CurrentResults { get; private set; }
    public int CurrentGrade { get; private set; }

    private const int StageLayer = 30, LabelLayer = 31;
    private readonly Color ink = new Color32(30, 0, 59, 255);
    private GameObject stage, outputRoot, labelRoot;
    private Transform bottlePivot;
    private Vector3 bottleVisualCenter;
    private Camera stageCamera, labelCamera;
    private RenderTexture labelTexture, viewTexture;
    private Material labelMaterial, bottleMaterial, backdropMaterial;
    private TMP_Text[] values;
    private TMP_Text instruction;
    private RectTransform stamp;
    private UnityEngine.UI.Image fade;
    private RawImage bottleView;
    private AudioSource sound;
    private AudioClip tick, land, stampSound;
    private float clock, lastTick, readyAt, shownAt;
    private float entranceProgress = 1f;
    private bool preview, closing, capturedState;
    private float previousTimeScale;
    private bool previousAudioPause, previousInput, previousCanMove;
    private CursorLockMode previousLock;
    private bool previousCursor;
    private InputHandler playerInput;
    private LocomotionController locomotion;
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private readonly List<Material> ownedMaterials = new List<Material>();

    private void OnEnable() { if (levelManager) levelManager.onLevelComplete.AddListener(ShowLevelResults); }
    private void OnDisable()
    {
        if (levelManager) levelManager.onLevelComplete.RemoveListener(ShowLevelResults);
        StopAllCoroutines();
        RestoreWorld();
        IsShowing = false;
        if (stage) stage.SetActive(false);
        if (labelRoot) labelRoot.SetActive(false);
        if (outputRoot) outputRoot.SetActive(false);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            if (!IsShowing) Preview();
            return;
        }
#endif
        if (!IsShowing || closing) return;
        ResizeView();
        clock += Time.unscaledDeltaTime;
        ApplyBottlePose();
        backdropMaterial.SetFloat("_Clock", clock);
        if (Time.unscaledTime - shownAt > .3f && AnyButtonPressed()) Advance();
        if (IsReady) instruction.alpha = .75f + .25f * Mathf.Sin(clock * 3f);
    }

    private void ApplyBottlePose()
    {
        float remaining = 1f - entranceProgress;
        // Use an unwrapped angle for one full spin, easing into the existing idle motion.
        bottlePivot.localRotation = Quaternion.Euler(Mathf.Sin(clock * .6f) * 1.2f - 12f * remaining,
            Mathf.Sin(clock * .7f) * rockingDegrees - 360f * remaining,
            bottleTilt + Mathf.Sin(clock * .55f) * 1.1f - 22f * remaining);
        // Start with the bottle itself centered, then ease the focus onto the label as it arrives.
        Vector3 centerOffset = bottlePivot.localRotation * bottleVisualCenter;
        bottlePivot.localPosition = new Vector3(0f, Mathf.Sin(clock * .85f) * .0012f,
            entranceDistance * remaining) - centerOffset * remaining;
    }

    private static bool AnyButtonPressed()
    {
        // Includes keyboard, all mouse buttons, gamepad buttons/triggers and other button devices.
        foreach (var device in InputSystem.devices)
            foreach (var control in device.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        return false;
    }

    public void ShowLevelResults()
    {
        if (IsShowing || IsAwaitingNext || !stats) return;
        stats.End();
        Show(RunResults.From(stats), false);
    }

    [ContextMenu("Preview Results (Play Mode)")]
    public void Preview()
    {
        if (Application.isPlaying && !IsShowing) Show(previewResults, true);
    }

    public int GradeFor(RunResults result)
    {
        if (result.seconds <= gradeAMaxSeconds && result.damageTaken <= gradeAMaxDamage) return 0;
        if (result.seconds <= gradeBMaxSeconds && result.damageTaken <= gradeBMaxDamage) return 1;
        if (result.seconds <= gradeCMaxSeconds && result.damageTaken <= gradeCMaxDamage) return 2;
        return 3;
    }

    public void Show(RunResults result, bool isPreview = false)
    {
        if (IsShowing) return;
        if (!bottlePrefab || !emptyLabel || !checkStamp || !numberFont || !labelShader || !bottleShader || !celebrationShader)
        { Debug.LogError("Bottle results art references are missing.", this); return; }
        if (!stage) BuildPresentation();
        preview = isPreview;
        CurrentResults = result;
        CurrentGrade = GradeFor(result);
        IsShowing = true; IsReady = false; IsFastForwarding = false; IsAwaitingNext = false;
        closing = false; clock = 0f; shownAt = Time.unscaledTime;
        entranceProgress = 0f; ApplyBottlePose();
        stageCamera.transform.localPosition = new Vector3(0, 0, -cameraDistance);
        CaptureWorld();
        stage.SetActive(true); labelRoot.SetActive(true); outputRoot.SetActive(true);
        foreach (var value in values) { value.text = ""; value.rectTransform.localScale = Vector3.one; }
        stamp.gameObject.SetActive(false);
        instruction.text = "";
        instruction.alpha = 1f;
        fade.color = Color.black;
        StartCoroutine(Reveal());
    }

    public void Advance()
    {
        if (!IsShowing || closing) return;
        if (!IsReady) { IsFastForwarding = true; }
        else if (Time.unscaledTime - readyAt > .25f) StartCoroutine(Close());
    }

    private float Step => Time.unscaledDeltaTime * (IsFastForwarding ? fastForwardMultiplier : 1f);
    private IEnumerator Reveal()
    {
        for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
        { fade.color = new Color(0, 0, 0, 1f - t / fadeSeconds); yield return null; }
        fade.color = Color.clear;
        for (float t = 0; t < entranceSeconds; t += Step)
        {
            float p = Mathf.Clamp01(t / entranceSeconds);
            entranceProgress = p * p * (3f - 2f * p);
            ApplyBottlePose();
            yield return null;
        }
        entranceProgress = 1f;
        ApplyBottlePose();
        float[] targets = { Mathf.Max(0, CurrentResults.seconds), CurrentResults.beersHeld,
            CurrentResults.cocktailsMixed, CurrentResults.bruisesEarned, CurrentResults.tripsToTap };
        for (int row = 0; row < values.Length; row++)
        {
            int previous = -1;
            for (float t = 0; t < countSeconds; t += Step)
            {
                float p = Mathf.Clamp01(t / countSeconds);
                int n = Mathf.FloorToInt(targets[row] * (1 - Mathf.Pow(1 - p, 3)));
                SetValue(row, n);
                if (n != previous && Time.unscaledTime - lastTick > .045f)
                { Play(tick, .12f, .95f + row * .09f + p * .12f); lastTick = Time.unscaledTime; }
                previous = n;
                yield return null;
            }
            SetValue(row, Mathf.FloorToInt(targets[row]));
            Play(land, .22f, 1f + row * .07f);
            for (float t = 0; t < .17f; t += Step)
            { values[row].rectTransform.localScale = Vector3.one * (1f + .17f * Mathf.Sin(t / .17f * Mathf.PI)); yield return null; }
            values[row].rectTransform.localScale = Vector3.one;
            for (float t = 0; t < rowGap; t += Step) yield return null;
        }
        // The supplied stamp includes the box. Its box centre is (22,27) in the 62x49 image.
        Vector2[] boxes = { new Vector2(362, 861), new Vector2(849, 861), new Vector2(362, 929), new Vector2(849, 929) };
        stamp.anchoredPosition = new Vector2(boxes[CurrentGrade].x, -boxes[CurrentGrade].y);
        stamp.gameObject.SetActive(true);
        Play(stampSound, .4f, 1f);
        for (float t = 0; t < .3f; t += Step)
        {
            float p = t / .3f;
            stamp.localScale = Vector3.one * (1f + .9f * Mathf.Pow(1 - p, 2) - .10f * Mathf.Sin(p * Mathf.PI));
            stamp.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-16, 0, p));
            yield return null;
        }
        stamp.localScale = Vector3.one; stamp.localRotation = Quaternion.identity;
        IsReady = true; readyAt = Time.unscaledTime;
        instruction.text = "PRESS ANY BUTTON TO CONTINUE";
    }

    private void SetValue(int row, int n) { values[row].text = row == 0 ? $"{n / 60}:{n % 60:00}" : n.ToString(); }
    private IEnumerator Close()
    {
        closing = true;
        for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
        { fade.color = new Color(0, 0, 0, t / fadeSeconds); yield return null; }
        fade.color = Color.black;
        stage.SetActive(false); labelRoot.SetActive(false);
        IsShowing = false; IsReady = false;
        RestoreWorld();
        if (preview) outputRoot.SetActive(false);
        else
        {
            IsAwaitingNext = true;
            // Keep a black transition screen until the next part is connected.
            instruction.text = "";
            onFinished.Invoke();
        }
        closing = false;
    }

    private void CaptureWorld()
    {
        previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause;
        previousLock = Cursor.lockState; previousCursor = Cursor.visible;
        playerInput = FindFirstObjectByType<InputHandler>();
        locomotion = FindFirstObjectByType<LocomotionController>();
        previousInput = playerInput && playerInput.enabled; previousCanMove = locomotion && locomotion.canMove;
        PlayerParryFeedback.CancelActiveFreeze();
        if (playerInput) playerInput.enabled = false;
        if (locomotion) locomotion.canMove = false;
        hiddenCanvases.Clear();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.enabled && !canvas.transform.IsChildOf(transform)) { hiddenCanvases.Add(canvas); canvas.enabled = false; }
        Time.timeScale = 0; AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        capturedState = true;
    }

    private void RestoreWorld()
    {
        if (!capturedState) return;
        Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousLock; Cursor.visible = previousCursor;
        if (playerInput) playerInput.enabled = previousInput;
        if (locomotion) locomotion.canMove = previousCanMove;
        foreach (var canvas in hiddenCanvases) if (canvas) canvas.enabled = true;
        hiddenCanvases.Clear(); capturedState = false;
    }

    private void BuildPresentation()
    {
        // Separate camera layers and a distant stage keep the bar out of this presentation.
        stage = new GameObject("Results Stage"); stage.transform.SetParent(transform, false);
        stage.transform.position = new Vector3(1000, 1000, 1000);
        var pivot = new GameObject("Floating Bottle Pivot"); pivot.transform.SetParent(stage.transform, false);
        bottlePivot = pivot.transform;
        var bottle = Instantiate(bottlePrefab, bottlePivot);
        bottle.name = "Results Bottle";
        bottle.transform.localPosition = new Vector3(0, -.071f, .02573f);
        bottle.transform.localRotation = Quaternion.identity; bottle.transform.localScale = Vector3.one;
        bottleVisualCenter = bottle.transform.localPosition + bottle.GetComponent<MeshFilter>().sharedMesh.bounds.center;
        foreach (var collider in bottle.GetComponentsInChildren<Collider>()) collider.enabled = false;
        SetLayer(bottle.transform, StageLayer);
        var body = bottle.GetComponent<MeshRenderer>();
        bottleMaterial = new Material(bottleShader);
        bottleMaterial.mainTexture = body.sharedMaterial.mainTexture;
        body.sharedMaterial = bottleMaterial;
        var label = bottle.transform.Find("Nutrition Facts Back Label").GetComponent<MeshRenderer>();
        labelMaterial = new Material(labelShader);
        label.sharedMaterial = labelMaterial;
        stageCamera = MakeCamera("Results Camera", stage.transform, StageLayer);
        var cameraData = stageCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = 1 << StageLayer;
        cameraData.volumeTrigger = stageCamera.transform;
        var filmObject = new GameObject("Level 1 Film Look — Results");
        filmObject.transform.SetParent(stage.transform, false); filmObject.layer = StageLayer;
        var film = filmObject.AddComponent<UnityEngine.Rendering.Volume>();
        film.isGlobal = true; film.weight = 1; film.priority = 5; film.sharedProfile = filmLookProfile;
        stageCamera.fieldOfView = fieldOfView; stageCamera.nearClipPlane = .005f; stageCamera.farClipPlane = 10;
        stageCamera.transform.localPosition = new Vector3(0, 0, -cameraDistance);
        viewTexture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24);
        viewTexture.name = "Results Camera Output"; viewTexture.Create(); stageCamera.targetTexture = viewTexture;
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Moving Celebration Lights"; backdrop.transform.SetParent(stage.transform, false);
        backdrop.layer = StageLayer; backdrop.transform.localPosition = new Vector3(0, 0, .5f);
        backdrop.transform.localScale = Vector3.one * 3; backdrop.GetComponent<Collider>().enabled = false;
        backdropMaterial = new Material(celebrationShader); backdrop.GetComponent<MeshRenderer>().sharedMaterial = backdropMaterial;
        BuildLabel();
        BuildOutput();
        sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false;
        sound.ignoreListenerPause = true; sound.spatialBlend = 0;
        tick = Tone("Results tick", 640, .026f); land = Tone("Results row landing", 880, .085f);
        stampSound = Tone("Results stamp", 150, .18f);
    }

    private void BuildLabel()
    {
        labelRoot = new GameObject("Live Label Studio"); labelRoot.transform.SetParent(transform, false);
        labelRoot.transform.position = new Vector3(2000, 2000, 2000);
        labelCamera = MakeCamera("Label Camera", labelRoot.transform, LabelLayer);
        labelCamera.orthographic = true; labelCamera.orthographicSize = emptyLabel.height * .5f;
        labelCamera.nearClipPlane = .1f; labelCamera.farClipPlane = 20;
        labelTexture = new RenderTexture(emptyLabel.width, emptyLabel.height, 24);
        labelTexture.name = "Live Performance Facts"; labelTexture.filterMode = FilterMode.Point; labelTexture.Create();
        labelCamera.targetTexture = labelTexture; labelMaterial.mainTexture = labelTexture;
        var canvasObject = new GameObject("Performance Facts", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(labelRoot.transform, false); canvasObject.layer = LabelLayer;
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = labelCamera; canvas.planeDistance = 1;
        var background = Raw("Empty Label", canvasObject.transform, emptyLabel);
        Stretch(background.rectTransform);
        values = new TMP_Text[5];
        // Image-space coordinates match the example artwork; 42pt produces 24px-high old-style digits.
        float[] centres = { 489, 545, 601, 657, 713 };
        for (int i = 0; i < 5; i++)
        {
            var text = Text("Result " + i, canvasObject.transform, 42, ink);
            Place(text.rectTransform, 685, centres[i] - 25, 185, 50);
            text.alignment = TextAlignmentOptions.MidlineRight;
            values[i] = text;
        }
        var check = Raw("Grade Stamp", canvasObject.transform, checkStamp);
        stamp = check.rectTransform;
        stamp.anchorMin = stamp.anchorMax = new Vector2(0, 1);
        stamp.pivot = new Vector2(22f / 62f, 1f - 27f / 49f);
        stamp.sizeDelta = new Vector2(62, 49) * 1.18f;
        stamp.gameObject.SetActive(false);
    }

    private void BuildOutput()
    {
        outputRoot = new GameObject("Results Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        outputRoot.transform.SetParent(transform, false);
        var canvas = outputRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = outputRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        bottleView = Raw("Bottle View", outputRoot.transform, viewTexture); Stretch(bottleView.rectTransform);
        instruction = Text("Continue Prompt", outputRoot.transform, 28, new Color32(255, 239, 202, 255));
        instruction.rectTransform.anchorMin = instruction.rectTransform.anchorMax = new Vector2(.5f, 0);
        instruction.rectTransform.pivot = new Vector2(.5f, 0); instruction.rectTransform.anchoredPosition = new Vector2(0, 14);
        instruction.rectTransform.sizeDelta = new Vector2(1400, 50); instruction.alignment = TextAlignmentOptions.Center;
        var curtain = new GameObject("Transition Fade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        curtain.transform.SetParent(outputRoot.transform, false); fade = curtain.GetComponent<UnityEngine.UI.Image>();
        fade.raycastTarget = false; Stretch(fade.rectTransform);
    }

    private Camera MakeCamera(string objectName, Transform parent, int layer)
    {
        var obj = new GameObject(objectName, typeof(Camera)); obj.transform.SetParent(parent, false);
        var camera = obj.GetComponent<Camera>(); camera.cullingMask = 1 << layer;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.allowHDR = false; camera.allowMSAA = false;
        return camera;
    }
    private RawImage Raw(string objectName, Transform parent, Texture texture)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(RawImage)); obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer; var raw = obj.GetComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false; return raw;
    }
    private TMP_Text Text(string objectName, Transform parent, float size, Color color)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        var text = obj.GetComponent<TextMeshProUGUI>(); text.font = numberFont; text.fontSize = size; text.color = color;
        var material = new Material(numberFont.material); material.SetFloat("_OutlineWidth", 0); material.SetFloat("_FaceDilate", 0);
        material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER"); material.DisableKeyword("OUTLINE_ON");
        material.SetColor("_UnderlayColor", Color.clear);
        material.SetColor("_FaceColor", Color.white); ownedMaterials.Add(material); text.fontSharedMaterial = material;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void SetLayer(Transform root, int layer)
    { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer); }
    private static AudioClip Tone(string clipName, float frequency, float duration)
    {
        const int rate = 22050;
        var samples = new float[Mathf.CeilToInt(rate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate, envelope = Mathf.Pow(1f - (float)i / samples.Length, 2);
            samples[i] = (Mathf.Sin(t * frequency * Mathf.PI * 2) + .22f * Mathf.Sin(t * frequency * Mathf.PI * 4)) * envelope * .45f;
        }
        var clip = AudioClip.Create(clipName, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
    }
    private void Play(AudioClip clip, float volume, float pitch) { sound.pitch = pitch; sound.PlayOneShot(clip, volume); }
    private void ResizeView()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (viewTexture.width == width && viewTexture.height == height) return;
        stageCamera.targetTexture = null;
        viewTexture.Release(); Destroy(viewTexture);
        viewTexture = new RenderTexture(width, height, 24) { name = "Results Camera Output" };
        viewTexture.Create(); stageCamera.targetTexture = viewTexture; bottleView.texture = viewTexture;
    }
    private void OnDestroy()
    {
        RestoreWorld();
        if (stageCamera) stageCamera.targetTexture = null;
        if (labelCamera) labelCamera.targetTexture = null;
        if (labelTexture) { labelTexture.Release(); Destroy(labelTexture); }
        if (viewTexture) { viewTexture.Release(); Destroy(viewTexture); }
        if (labelMaterial) Destroy(labelMaterial); if (bottleMaterial) Destroy(bottleMaterial); if (backdropMaterial) Destroy(backdropMaterial);
        foreach (var material in ownedMaterials) if (material) Destroy(material);
        if (tick) Destroy(tick); if (land) Destroy(land); if (stampSound) Destroy(stampSound);
    }
}
