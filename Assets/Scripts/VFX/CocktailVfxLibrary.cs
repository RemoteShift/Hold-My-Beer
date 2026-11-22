using UnityEngine;

/// <summary>Selected imported pack clips. Frame metadata comes from the supplied pack manifests.</summary>
public sealed class CocktailVfxLibrary : ScriptableObject
{
    [System.Serializable]
    public sealed class Clip
    {
        public string name;
        public Material material;
        public Vector2Int frameSize;
        public int columns, frameCount;
        public float framesPerSecond;
        public Vector2 pivot = new Vector2(.5f, .5f);
    }
    [System.Serializable]
    public sealed class Style
    {
        public CocktailKind kind;
        public Color color;
        public Clip flight, impact, area, status;
        public float statusWidth = 1.4f;
    }
    public Style[] styles;
    private static CocktailVfxLibrary instance;
    public static CocktailVfxLibrary Instance
    {
        get { if (!instance) instance = Resources.Load<CocktailVfxLibrary>("CocktailVfxLibrary"); return instance; }
    }
    public Style Get(CocktailKind kind)
    {
        if (styles != null) foreach (var style in styles) if (style.kind == kind) return style;
        return null;
    }
}

public static class CocktailVfx
{
    public static CocktailVfxLibrary.Style Style(CocktailKind kind) =>
        CocktailVfxLibrary.Instance ? CocktailVfxLibrary.Instance.Get(kind) : null;

    public static Color ColorFor(CocktailKind kind, Color fallback)
    {
        var style = Style(kind);
        return style != null ? style.color : fallback;
    }

    public static CocktailPackEffect Flight(CocktailKind kind, Transform owner)
    {
        var style = Style(kind);
        return style == null || style.flight == null || !style.flight.material ? null : CocktailPackEffect.Play(style.flight, owner.position,
            Vector3.up, .85f, true, false, .7f, owner);
    }

    public static void Impact(CocktailKind kind, Vector3 position, Vector3 normal, float radius,
        EnemyBrain struckEnemy = null)
    {
        var style = Style(kind);
        if (style == null) return;
        // Soda's burst travels with a struck patron instead of being left behind by the push.
        var target = kind == CocktailKind.FizzySodaSplash && struckEnemy ? struckEnemy.transform : null;
        var offset = target ? position - target.position : Vector3.zero;
        var effect = CocktailPackEffect.Play(style.impact, position, normal,
            kind == CocktailKind.FizzySodaSplash ? 1.6f : Mathf.Clamp(radius * 1.1f, 2f, 3.6f),
            false, false, kind == CocktailKind.FizzySodaSplash ? .85f : 1f, target, offset);
        // Pull body hits clear of the rendered mesh while preserving normal depth testing.
        if (effect && target) effect.CameraOffset = .35f;
    }

    public static CocktailPackEffect Area(CocktailKind kind, Transform owner, Vector3 normal, float radius)
    {
        var style = Style(kind);
        return style == null || style.area == null || !style.area.material ? null : CocktailPackEffect.Play(style.area, owner.position, normal,
            radius * 2f, true, true, kind == CocktailKind.HoneyBrew ? .48f : .3f, owner);
    }

    public static CocktailPackEffect Status(CocktailKind kind, EnemyBrain enemy, float footOffset)
    {
        var style = Style(kind);
        if (style == null) return null;
        var height = kind == CocktailKind.FizzySodaSplash ? .35f : footOffset;
        var offset = kind == CocktailKind.AggressionTonic ? AngerOffset(enemy) : Vector3.up * height;
        var effect = CocktailPackEffect.Play(style.status, enemy.transform.position, Vector3.up,
            style.statusWidth, true, false, kind == CocktailKind.FizzySodaSplash ? .45f : .85f, enemy.transform, offset);
        if (effect) effect.CameraOffset = kind == CocktailKind.FizzySodaSplash ? .65f : .2f;
        return effect;
    }

    public static Vector3 AngerOffset(EnemyBrain enemy)
    {
        var body = enemy.GetComponent<Collider>();
        var height = body ? body.bounds.max.y - enemy.transform.position.y - .20f : .83f;
        var side = body ? body.bounds.extents.x + .35f : .85f;
        var camera = Camera.main;
        return Vector3.up * height - (camera ? camera.transform.right : Vector3.right) * side;
    }

    public static void Preview(ref CocktailPackEffect effect, CocktailKind kind, Vector3 point,
        Vector3 normal, float radius)
    {
        var style = Style(kind);
        if (style == null || style.area == null || !style.area.material) { if (effect) effect.Release(); effect = null; return; }
        if (!effect || !effect.IsPlaying || effect.Clip != style.area)
        {
            if (effect) effect.Release();
            effect = CocktailPackEffect.Play(style.area, point, normal, radius * 2f, true, true, .18f);
        }
        if (effect) effect.SetGroundPose(point, normal, radius * 2f);
    }
}
