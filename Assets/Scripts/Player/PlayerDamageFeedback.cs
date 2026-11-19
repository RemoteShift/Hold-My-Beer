using UnityEngine;

/// <summary>Camera impact and screen-edge health feedback, independent of HUD widgets.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Camera playerCamera;
    [Header("Damage impact")]
    [SerializeField, Range(0f, 2f)] private float shakeDegrees = 0.65f;
    [SerializeField, Range(0f, 0.05f)] private float shakeDistance = 0.012f;
    [SerializeField, Min(0.05f)] private float shakeDuration = 0.25f;
    [SerializeField, Range(0f, 3f)] private float impactZoom = 1.1f;
    [SerializeField, Range(0f, 0.2f)] private float redFlashOpacity = 0.055f;
    [SerializeField, Min(0.1f)] private float flashDuration = 0.45f;
    [Header("Low health")]
    [SerializeField, Range(0.05f, 0.5f)] private float lowHealthThreshold = 0.3f;
    [SerializeField, Range(0f, 0.4f)] private float lowHealthEdgeOpacity = 0.18f;
    [SerializeField, Range(0f, 1f)] private float breathingZoom = 0.25f;

    private Transform cameraTransform;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float restFov, previousHealth, fraction = 1f;
    private float impact, impactAge = 10f, lowBlend, pulseClock;
    private float edgeAlpha, flashAlpha;
    private Texture2D edgeTexture;
    private bool ready;

    private void OnEnable()
    {
        if (!health || !playerCamera) return;
        cameraTransform = playerCamera.transform;
        restPosition = cameraTransform.localPosition;
        restRotation = cameraTransform.localRotation;
        restFov = playerCamera.fieldOfView;
        previousHealth = health.CurrentHealth;
        fraction = health.MaxHealth > 0f ? Mathf.Clamp01(previousHealth / health.MaxHealth) : 0f;
        health.OnHealthChanged += OnHealthChanged;
        ready = true;
        CreateEdgeTexture();
    }

    private void Start()
    {
        if (!ready) return;
        previousHealth = health.CurrentHealth;
        fraction = health.MaxHealth > 0f ? Mathf.Clamp01(previousHealth / health.MaxHealth) : 0f;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float lost = Mathf.Max(0f, previousHealth - current);
        fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (current <= 0f)
        {
            ClearEffects();
        }
        else if (lost > 0f && maximum > 0f)
        {
            impact = Mathf.Max(impact * Mathf.Clamp01(1f - impactAge / flashDuration),
                Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(lost / maximum * 3f)));
            impactAge = 0f;
        }
        else if (current > previousHealth)
        {
            // Healing removes the hit flash; low-health edges ease out.
            impact = 0f;
            impactAge = flashDuration;
        }
        previousHealth = current;
    }

    private void LateUpdate() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        if (!ready || dt <= 0f) return;
        if (!playerCamera.isActiveAndEnabled || !health || health.IsDead)
        {
            ClearEffects();
            return;
        }
        impactAge += dt;
        float severity = fraction > 0f && fraction < lowHealthThreshold
            ? 1f - fraction / lowHealthThreshold : 0f;
        lowBlend = Mathf.MoveTowards(lowBlend, severity, dt * 2f);
        pulseClock += dt * Mathf.Lerp(0.85f, 1.35f, lowBlend) * Mathf.PI * 2f;
        float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(pulseClock), 3f);
        float shake = impact * Mathf.Pow(Mathf.Clamp01(1f - impactAge / shakeDuration), 2f);
        float fade = impact * Mathf.Pow(Mathf.Clamp01(1f - impactAge / flashDuration), 2f);
        float x = Mathf.Sin(impactAge * 93f);
        float y = Mathf.Sin(impactAge * 117f + 1f);
        cameraTransform.localPosition = restPosition + new Vector3(x, y, 0f) * (shakeDistance * shake);
        cameraTransform.localRotation = restRotation * Quaternion.Euler(
            y * shakeDegrees * shake, x * shakeDegrees * shake * 0.65f,
            x * shakeDegrees * shake * 0.35f);
        playerCamera.fieldOfView = restFov - impactZoom * fade - breathingZoom * lowBlend * pulse;
        flashAlpha = redFlashOpacity * fade;
        edgeAlpha = Mathf.Clamp01(0.24f * fade + lowHealthEdgeOpacity * lowBlend * (0.4f + 0.6f * pulse));
    }

    private void OnGUI()
    {
        if (!ready || !playerCamera || !playerCamera.isActiveAndEnabled ||
            !health || health.IsDead || Event.current.type != EventType.Repaint) return;
        Color before = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -80;
        Rect rect = playerCamera.pixelRect;
        rect.y = Screen.height - rect.yMax;
        if (flashAlpha > 0.001f)
        {
            GUI.color = new Color(0.65f, 0.035f, 0.02f, flashAlpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
        if (edgeAlpha > 0.001f && edgeTexture)
        {
            GUI.color = new Color(0.65f, 0.025f, 0.015f, edgeAlpha);
            GUI.DrawTexture(rect, edgeTexture, ScaleMode.StretchToFill, true);
        }
        GUI.color = before;
        GUI.depth = oldDepth;
    }

    private void CreateEdgeTexture()
    {
        if (edgeTexture) return;
        const int size = 128;
        edgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        edgeTexture.name = "Damage screen edge gradient";
        edgeTexture.hideFlags = HideFlags.HideAndDontSave;
        edgeTexture.wrapMode = TextureWrapMode.Clamp;
        edgeTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = x / (size - 1f) * 2f - 1f;
            float ny = y / (size - 1f) * 2f - 1f;
            float radius = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, radius));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        edgeTexture.SetPixels(pixels);
        edgeTexture.Apply(false, true);
    }

    private void ClearEffects()
    {
        impact = lowBlend = edgeAlpha = flashAlpha = 0f;
        impactAge = flashDuration;
        if (cameraTransform)
        {
            cameraTransform.localPosition = restPosition;
            cameraTransform.localRotation = restRotation;
        }
        if (playerCamera) playerCamera.fieldOfView = restFov;
    }

    private void OnDisable()
    {
        if (health) health.OnHealthChanged -= OnHealthChanged;
        if (ready) ClearEffects();
        ready = false;
        if (edgeTexture) Destroy(edgeTexture);
        edgeTexture = null;
    }
}