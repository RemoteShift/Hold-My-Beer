using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Non-damaging cocktail bottle with swept collision and a visible flight trail.</summary>
public class CocktailProjectile : MonoBehaviour
{
    private float sodaPushStrength;
    private CocktailRecipe recipe;
    private Transform owner;
    private Vector3 origin;
    private Vector3 launchVelocity;
    private float elapsed;
    private float pendingTime;
    private Material bottleMaterial;
    private Material trailMaterial;
    private Transform bottle;
    private LineRenderer flightLanding;

    public static CocktailProjectile Launch(CocktailRecipe recipe, Transform aim, Transform owner)
    {
        if (recipe == null || !recipe.IsThrowable || !aim) return null;
        var go = new GameObject(recipe.Name + " projectile");
        var projectile = go.AddComponent<CocktailProjectile>();
        projectile.Initialize(recipe, aim, owner);
        return projectile;
    }

    private void Initialize(CocktailRecipe selected, Transform aim, Transform caster)
    {
        recipe = selected;
        sodaPushStrength = Mathf.Max(0f, CocktailSettings.Instance.sodaPushStrength);
        owner = caster;
        CocktailBallistics.GetLaunch(aim, out origin, out launchVelocity);
        transform.position = origin;
        var visual = new GameObject("Bottle visual");
        visual.transform.SetParent(transform, false);
        bottle = visual.transform;
        bottleMaterial = CocktailVisuals.CreateMaterial(recipe.Color);
        CocktailVisuals.CreateBottle(bottle, bottleMaterial);
        trailMaterial = CocktailVisuals.CreateMaterial(Color.white);
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.28f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0f;
        trail.startColor = recipe.Color;
        trail.endColor = new Color(recipe.Color.r, recipe.Color.g, recipe.Color.b, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.numCapVertices = 3;
        flightLanding = CocktailVisuals.CreateLine("In-flight landing radius", transform, trailMaterial, 0.055f);
        flightLanding.enabled = false;
        var previous = origin;
        for (var i = 1; i <= CocktailBallistics.MaxSteps; i++)
        {
            var next = CocktailBallistics.Position(origin, launchVelocity, i * CocktailBallistics.Step);
            if (CocktailBallistics.Sweep(previous, next, owner, out var hit))
            {
                if (CocktailBallistics.GroundBelow(hit.point + hit.normal * 0.02f, owner, out var ground, out var normal))
                {
                    flightLanding.enabled = true;
                    CocktailVisuals.SetRing(flightLanding, ground, normal, recipe.Radius, recipe.Color);
                }
                break;
            }
            previous = next;
        }

    }

    private void Update()
    {
        if (recipe == null) { Destroy(gameObject); return; }
        pendingTime += Time.deltaTime;
        // Fixed-sized swept segments are also used by the preview, independent of render framerate.
        while (pendingTime >= CocktailBallistics.Step)
        {
            pendingTime -= CocktailBallistics.Step;
            var nextTime = elapsed + CocktailBallistics.Step;
            var next = CocktailBallistics.Position(origin, launchVelocity, nextTime);
            if (CocktailBallistics.Sweep(transform.position, next, owner, out var hit))
            {
                Impact(hit);
                Destroy(gameObject);
                return;
            }
            elapsed = nextTime;
            transform.position = next;
            if (elapsed >= CocktailBallistics.Lifetime) { Destroy(gameObject); return; }
        }
        if (bottle) bottle.Rotate(Vector3.right, 340f * Time.deltaTime, Space.Self);
    }

    private void Impact(RaycastHit hit)
    {
        var impact = hit.point + hit.normal * 0.02f;
        var grounded = CocktailBallistics.GroundBelow(impact, owner, out var ground, out var normal);
        if (recipe.Id == CocktailKind.FizzySodaSplash)
        {
            if (!grounded) return;
            var affected = new System.Collections.Generic.List<EnemyBrain>();
            var enemies = EnemyBrain.All;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!CocktailBallistics.Reaches(enemy, ground, recipe.Radius)) continue;
                var direction = Vector3.ProjectOnPlane(enemy.transform.position - ground, Vector3.up);
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
                enemy.KnockbackFrom(enemy.transform.position - direction.normalized, sodaPushStrength);
                affected.Add(enemy);
            }
            CocktailSplash.Create(ground, normal, recipe.Radius, recipe.Color, 0.3f, affected);
            return;
        }
        // A floor patch cannot exist in a void; a wall/patron impact projects onto the floor below it.
        if (!grounded) return;
        if (recipe.Id == CocktailKind.HoneyBrew)
            CocktailHoneyPatch.Create(ground, normal, recipe);
        else if (recipe.Id == CocktailKind.AggressionTonic)
        {
            var enemies = EnemyBrain.All;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (CocktailBallistics.Reaches(enemy, ground, recipe.Radius)) enemy.ApplyAggression(recipe.Duration);
            }
            CocktailSplash.Create(ground, normal, recipe.Radius, recipe.Color, recipe.Duration);
        }
    }

    private void OnDestroy()
    {
        if (bottleMaterial) Destroy(bottleMaterial);
        if (trailMaterial) Destroy(trailMaterial);
    }
}
