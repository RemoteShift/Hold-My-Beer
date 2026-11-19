using UnityEngine;

/// <summary>Two staggered rising copies of the pack's music symbol; no generated artwork.</summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class RisingMusicNotes : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Material material;
    [SerializeField] private Vector3 offset = new Vector3(0.8f, 0.6f, 0.65f);
    [SerializeField, Min(0.1f)] private float lifetime = 1.2f;
    [SerializeField, Min(0.01f)] private float noteWidth = 0.22f;
    [SerializeField] private float riseHeight = 0.6f;
    private SpriteRenderer[] notes;
    private float age;

    private void OnEnable()
    {
        age = 0f;
        var oldIcon = GetComponent<SpriteRenderer>();
        if (oldIcon) oldIcon.enabled = false;
        if (notes == null)
        {
            notes = new SpriteRenderer[2];
            for (int i = 0; i < notes.Length; i++)
            {
                var child = new GameObject("Rising Music Note " + (i + 1));
                child.transform.SetParent(transform, false);
                notes[i] = child.AddComponent<SpriteRenderer>();
                notes[i].sharedMaterial = material;
                notes[i].sortingOrder = 5;
                notes[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                notes[i].receiveShadows = false;
            }
        }
        UpdateNotes();
    }

    private void LateUpdate() { age += Time.deltaTime; UpdateNotes(); }

    private void UpdateNotes()
    {
        var camera = Camera.main;
        if (camera && anchor)
        {
            transform.SetPositionAndRotation(anchor.position + camera.transform.right * offset.x
                + Vector3.up * offset.y - camera.transform.forward * offset.z, camera.transform.rotation);
            transform.localScale = Vector3.one;
            var scale = transform.lossyScale;
            transform.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
        }
        if (notes == null) return;
        for (int i = 0; i < notes.Length; i++)
        {
            if (frames == null || frames.Length == 0) { notes[i].enabled = false; continue; }
            float phase = Mathf.Repeat(age / lifetime + i * 0.5f, 1f);
            int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(phase * lifetime / 0.32f * frames.Length));
            var sprite = frames[frame];
            notes[i].enabled = true;
            notes[i].sprite = sprite;
            float opacity = Mathf.Min(1f, Mathf.Min(phase / 0.12f, (1f - phase) / 0.2f));
            notes[i].color = new Color(1f, 1f, 1f, Mathf.Round(opacity * 4f) / 4f);
            notes[i].transform.localPosition = new Vector3((i == 0 ? -0.09f : 0.09f)
                + Mathf.Sin(phase * Mathf.PI * 2f + i) * 0.035f, phase * riseHeight, 0f);
            float size = sprite ? noteWidth / (sprite.rect.width / sprite.pixelsPerUnit) : 1f;
            notes[i].transform.localScale = Vector3.one * size;
        }
    }
}
