using UnityEngine;
using Unity.Cinemachine;

/// <summary>Small continuous motion on the chalkboard view, without moving its saved framing.</summary>
[AddComponentMenu("Hold My Beer/UI/Main Menu Camera Bob")]
public sealed class MainMenuCameraBob : CinemachineExtension
{
    public GreenWellMenu menu;
    [Min(1f)] public float period = 12f;
    public Vector3 positionAmplitude = new Vector3(.003f, .008f, 0f);
    public Vector3 rotationAmplitude = new Vector3(.025f, .035f, .015f);

    private float elapsed;
    private int lastFrame = -1;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (!Application.isPlaying || stage != CinemachineCore.Stage.Noise) return;

        if (lastFrame != Time.frameCount)
        {
            lastFrame = Time.frameCount;
            bool hold = menu && (!menu.menuCamera || !menu.menuCamera.enabled ||
                !menu.cameraBrain || !menu.cameraBrain.enabled || menu.cameraBrain.IsBlending ||
                (menu.audioMenu && (menu.audioMenu.IsOpen || menu.audioMenu.IsTransitioning)) ||
                (menu.receiptControls && menu.receiptControls.IsOpen));
            if (!hold) elapsed += Time.unscaledDeltaTime;
        }

        float phase = elapsed * (Mathf.PI * 2f / Mathf.Max(1f, period));
        var offset = new Vector3(positionAmplitude.x * Mathf.Sin(phase * .73f),
            positionAmplitude.y * Mathf.Sin(phase), positionAmplitude.z * Mathf.Sin(phase * .53f));
        var angles = new Vector3(rotationAmplitude.x * Mathf.Sin(phase * .8f),
            rotationAmplitude.y * Mathf.Sin(phase * .6f), rotationAmplitude.z * Mathf.Sin(phase * .45f));
        state.PositionCorrection += state.RawOrientation * offset;
        state.OrientationCorrection *= Quaternion.Euler(angles);
    }
}
