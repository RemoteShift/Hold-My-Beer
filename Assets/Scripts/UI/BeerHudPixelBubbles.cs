using UnityEngine;
using UnityEngine.UI;

/// <summary>Native-pixel effervescence, drawn as stepped UI geometry inside a beer fill.</summary>
[AddComponentMenu("UI/Beer HUD Pixel Bubbles")]
public sealed class BeerHudPixelBubbles : MaskableGraphic
{
    [SerializeField] private UnityEngine.UI.Image liquid;
    [SerializeField, Range(1, 20)] private int bubbleCount = 9;
    [SerializeField, Range(4, 24)] private int framesPerSecond = 12;
    [SerializeField] private int phaseSeed;
    [SerializeField] private Color highlight = new Color32(255, 239, 184, 215);
    [SerializeField] private Color amberEdge = new Color32(191, 112, 28, 120);

    private float clock;
    private int frame = -1;
    private float previousFill = -1f;
    private Sprite previousSprite;
    private int columns, rows, visibleRows;
    private Rect bounds;

    public void Configure(UnityEngine.UI.Image source, int seed)
    {
        liquid = source;
        phaseSeed = seed;
        raycastTarget = false;
        SetAllDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        if (!liquid && transform.parent) liquid = transform.parent.GetComponent<UnityEngine.UI.Image>();
        previousFill = -1f;
        SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (!liquid) return;
        // Scaled time keeps the effect still while gameplay is paused.
        clock += Time.deltaTime;
        // Bounded clock avoids long-session integer overflow and preserves frame timing.
        if (clock >= 3600f) clock -= 3600f;
        int nextFrame = Mathf.FloorToInt(clock * framesPerSecond);
        if (nextFrame == frame && previousFill == liquid.fillAmount && previousSprite == liquid.sprite) return;
        frame = nextFrame;
        previousFill = liquid.fillAmount;
        previousSprite = liquid.sprite;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!liquid || !liquid.enabled || !liquid.sprite || liquid.color.a <= 0f) return;
        columns = Mathf.Max(1, Mathf.RoundToInt(liquid.sprite.rect.width));
        rows = Mathf.Max(1, Mathf.RoundToInt(liquid.sprite.rect.height));
        visibleRows = Mathf.Clamp(Mathf.RoundToInt(liquid.fillAmount * rows), 0, rows);
        if (columns < 9 || visibleRows < 5) return;
        bounds = rectTransform.rect;

        for (int i = 0; i < bubbleCount; i++)
        {
            int seed = i * 37 + phaseSeed * 19;
            int ticksPerRow = 1 + (i % 3 == 0 ? 1 : 0);
            int travel = visibleRows - 4;
            int riseTicks = travel * ticksPerRow;
            int period = riseTicks + 10 + i % 4;
            int age = (Mathf.Max(0, frame) + seed * 7) % period;
            int x = 4 + (seed * 11 % (columns - 8));
            int wobblePhase = (age / 7 + seed) % 4;
            x += wobblePhase == 1 ? 1 : wobblePhase == 3 ? -1 : 0;
            float opacity = liquid.color.a * (i % 3 == 0 ? 0.85f : 0.65f);

            if (age < riseTicks)
            {
                int y = 2 + age / ticksPerRow;
                // Nucleate as a single pixel; larger bubbles open into a tiny hollow ring.
                bool large = i % 3 == 0 && y > 7 && visibleRows - y > 3;
                if (large)
                {
                    Pixel(vh, x, y + 1, highlight, opacity);
                    Pixel(vh, x - 1, y, highlight, opacity);
                    Pixel(vh, x + 1, y, highlight, opacity * 0.72f);
                    Pixel(vh, x, y - 1, amberEdge, opacity);
                }
                else
                {
                    Pixel(vh, x, y, highlight, opacity);
                    if (i % 3 == 1 && y > 5) Pixel(vh, x, y - 1, amberEdge, opacity);
                }
            }
            else if (age < riseTicks + 3)
            {
                // A two-frame split/pop stays below the foam and the cropped surface.
                int spread = age == riseTicks ? 1 : 2;
                Pixel(vh, x - spread, visibleRows - 2, highlight, opacity * 0.6f);
                Pixel(vh, x + spread, visibleRows - 2, highlight, opacity * 0.45f);
            }
        }
    }

    private void Pixel(VertexHelper vh, int x, int y, Color tint, float opacity)
    {
        // Clip in native sprite cells, so neither motion nor clipping uses fractional pixels.
        if (x < 2 || x >= columns - 2 || y < 1 || y >= visibleRows - 1) return;
        float x0 = bounds.xMin + bounds.width * x / columns;
        float x1 = bounds.xMin + bounds.width * (x + 1) / columns;
        float y0 = bounds.yMin + bounds.height * y / rows;
        float y1 = bounds.yMin + bounds.height * (y + 1) / rows;
        tint *= color;
        tint.a *= opacity;
        Color32 c = tint;
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
        vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
        vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
        vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
