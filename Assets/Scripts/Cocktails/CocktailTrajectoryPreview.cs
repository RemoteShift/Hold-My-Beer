using UnityEngine;

/// <summary>Aim arc and landing radius, using the same swept segments as a live cocktail.</summary>
[DisallowMultipleComponent]
public class CocktailTrajectoryPreview : MonoBehaviour
{
    private readonly Vector3[] points = new Vector3[1201];
    private LineRenderer arc;
    private CocktailPixelRange landing;
    private CocktailPackEffect landingEffect;
    private Material material;
    private CocktailRecipe recipe;
    private Transform aim;
    private Transform owner;
    private int lastDrawFrame = -1;

    public void Show(CocktailRecipe selected, Transform aimTransform, Transform ownerTransform)
    {
        if (selected == null || !selected.IsThrowable || !aimTransform) { Hide(); return; }
        recipe = selected;
        aim = aimTransform;
        owner = ownerTransform;
        EnsureVisuals();
        Redraw();
    }

    public void Hide()
    {
        recipe = null;
        aim = null;
        if (arc) arc.enabled = false;
        if (landing) landing.Hide();
        if (landingEffect) { landingEffect.Release(); landingEffect = null; }
    }

    private void LateUpdate()
    {
        if (recipe != null && aim) Redraw();
    }

    private void EnsureVisuals()
    {
        if (arc) return;
        material = CocktailVisuals.CreateMaterial(Color.white);
        arc = CocktailVisuals.CreateLine("Cocktail aim arc", transform, material, 0.035f);
        landing = CocktailPixelRange.Create();
        arc.numCapVertices = 0;
        arc.numCornerVertices = 0;
        arc.enabled = false;
        if (landing) landing.Hide();
    }

    private void Redraw()
    {
        if (lastDrawFrame == Time.frameCount) return;
        lastDrawFrame = Time.frameCount;
        CocktailBallistics.GetLaunch(aim, out var origin, out var velocity);
        points[0] = origin;
        var count = 1;
        var landed = false;
        var impact = origin;
        for (var i = 1; i <= CocktailBallistics.MaxSteps; i++)
        {
            var next = CocktailBallistics.Position(origin, velocity, i * CocktailBallistics.Step);
            if (CocktailBallistics.Sweep(points[count - 1], next, owner, out var hit))
            {
                points[count++] = hit.point + hit.normal * CocktailBallistics.BottleRadius;
                impact = hit.point + hit.normal * 0.02f;
                landed = true;
                break;
            }
            points[count++] = next;
        }
        // Simulate from the eye for accurate close-wall collision, but omit the near-clip
        // portion when drawing: a ribbon starting inside the camera creates large triangles.
        var firstVisible = 0;
        while (firstVisible < count - 1 && Vector3.Distance(points[firstVisible], origin) < 0.8f)
            firstVisible++;
        var visibleCount = count - firstVisible;
        arc.enabled = visibleCount > 1;
        arc.positionCount = visibleCount;
        for (var i = 0; i < visibleCount; i++) arc.SetPosition(i, points[firstVisible + i]);
        var color = CocktailVfx.ColorFor(recipe.Id, recipe.Color);
        arc.startColor = new Color(color.r, color.g, color.b, 0.28f);
        arc.endColor = new Color(color.r, color.g, color.b, 0.9f);
        if (landing) landing.Hide();
        if (landed && CocktailBallistics.GroundBelow(impact, owner, out var ground, out var normal))
        {
            if (landing) landing.Show(ground, normal,
                recipe.Radius > 0f ? recipe.Radius : 0.15f, new Color(color.r, color.g, color.b, 0.65f));
            CocktailVfx.Preview(ref landingEffect, recipe.Id, ground, normal, recipe.Radius);
        }
        else if (landingEffect) { landingEffect.Release(); landingEffect = null; }
    }

    private void OnDisable() { Hide(); }

    private void OnDestroy()
    {
        if (arc) Destroy(arc.gameObject);
        if (landing) Destroy(landing.gameObject);
        if (material) Destroy(material);
    }
}
