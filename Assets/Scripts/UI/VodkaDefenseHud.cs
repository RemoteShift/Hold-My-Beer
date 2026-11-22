using UnityEngine;
using UnityEngine.UI;

/// <summary>Pixel shield and duration pips remain visible for the whole vodka defense effect.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VodkaDefenseHud : MaskableGraphic
{
    [SerializeField] private PlayerHealth health;
    [SerializeField, Min(1f)] private float pixelSize = 2f;
    private float clock, totalDuration, previousRemaining, activationAge;
    private int frame = -1, previousPips = -1;
    private bool wasActive;
    private static readonly string[] Shield =
    {
        "001111100", "012222210", "122333221", "122323221",
        "122222221", "122222221", "012222210", "012222210",
        "001222100", "000121000", "000010000"
    };
    private static readonly Color Edge = new Color32(24, 75, 88, 255);
    private static readonly Color Steel = new Color32(93, 214, 220, 255);
    private static readonly Color Shine = new Color32(231, 255, 200, 255);

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        wasActive = false;
        frame = previousPips = -1;
        clock = totalDuration = previousRemaining = activationAge = 0f;
    }

    private void Update() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        float remaining = health && !health.IsDead ? health.InvulnerableSecondsLeft : 0f;
        bool active = remaining > 0f;
        if (active && (!wasActive || remaining > previousRemaining + .001f))
        {
            totalDuration = remaining;
            activationAge = 0f;
        }
        if (active && dt > 0f)
        {
            clock = (clock + dt) % 600f;
            activationAge += dt;
        }
        int next = Mathf.FloorToInt(clock * 12f);
        int pips = active && totalDuration > 0f
            ? Mathf.Clamp(Mathf.CeilToInt(9f * remaining / totalDuration), 1, 9) : 0;
        if (active != wasActive || next != frame || pips != previousPips)
        {
            frame = next;
            previousPips = pips;
            SetVerticesDirty();
        }
        wasActive = active;
        previousRemaining = remaining;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!health || health.IsDead || !health.IsInvulnerable) return;
        Vector2 center = rectTransform.rect.center;
        float left = center.x - 4.5f * pixelSize;
        float top = center.y + 5.5f * pixelSize;
        for (int row = 0; row < Shield.Length; row++)
            for (int col = 0; col < Shield[row].Length; col++)
            {
                char value = Shield[row][col];
                if (value == '0') continue;
                Color tint = value == '1' ? Edge : value == '3' ? Shine : Steel;
                if (value == '2' && (row + col + Mathf.Max(0, frame) / 2) % 18 == 0) tint = Shine;
                PixelHudDrawing.Box(mesh, left + col * pixelSize,
                    top - (row + 1) * pixelSize, pixelSize, pixelSize, tint);
            }
        float barY = top - 11f * pixelSize - 4f;
        for (int i = 0; i < 9; i++)
            PixelHudDrawing.Box(mesh, center.x - 8.5f + i * 2f, barY, 1f, 2f,
                i < previousPips ? Shine : Edge);
        if (activationAge < .4f)
        {
            int arms = Mathf.Max(0, frame) % 3 == 1 ? 1 : 0;
            PixelHudDrawing.Star(mesh, left - 4f, center.y + 3f, 1f, arms, Shine);
            PixelHudDrawing.Star(mesh, left + 9f * pixelSize + 2f, center.y - 3f, 1f, arms, Shine);
        }
    }
}
