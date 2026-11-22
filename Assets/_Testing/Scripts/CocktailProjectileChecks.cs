#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Runs swept flight, impact and floor-patch integration checks in an isolated test area.</summary>
public class CocktailProjectileChecks : MonoBehaviour
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly List<GameObject> suspended = new List<GameObject>();
    private readonly Vector3 origin = new Vector3(300f, 100f, 300f);
    private int assertions;
    private float priorTimeScale;

    [MenuItem("Hold My Beer/Run Cocktail Projectile Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("Enter Play mode before running cocktail projectile checks."); return; }
        new GameObject("Cocktail Projectile Checks").AddComponent<CocktailProjectileChecks>();
    }

    private IEnumerator Start()
    {
        priorTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        { suspended.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
        try
        {
            var floor = Primitive("Cocktail test floor", PrimitiveType.Cube, origin - Vector3.up * 0.1f,
                new Vector3(60f, 0.2f, 60f));
            var owner = Primitive("Cocktail test owner", PrimitiveType.Capsule, origin + Vector3.up,
                Vector3.one);
            var aim = new GameObject("Cocktail test aim"); created.Add(aim);
            aim.transform.SetParent(owner.transform, false);
            aim.transform.localPosition = Vector3.up * 0.5f;
            aim.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            var honey = CocktailRecipes.Find(IngredientMask.LifeEssence | IngredientMask.Sugar);
            var soda = CocktailRecipes.Find(IngredientMask.Water | IngredientMask.Sugar);
            var aggression = CocktailRecipes.Find(IngredientMask.Water | IngredientMask.Gunpowder);
            Physics.SyncTransforms();

            CocktailBallistics.GetLaunch(aim.transform, out var launch, out var velocity);
            Assert(!CocktailBallistics.Sweep(launch, launch + aim.transform.forward * 0.2f, owner.transform, out _),
                "The bottle ignores its owner's starting collider");
            Assert(Predict(aim.transform, owner.transform, out var predicted), "Floor shot has a predicted impact");
            Assert(predicted.collider == floor.GetComponent<Collider>(), "Prediction ends on the supporting floor");
            var preview = owner.AddComponent<CocktailTrajectoryPreview>();
            preview.Show(honey, aim.transform, owner.transform);
            var arc = owner.transform.Find("Cocktail aim arc").GetComponent<LineRenderer>();
            Assert(Vector3.Distance(arc.GetPosition(arc.positionCount - 1),
                predicted.point + predicted.normal * CocktailBallistics.BottleRadius) < 0.01f,
                "The visible aim arc ends at the swept bottle collision");
            var radius = (CocktailPixelRange)typeof(CocktailTrajectoryPreview)
                .GetField("landing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
            Assert(radius && radius.gameObject.activeSelf && Mathf.Abs(radius.Radius - honey.Radius) < 0.03f
                && Vector3.Distance(radius.Center, predicted.point) < 0.03f,
                "Honey preview shows its actual area radius on the floor");
            var projectile = CocktailProjectile.Launch(honey, aim.transform, owner.transform);
            created.Add(projectile.gameObject);
            var elapsedField = typeof(CocktailProjectile).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance);
            var deadline = Time.time + 2f;
            while (projectile && Time.time < deadline)
            {
                yield return null;
                if (!projectile) break;
                var t = (float)elapsedField.GetValue(projectile);
                var expected = launch + velocity * t + Physics.gravity * (0.5f * t * t);
                Assert(Vector3.Distance(projectile.transform.position, expected) < 0.01f,
                    "Live bottle follows the preview's ballistic flight");
            }
            Assert(!projectile, "The bottle resolves collision rather than travelling through the floor");
            var patch = FindNear<CocktailHoneyPatch>(predicted.point);
            Assert(patch && Vector3.Distance(patch.transform.position, predicted.point) < 0.03f,
                "The Honey patch appears at the predicted impact");
            created.Add(patch.gameObject);
            preview.Hide();
            Assert(!arc.enabled && !radius.gameObject.activeSelf, "Cancelling/hiding removes both aiming visuals");

            var caught = Probe("Honey victim", patch.transform.position + Vector3.up + Vector3.right,
                owner.transform);
            yield return null;
            Assert(caught.IsHoneyRooted && caught.CurrentState is EnemyStunState,
                "A Honey floor entrant receives a brief stun and movement root");
            yield return null;
            Assert(StatusVisible(caught, "honey"), "Honey shows sticky ankle VFX on a rooted enemy");
            var area = (CocktailPackEffect)typeof(CocktailHoneyPatch)
                .GetField("areaVfx", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(patch);
            Assert(area && area.IsPlaying && !area.Paused, "Honey puddle animation is running");
            yield return new WaitForSeconds(2f);
            var late = Probe("Late Honey victim", patch.transform.position + Vector3.up + Vector3.left,
                owner.transform);
            yield return null;
            Assert(late.IsHoneyRooted, "Enemies entering an existing Honey patch are caught");
            yield return new WaitForSeconds(3.15f);
            Assert(patch && !caught.IsHoneyRooted, "Honey marker persists for late roots after the active puddle expires");
            Assert(late.IsHoneyRooted, "A late entrant receives its own full five seconds of glue");
            yield return new WaitForSeconds(2f);
            Assert(!patch && !late.IsHoneyRooted, "The late entrant's root also expires");
            yield return null;
            Assert(!StatusVisible(caught, "honey") && !StatusVisible(late, "honey") && !area.IsPlaying,
                "Honey ankle and puddle VFX stop when their gameplay effects end");
            caught.gameObject.SetActive(false); late.gameObject.SetActive(false);

            aim.transform.rotation = Quaternion.identity;
            var direct = Probe("Soda direct victim", origin + Vector3.up + Vector3.forward * 3f, owner.transform);
            var bystander = Probe("Soda bystander", origin + Vector3.up + Vector3.forward * 3f + Vector3.right * 2f,
                owner.transform);
            Physics.SyncTransforms();
            var directHealth = direct.currentHealth;
            projectile = CocktailProjectile.Launch(soda, aim.transform, owner.transform);
            created.Add(projectile.gameObject);
            var impactDeadline = Time.time + 1f;
            while (projectile && Time.time < impactDeadline) yield return null;
            yield return null;
            CocktailPackEffect bodyBurst = null;
            var sodaStyle = CocktailVfx.Style(CocktailKind.FizzySodaSplash);
            foreach (var effect in FindObjectsByType<CocktailPackEffect>(FindObjectsSortMode.None))
                if (effect.Clip == sodaStyle.impact && effect.IsPlaying &&
                    (Transform)typeof(CocktailPackEffect).GetField("follow", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(effect) == direct.transform) bodyBurst = effect;
            Assert(bodyBurst && bodyBurst.transform.position.y > origin.y + .5f && bodyBurst.CameraOffset >= .3f,
                "Direct Soda hit shows a body-height burst clear of the enemy mesh");
            var burstBefore = bodyBurst.transform.position;
            direct.transform.position += Vector3.right;
            yield return null;
            Assert(Vector3.Distance(bodyBurst.transform.position, burstBefore + Vector3.right) < .1f,
                "Soda hit burst follows the pushed enemy rather than remaining on the floor");
            direct.transform.position -= Vector3.right;
            yield return new WaitForSeconds(0.2f);
            Assert(!projectile && direct.Knockbacks == 1, "Soda knocks back the directly struck patron once");
            Assert(direct.currentHealth == directHealth && bystander.Knockbacks == 1 && direct.LastStrength == bystander.LastStrength,
                "Soda deals no damage and gives nearby bystanders the same knockback strength");
            direct.gameObject.SetActive(false); bystander.gameObject.SetActive(false);

            aim.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            Assert(Predict(aim.transform, owner.transform, out predicted), "Aggression has a floor impact");
            var first = Probe("Aggression first", predicted.point + Vector3.up + Vector3.right * 1.5f, owner.transform);
            var second = Probe("Aggression second", predicted.point + Vector3.up - Vector3.right * 1.5f, owner.transform);
            var outside = Probe("Aggression outside radius", predicted.point + Vector3.up + Vector3.forward * 6f,
                owner.transform);
            Physics.SyncTransforms();
            projectile = CocktailProjectile.Launch(aggression, aim.transform, owner.transform);
            created.Add(projectile.gameObject);
            yield return new WaitForSeconds(0.85f);
            Assert(!projectile && first.IsAggressive && second.IsAggressive,
                "Aggression affects patrons inside the projectile's area");
            Assert(first.target == second.transform && second.target == first.transform,
                "Affected patrons target each other instead of the owner");
            Assert(!outside.IsAggressive && first.currentHealth == first.maxHealth && second.currentHealth == second.maxHealth,
                "Aggression is non-damaging and respects its area radius");
            Assert(!FindNear<CocktailSplash>(predicted.point), "Aggression leaves no lingering ground circle");
            yield return new WaitForSeconds(2.1f);
            Assert(!FindNear<CocktailSplash>(predicted.point), "Aggression leaves no ground circle after expiry");
            Assert(!first.IsAggressive && first.target == owner.transform && second.target == owner.transform,
                "Aggression expires and restores the previous target");
            first.gameObject.SetActive(false); second.gameObject.SetActive(false); outside.gameObject.SetActive(false);

            var wall = Primitive("Cocktail blocking wall", PrimitiveType.Cube,
                origin + Vector3.up * 2f + Vector3.forward * 2f, new Vector3(4f, 4f, 0.2f));
            Physics.SyncTransforms();
            Assert(Predict(aim.transform, owner.transform, out predicted) && predicted.collider == wall.GetComponent<Collider>(),
                "A nearby wall stops the preview before the distant ground");
            Debug.Log($"COCKTAIL PROJECTILE CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            foreach (var go in created) if (go) Destroy(go);
            foreach (var go in suspended) if (go) go.SetActive(true);
            foreach (var splash in FindObjectsByType<CocktailSplash>(FindObjectsSortMode.None))
                if (Vector3.Distance(splash.transform.position, origin) < 40f) Destroy(splash.gameObject);
            Time.timeScale = priorTimeScale;
            Destroy(gameObject);
        }
    }

    private bool StatusVisible(EnemyBrain enemy, string field)
    {
        var vfx = enemy.GetComponent<CocktailEnemyVfx>();
        var effect = vfx ? (CocktailPackEffect)typeof(CocktailEnemyVfx)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vfx) : null;
        return effect && effect.IsPlaying && !effect.Paused;
    }

    private bool Predict(Transform aim, Transform owner, out RaycastHit hit)
    {
        CocktailBallistics.GetLaunch(aim, out var position, out var velocity);
        var previous = position;
        hit = default;
        for (var i = 1; i <= CocktailBallistics.MaxSteps; i++)
        {
            var next = position + velocity * (i * CocktailBallistics.Step)
                + Physics.gravity * (0.5f * i * i * CocktailBallistics.Step * CocktailBallistics.Step);
            if (CocktailBallistics.Sweep(previous, next, owner, out hit)) return true;
            previous = next;
        }
        return false;
    }

    private CocktailProjectileProbeEnemy Probe(string name, Vector3 position, Transform target)
    {
        var go = Primitive(name, PrimitiveType.Capsule, position, Vector3.one);
        go.SetActive(false);
        var agent = go.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        var probe = go.AddComponent<CocktailProjectileProbeEnemy>();
        probe.target = target;
        go.SetActive(true);
        return probe;
    }

    private GameObject Primitive(string name, PrimitiveType kind, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(kind); created.Add(go);
        go.name = name; go.transform.position = position; go.transform.localScale = scale;
        return go;
    }

    private T FindNear<T>(Vector3 point) where T : Component
    {
        foreach (var component in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (Vector3.Distance(component.transform.position, point) < 1f) return component;
        return null;
    }

    private void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("Cocktail projectile check failed: " + message);
        assertions++;
    }
}

/// <summary>A stationary target isolates projectile effects from unrelated navigation and attacks.</summary>
public class CocktailProjectileProbeEnemy : EnemyBrain
{
    public int Knockbacks { get; private set; }
    public float LastStrength { get; private set; }
    public override void ReturnToDefaultState() { }
    public override void KnockbackFrom(Vector3 origin, float strength)
    {
        Knockbacks++;
        LastStrength = strength;
        base.KnockbackFrom(origin, strength);
    }
}
#endif
