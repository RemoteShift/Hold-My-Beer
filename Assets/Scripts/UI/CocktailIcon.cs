using UnityEngine;
using UnityEngine.UI;

public enum CocktailGlyph { LifeEssence, Water, Sugar, Gunpowder, Beer, Honey, Fizzy, Aggression, Steel }

/// <summary>Small vector icons shared by the inventory, recipe costs and cocktail preview.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CocktailIcon : MaskableGraphic
{
    [SerializeField] private CocktailGlyph glyph;

    protected CocktailIcon() { useLegacyMeshGeneration = false; }

    public void SetGlyph(CocktailGlyph value, Color tint)
    {
        if (glyph == value && color == tint) return;
        glyph = value;
        color = tint;
        SetVerticesDirty();
    }

    public void SetRecipe(CocktailRecipe recipe)
    {
        var value = CocktailGlyph.Beer;
        switch (recipe.Id)
        {
            case CocktailKind.HoneyBrew: value = CocktailGlyph.Honey; break;
            case CocktailKind.FizzySodaSplash: value = CocktailGlyph.Fizzy; break;
            case CocktailKind.AggressionTonic: value = CocktailGlyph.Aggression; break;
            case CocktailKind.SteelGutVodka: value = CocktailGlyph.Steel; break;
        }
        SetGlyph(value, recipe.Color);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var shade = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, color.a);
        switch (glyph)
        {
            case CocktailGlyph.LifeEssence:
                Disc(mesh, 0.32f, 0.64f, 0.23f, color);
                Disc(mesh, 0.68f, 0.64f, 0.23f, color);
                Shape(mesh, color, new Vector2(.11f,.58f), new Vector2(.89f,.58f), new Vector2(.5f,.07f));
                break;
            case CocktailGlyph.Water:
                Disc(mesh, .5f, .35f, .29f, color);
                Shape(mesh, color, new Vector2(.5f,.96f), new Vector2(.22f,.43f), new Vector2(.78f,.43f));
                Disc(mesh, .4f, .29f, .065f, Color.Lerp(color, Color.white, .65f));
                break;
            case CocktailGlyph.Sugar:
                Shape(mesh, color, new Vector2(.5f,.94f), new Vector2(.9f,.7f), new Vector2(.5f,.46f), new Vector2(.1f,.7f));
                Shape(mesh, Color.Lerp(color, shade, .3f), new Vector2(.1f,.7f), new Vector2(.5f,.46f), new Vector2(.5f,.07f), new Vector2(.1f,.31f));
                Shape(mesh, shade, new Vector2(.5f,.46f), new Vector2(.9f,.7f), new Vector2(.9f,.31f), new Vector2(.5f,.07f));
                break;
            case CocktailGlyph.Gunpowder:
                Shape(mesh, color, new Vector2(.36f,.74f), new Vector2(.64f,.74f), new Vector2(.88f,.27f), new Vector2(.75f,.09f), new Vector2(.25f,.09f), new Vector2(.12f,.27f));
                Box(mesh, .34f, .8f, .32f, .13f, color);
                Box(mesh, .27f, .73f, .46f, .055f, shade);
                Box(mesh, .47f, .27f, .06f, .25f, shade);
                Box(mesh, .37f, .36f, .26f, .06f, shade);
                break;
            case CocktailGlyph.Honey:
                Hex(mesh, .31f, .33f, .23f, color);
                Hex(mesh, .7f, .49f, .23f, color);
                Hex(mesh, .35f, .75f, .23f, color);
                break;
            case CocktailGlyph.Fizzy:
                Shape(mesh, color, new Vector2(.2f,.68f), new Vector2(.8f,.68f), new Vector2(.72f,.07f), new Vector2(.28f,.07f));
                Disc(mesh, .34f, .85f, .07f, color);
                Disc(mesh, .63f, .9f, .09f, color);
                Disc(mesh, .44f, .45f, .065f, shade);
                Disc(mesh, .6f, .23f, .04f, shade);
                break;
            case CocktailGlyph.Aggression:
                var star = new Vector2[16];
                for (var i = 0; i < star.Length; i++)
                {
                    var angle = i * Mathf.PI / 8f;
                    star[i] = new Vector2(.5f, .5f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? .48f : .28f);
                }
                Shape(mesh, color, star);
                Box(mesh, .46f, .43f, .08f, .29f, shade);
                Box(mesh, .46f, .28f, .08f, .08f, shade);
                break;
            case CocktailGlyph.Steel:
                Shape(mesh, color, new Vector2(.12f,.84f), new Vector2(.5f,.97f), new Vector2(.88f,.84f), new Vector2(.82f,.39f), new Vector2(.5f,.05f), new Vector2(.18f,.39f));
                Box(mesh, .46f, .3f, .08f, .44f, shade);
                Box(mesh, .32f, .48f, .36f, .08f, shade);
                break;
            default:
                Box(mesh, .15f, .13f, .51f, .59f, color);
                Box(mesh, .65f, .24f, .22f, .09f, color);
                Box(mesh, .79f, .24f, .08f, .36f, color);
                Box(mesh, .65f, .54f, .22f, .09f, color);
                Disc(mesh, .25f, .76f, .12f, color);
                Disc(mesh, .44f, .8f, .13f, color);
                Disc(mesh, .6f, .75f, .1f, color);
                Box(mesh, .29f, .24f, .05f, .34f, shade);
                Box(mesh, .47f, .24f, .05f, .34f, shade);
                break;
        }
    }

    private void Box(VertexHelper mesh, float x, float y, float w, float h, Color tint) =>
        Shape(mesh, tint, new Vector2(x,y), new Vector2(x+w,y), new Vector2(x+w,y+h), new Vector2(x,y+h));

    private void Disc(VertexHelper mesh, float x, float y, float radius, Color tint) => Circle(mesh, x, y, radius, 24, tint);
    private void Hex(VertexHelper mesh, float x, float y, float radius, Color tint) => Circle(mesh, x, y, radius, 6, tint);
    private void Circle(VertexHelper mesh, float x, float y, float radius, int sides, Color tint)
    {
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
        {
            var angle = i * Mathf.PI * 2f / sides;
            points[i] = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
        }
        Shape(mesh, tint, points);
    }
    private void Shape(VertexHelper mesh, Color tint, params Vector2[] points)
    {
        var center = Vector2.zero;
        foreach (var point in points) center += point;
        center /= points.Length;
        var start = mesh.currentVertCount;
        AddVertex(mesh, center, tint);
        foreach (var point in points) AddVertex(mesh, point, tint);
        for (var i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
    }
    private void AddVertex(VertexHelper mesh, Vector2 point, Color tint)
    {
        var rect = GetPixelAdjustedRect();
        var vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);
        vertex.color = tint;
        mesh.AddVert(vertex);
    }
}
