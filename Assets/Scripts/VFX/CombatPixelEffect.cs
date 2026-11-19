using UnityEngine;

/// <summary>Camera-facing, unlit pack animation. One-shots clean up; looping fuse flames can pause.</summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CombatPixelEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 15f;
    [SerializeField] private bool loop;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField, Min(0.01f)] private float worldWidth = 1f;
    [SerializeField, Min(0f)] private float cameraOffset;
    private Vector3 fixedPosition;
    private SpriteRenderer spriteRenderer;
    private float age;
    private Transform follow;
    private Vector3 worldDirection;
    public bool Paused { get; set; }

    private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); }
    private void OnEnable() { fixedPosition = transform.position; age = 0f; ApplyFrame(); FaceCamera(); }
    public void SetWorldWidth(float width) { worldWidth = Mathf.Max(0.01f, width); FaceCamera(); }
    public void Follow(Transform target) { follow = target; FaceCamera(); }
    public void SetDirection(Vector3 direction) { worldDirection = direction; FaceCamera(); }
    public void SampleProgress(float progress)
    {
        if (frames == null || frames.Length == 0) return;
        Paused = true;
        age = Mathf.Clamp01(progress) * (frames.Length - 1) / framesPerSecond;
        ApplyFrame();
    }

    private void LateUpdate()
    {
        if (!Paused) age += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (frames == null || frames.Length == 0) { Destroy(gameObject); return; }
        if (!loop && age >= frames.Length / framesPerSecond) { Destroy(gameObject); return; }
        ApplyFrame();
        FaceCamera();
    }

    private void ApplyFrame()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) { spriteRenderer.enabled = false; return; }
        spriteRenderer.enabled = true;
        var index = Mathf.FloorToInt(age * framesPerSecond);
        spriteRenderer.sprite = frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        spriteRenderer.color = Color.white;
    }

    private void FaceCamera()
    {
        var position = follow ? follow.position : fixedPosition;
        var camera = Camera.main;
        if (camera)
        {
            position -= camera.transform.forward * cameraOffset;
            var projected = new Vector2(Vector3.Dot(worldDirection, camera.transform.right), Vector3.Dot(worldDirection, camera.transform.up));
            float angle = projected.sqrMagnitude > 0.0001f ? Mathf.Atan2(projected.y, projected.x) * Mathf.Rad2Deg : 0f;
            transform.rotation = camera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
        }
        transform.position = position;
        if (frames == null || frames.Length == 0 || !frames[0]) return;
        float scale = worldWidth / (frames[0].rect.width / frames[0].pixelsPerUnit);
        transform.localScale = Vector3.one;
        var inherited = transform.lossyScale;
        transform.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(inherited.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(inherited.y)),
            scale / Mathf.Max(.0001f, Mathf.Abs(inherited.z)));
    }
}
