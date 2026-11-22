using UnityEngine;
using UnityEngine.UI;

/// <summary>Hard-edged four-point sparkles, stepped at 12 fps while AYCD is active.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BeerPowerupSparkles : MaskableGraphic
{
    [SerializeField] private BeerInventory inventory;
    [SerializeField] private int phaseSeed;
    [SerializeField, Range(6, 18)] private int framesPerSecond = 12;
    [SerializeField, Min(1f)] private float pixelSize = 2f;
    private float clock;
    private int frame = -1;
    private bool wasActive;
    private static readonly Color Cream = new Color32(255, 255, 210, 255);
    private static readonly Color Mint = new Color32(139, 255, 209, 255);
    private static readonly Vector2[] Positions =
    {
        new Vector2(-56f, 20f), new Vector2(30f, 68f),
        new Vector2(54f, -10f), new Vector2(-24f, 70f)
    };

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        clock = 0f;
        frame = -1;
        wasActive = false;
    }

    private void Update() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        bool active = inventory && inventory.HasInfiniteBeer;
        if (active && !wasActive) clock = 0f;
        if (active && dt > 0f) clock = (clock + dt) % 600f;
        int next = Mathf.FloorToInt(clock * framesPerSecond);
        if (active != wasActive || next != frame)
        {
            wasActive = active;
            frame = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!inventory || !inventory.HasInfiniteBeer) return;
        Vector2 center = rectTransform.rect.center;
        for (int i = 0; i < Positions.Length; i++)
        {
            int phase = (Mathf.Max(0, frame) + phaseSeed * 5 + i * 4) % 16;
            if (phase > 6) continue;
            int arms = phase == 2 || phase == 3 ? 2 : phase == 1 || phase == 4 ? 1 : 0;
            Vector2 p = center + Positions[(i + phaseSeed) % Positions.Length];
            p.y += (phase / 3) * pixelSize;
            Color tint = (i + phaseSeed) % 2 == 0 ? Cream : Mint;
            PixelHudDrawing.Star(mesh, p.x, p.y, pixelSize, arms, tint);
        }
    }
}
