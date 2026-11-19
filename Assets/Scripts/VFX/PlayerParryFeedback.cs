using UnityEngine;

/// <summary>A brief full-world/player freeze followed by a held camera kick and smooth recovery.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class PlayerParryFeedback : MonoBehaviour
{
    [SerializeField] private BeerHandler handler;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Range(0.04f, 0.25f)] private float hitStopSeconds = 0.14f;
    [SerializeField, Range(0f, 3f)] private float cameraPunchDegrees = 2.6f;
    [SerializeField, Range(0f, 1f)] private float cameraKickDegrees = 0.45f;
    [SerializeField, Min(0.05f)] private float cameraPunchSeconds = 0.24f;
    [SerializeField, Min(0.01f)] private float cameraEaseInSeconds = 0.025f;
    private static PlayerParryFeedback activeFreeze;
    private PlayerHealth health;
    private LocomotionController locomotion;
    private bool ownsHitStop;
    private float previousTimeScale;
    private float stopUntil;
    private float zoomAge = 10f;
    private float impactStartedAt;
    private bool zoomApplied;
    private float beforeZoom;
    private float appliedFov;
    private Quaternion beforeRotation;
    private Quaternion appliedRotation;

    public bool IsHitStopping => ownsHitStop;
    public static bool IsPlayerFrozen => activeFreeze && activeFreeze.ownsHitStop && Time.timeScale == 0f;

    // Other time controllers cancel this layer before taking ownership of time.
    public static void CancelActiveFreeze()
    {
        if (InputHandler.TryGetInstance(out var input)) input.ClearParryFollowUpInputs();
        if (activeFreeze) activeFreeze.ReleaseHitStop();
        else activeFreeze = null;
    }

    private void OnEnable()
    {
        if (!handler) handler = GetComponent<BeerHandler>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
        health = GetComponent<PlayerHealth>();
        locomotion = GetComponent<LocomotionController>();
        if (handler) handler.OnParried += OnParried;
    }

    private void OnParried(EnemyBrain enemy)
    {
        if (!enemy || (health && health.IsDead)) return;
        if (!playerCamera) playerCamera = Camera.main;
        zoomAge = 0f;
        impactStartedAt = Time.realtimeSinceStartup;
        BeginHitStop();
    }

    private void BeginHitStop()
    {
        if (ownsHitStop && Time.timeScale == 0f)
        {
            stopUntil = Time.realtimeSinceStartup + hitStopSeconds;
            return;
        }
        if (Time.timeScale < 0.5f || AudioListener.pause || (InputHandler.TryGetInstance(out var input) && input.ActionsBlocked)) return;
        previousTimeScale = Time.timeScale;
        ownsHitStop = true;
        activeFreeze = this;
        stopUntil = Time.realtimeSinceStartup + hitStopSeconds;
        Time.timeScale = 0f;
    }

    private void Update()
    {
        RemoveCameraPunch();

        if (ownsHitStop)
        {
            if (Time.timeScale != 0f)
            {
                ownsHitStop = false;
                if (activeFreeze == this) activeFreeze = null;
            }
            else if (Time.realtimeSinceStartup >= stopUntil) ReleaseHitStop();
        }
        // Hold the impact pose throughout the freeze; settle only once it ends.
        if (!ownsHitStop) zoomAge += Time.unscaledDeltaTime;
        if (health && health.IsDead)
        {
            zoomAge = cameraPunchSeconds;
            ReleaseHitStop();
        }
    }

    private void LateUpdate()
    {
        if (!playerCamera || !playerCamera.isActiveAndEnabled || zoomAge >= cameraPunchSeconds) return;
        beforeZoom = playerCamera.fieldOfView;
        beforeRotation = playerCamera.transform.localRotation;
        // Ease into the held impact pose and leave it with zero endpoint velocity.
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.realtimeSinceStartup - impactStartedAt) / cameraEaseInSeconds));
        float recovery = ownsHitStop ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoomAge / cameraPunchSeconds));
        float strength = attack * recovery;
        appliedFov = Mathf.Clamp(beforeZoom - cameraPunchDegrees * strength, 20f, 140f);
        appliedRotation = beforeRotation * Quaternion.Euler(-cameraKickDegrees * strength, 0f, cameraKickDegrees * 0.5f * strength);
        playerCamera.fieldOfView = appliedFov;
        playerCamera.transform.localRotation = appliedRotation;
        zoomApplied = true;
    }

    private void RemoveCameraPunch()
    {
        if (zoomApplied && playerCamera)
        {
            if (Mathf.Approximately(playerCamera.fieldOfView, appliedFov))
                playerCamera.fieldOfView = beforeZoom;
            if (Quaternion.Angle(playerCamera.transform.localRotation, appliedRotation) < 0.001f)
                playerCamera.transform.localRotation = beforeRotation;
        }
        zoomApplied = false;
    }

    private void ReleaseHitStop()
    {
        bool wasOwner = ownsHitStop;
        if (ownsHitStop && Time.timeScale == 0f && !AudioListener.pause && (!health || !health.IsDead))
            Time.timeScale = previousTimeScale;
        ownsHitStop = false;
        if (activeFreeze == this) activeFreeze = null;
        if (wasOwner && locomotion && locomotion.canMove) locomotion.RefreshAfterHitStop();
    }

    private void OnApplicationFocus(bool focused) { if (!focused) { if (InputHandler.TryGetInstance(out var input)) input.ClearParryFollowUpInputs(); ReleaseHitStop(); } }
    private void OnApplicationPause(bool paused) { if (paused) { if (InputHandler.TryGetInstance(out var input)) input.ClearParryFollowUpInputs(); ReleaseHitStop(); } }
    private void OnDisable()
    {
        if (handler) handler.OnParried -= OnParried;
        if (InputHandler.TryGetInstance(out var input)) input.ClearParryFollowUpInputs();
        RemoveCameraPunch();
        ReleaseHitStop();
        zoomAge = cameraPunchSeconds;
    }
}
