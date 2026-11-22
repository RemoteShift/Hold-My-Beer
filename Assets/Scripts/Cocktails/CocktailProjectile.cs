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
    private Material trailMaterial;
    private CocktailPixelRange flightLanding;
    private CocktailPackEffect flightVfx;

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
        var art = WorldHudArt.Instance;
        WorldHudSprite.Create("Cocktail icon", transform, art ? art.Cocktail(recipe.Id) : null, 340f);
        flightVfx = CocktailVfx.Flight(recipe.Id, transform);
        var vfxColor = CocktailVfx.ColorFor(recipe.Id, recipe.Color);
        trailMaterial = CocktailVisuals.CreateMaterial(Color.white);
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.28f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0f;
        trail.startColor = vfxColor;
        trail.endColor = new Color(vfxColor.r, vfxColor.g, vfxColor.b, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.numCapVertices = 3;
        flightLanding = recipe.Id == CocktailKind.HoneyBrew ? CocktailPixelRange.Create() : null;
        var previous = origin;
        for (var i = 1; i <= CocktailBallistics.MaxSteps && flightLanding; i++)
        {
            var next = CocktailBallistics.Position(origin, launchVelocity, i * CocktailBallistics.Step);
            if (CocktailBallistics.Sweep(previous, next, owner, out var hit))
            {
                if (CocktailBallistics.GroundBelow(hit.point + hit.normal * 0.02f, owner, out var ground, out var normal))
                {
                    if (flightLanding) flightLanding.Show(ground, normal, recipe.Radius, vfxColor);
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
        if (flightVfx) flightVfx.SetDirection(launchVelocity + Physics.gravity * elapsed);
    }

    private void Impact(RaycastHit hit)
    {
        var impact = hit.point + hit.normal * 0.02f;
        var grounded = CocktailBallistics.GroundBelow(impact, owner, out var ground, out var normal);
        // Honey's authored goo splash opens at the feet; Soda still bursts on the struck body.
        if (recipe.Id == CocktailKind.HoneyBrew)
        {
            if (grounded) CocktailVfx.Impact(recipe.Id, ground, normal, recipe.Radius);
        }
        else
        {
            var struckEnemy = hit.collider.GetComponentInParent<EnemyBrain>();
            CocktailVfx.Impact(recipe.Id, impact, hit.normal, recipe.Radius, struckEnemy);
        }
        if (recipe.Id == CocktailKind.FizzySodaSplash)
        {
            if (!grounded) return;
            var enemies = EnemyBrain.All;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!CocktailBallistics.Reaches(enemy, ground, recipe.Radius)) continue;
                var direction = Vector3.ProjectOnPlane(enemy.transform.position - ground, Vector3.up);
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
                enemy.KnockbackFrom(enemy.transform.position - direction.normalized, sodaPushStrength);
                if (enemy.IsBeingPushed)
                {
                    CocktailEnemyVfx.Ensure(enemy);
                    var stamps = enemy.GetComponentInChildren<EnemyFreezeTimer>(true);
                    if (stamps) stamps.ShowSodaPush();
                }
            }
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
                if (CocktailBallistics.Reaches(enemy, ground, recipe.Radius))
                {
                    enemy.ApplyAggression(recipe.Duration);
                    CocktailEnemyVfx.Ensure(enemy);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (flightVfx) flightVfx.Release();
        if (flightLanding) Destroy(flightLanding.gameObject);
        if (trailMaterial) Destroy(trailMaterial);
    }
}
