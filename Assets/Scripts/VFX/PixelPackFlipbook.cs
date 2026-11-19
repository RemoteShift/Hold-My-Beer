using UnityEngine;

/// <summary>Plays the supplied pack frames without generating or modifying sprite artwork.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelPackFlipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 15f;
    [SerializeField] private bool loop = true;
    [SerializeField] private Transform anchor;
    [Tooltip("X: camera right; Y: world up; Z: towards camera, in world units.")]
    [SerializeField] private Vector3 offset;
    private SpriteRenderer spriteRenderer;
    private float age;

    private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); }
    private void OnEnable() { age = 0f; ShowFrame(); }
    private void LateUpdate()
    {
        age += Time.deltaTime;
        ShowFrame();
        var camera = Camera.main;
        if (!camera || !anchor) return;
        transform.SetPositionAndRotation(
            anchor.position + Vector3.up * offset.y + camera.transform.right * offset.x
                - camera.transform.forward * offset.z,
            camera.transform.rotation);
    }
    private void ShowFrame()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) { spriteRenderer.enabled = false; return; }
        spriteRenderer.enabled = true;
        int frame = Mathf.FloorToInt(age * framesPerSecond);
        frame = loop ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
        spriteRenderer.sprite = frames[frame];
    }
}
