using System.Collections.Generic;
using UnityEngine;

/// <summary>Honey holds feet in place while enemies keep attacking and drinking normally.</summary>
public class CocktailHoneyPatch : MonoBehaviour
{
    private readonly HashSet<EnemyBrain> affected = new HashSet<EnemyBrain>();
    private float expiresAt;
    private float duration;
    private float radius;
    private Color color;
    private CocktailPixelRange edge;
    private CocktailPackEffect areaVfx;

    public static CocktailHoneyPatch Create(Vector3 position, Vector3 normal, CocktailRecipe recipe)
    {
        var go = new GameObject("Honey Brew floor patch");
        go.transform.position = position;
        var patch = go.AddComponent<CocktailHoneyPatch>();
        patch.duration = recipe.Duration;
        patch.expiresAt = Time.time + recipe.Duration;
        patch.radius = recipe.Radius;
        patch.color = recipe.Color;
        patch.edge = CocktailPixelRange.Create();
        if (patch.edge) patch.edge.transform.SetParent(go.transform, true);
        var tint = CocktailVfx.ColorFor(recipe.Id, recipe.Color);
        if (patch.edge) patch.edge.Show(position, normal, recipe.Radius,
            new Color(tint.r, tint.g, tint.b, .4f));
        patch.areaVfx = CocktailVfx.Area(recipe.Id, go.transform, normal, recipe.Radius);
        patch.ApplyToNearbyEnemies();
        return patch;
    }

    private void Update()
    {
        var remaining = expiresAt - Time.time;
        if (remaining > 0f) { ApplyToNearbyEnemies(); return; }
        // Late entrants keep their full root duration; retain the boundary until those roots end.
        foreach (var enemy in affected)
            if (enemy && !enemy.IsDead && enemy.isActiveAndEnabled && enemy.IsHoneyRooted) return;
        Destroy(gameObject);
    }

    private void ApplyToNearbyEnemies()
    {
        var enemies = EnemyBrain.All;
        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (affected.Contains(enemy) || !CocktailBallistics.Reaches(enemy, transform.position, radius)) continue;
            affected.Add(enemy);
            // Each entrant gets the full recipe duration, even just before the puddle dries.
            enemy.ApplyHoney(duration);
            CocktailEnemyVfx.Ensure(enemy);
        }
    }

    private void OnDestroy()
    {
        if (areaVfx) areaVfx.Release();
    }
}
