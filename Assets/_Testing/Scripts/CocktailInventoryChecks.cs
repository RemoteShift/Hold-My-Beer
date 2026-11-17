#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Checks real brewing transactions and timed buffs without changing the live player's inventory.</summary>
public class CocktailInventoryChecks : MonoBehaviour
{
    private int assertions;

    [MenuItem("Hold My Beer/Run Cocktail Inventory Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying || Time.timeScale <= 0f)
        {
            Debug.LogError("Enter unpaused Play mode before running cocktail inventory checks.");
            return;
        }
        new GameObject("Cocktail Inventory Checks").AddComponent<CocktailInventoryChecks>();
    }

    private IEnumerator Start()
    {
        // Keep this fixture inactive: its required locomotion singleton must never replace the live player.
        // Invoke only the resource components' lifecycle, not movement, input, camera or HUD lifecycle.
        var fixture = new GameObject("Inactive cocktail inventory fixture");
        fixture.SetActive(false);
        var brewer = fixture.AddComponent<CocktailBrewer>();
        var inventory = fixture.GetComponent<IngredientInventory>();
        var beers = fixture.GetComponent<BeerInventory>();
        var health = fixture.GetComponent<PlayerHealth>();
        var effects = fixture.GetComponent<PlayerCocktailEffects>();
        try
        {
            InvokeLifecycle(beers, "Awake");
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(effects, "Awake");
            InvokeLifecycle(brewer, "Awake");
            InvokeLifecycle(effects, "OnEnable");
            InvokeLifecycle(brewer, "OnEnable");
            health.RespawnOnDeath = false;
            var honeyMask = IngredientMask.LifeEssence | IngredientMask.Sugar;
            var honey = CocktailRecipes.Find(honeyMask);
            Assert(honey != null && honey.Id == CocktailKind.HoneyBrew && honey.GetCost(IngredientType.Sugar) == 2 &&
                   honey.GetCost(IngredientType.LifeEssence) == 1 && honey.Duration == 5f,
                "Honey uses the documented two sugar, one life essence and five-second root");
            Assert(CocktailRecipes.Find(IngredientMask.Water | IngredientMask.Sugar).Id == CocktailKind.FizzySodaSplash &&
                   CocktailRecipes.Find(IngredientMask.Water | IngredientMask.Gunpowder).Id == CocktailKind.AggressionTonic &&
                   CocktailRecipes.Find(IngredientMask.LifeEssence).BeerCost == 1 &&
                   CocktailRecipes.Find(IngredientMask.LifeEssence | IngredientMask.Gunpowder).Duration == 6f,
                "All five recipes resolve from their exact ingredient combinations");
            for (var key = 1; key <= 5; key++)
                Assert(CocktailRecipes.GetByHotkey(key).Hotkey == key, "Each cocktail has its own number key");
            brewer.ProcessControls(1, false, false, false);
            Assert(brewer.HeldHotkey == 1, "Holding 1 previews Honey before clicking");
            brewer.ProcessControls(3, false, false, false);
            Assert(brewer.HeldHotkey == 2, "A newly held 2 overrides the still-held 1");
            brewer.ProcessControls(7, false, false, false);
            Assert(brewer.HeldHotkey == 3, "A newly held 3 takes priority over 1 and 2");
            brewer.ProcessControls(5, false, false, false);
            Assert(brewer.HeldHotkey == 3, "Releasing a non-selected key preserves the selection");
            brewer.ProcessControls(1, false, false, false);
            Assert(brewer.HeldHotkey == 1, "Releasing the newest key falls back to a key still held");
            brewer.ProcessControls(0, false, false, false);
            Assert(brewer.SelectedRecipe == null, "Releasing every number key clears the preview");
            brewer.ProcessControls(16, false, false, false);
            brewer.ProcessControls(17, false, false, false);
            Assert(brewer.HeldHotkey == 1, "Recency takes priority over a higher key number");
            brewer.ProcessControls(16, false, false, false);
            Assert(brewer.HeldHotkey == 5, "Fallback can restore the fifth cocktail");
            brewer.ProcessControls(0, false, false, false);

            inventory.Add(IngredientType.LifeEssence, 10);
            inventory.Add(IngredientType.Sugar, 1);
            var feedback = string.Empty;
            brewer.OnFeedback += (message, error) => { if (error) feedback = message; };
            Assert(brewer.ProcessControls(1, true, false, false), "A failed craft still consumes its combat click");
            Assert(brewer.ConfirmedRecipe == null && inventory.GetCount(IngredientType.LifeEssence) == 10 &&
                   inventory.GetCount(IngredientType.Sugar) == 1 && feedback.Contains("1 Sugar"),
                "A shortfall names the missing ingredient and spends nothing");
            brewer.ProcessControls(0, false, true, false);
            inventory.Add(IngredientType.Sugar, 3);
            var physicalBefore = beers.PhysicalCount;
            brewer.ProcessControls(0, true, false, false);
            Assert(inventory.GetCount(IngredientType.Sugar) == 4 && inventory.GetCount(IngredientType.LifeEssence) == 10 &&
                   beers.PhysicalCount == physicalBefore, "Clicking with no cocktail selected spends no recipe ingredients");
            brewer.ProcessControls(0, false, true, false);

            Assert(brewer.ProcessControls(1, true, false, false), "A recipe confirmation owns its click");
            Assert(brewer.ConfirmedRecipe == honey && inventory.GetCount(IngredientType.Sugar) == 2 &&
                   inventory.GetCount(IngredientType.LifeEssence) == 9, "Confirmation reserves exact costs once");
            Assert(brewer.ProcessControls(0, false, false, false) && brewer.ConfirmedRecipe == honey,
                "Releasing the number key preserves the locked cocktail and captured mouse input");
            brewer.ProcessControls(1, true, false, false);
            Assert(inventory.GetCount(IngredientType.Sugar) == 2 && inventory.GetCount(IngredientType.LifeEssence) == 9,
                "Repeated press edges while held cannot spend ingredients again");
            brewer.ProcessControls(2, false, false, false);
            Assert(brewer.HeldHotkey == 2 && brewer.ConfirmedRecipe == honey && brewer.SelectedRecipe == honey,
                "Changing number keys while aiming cannot replace the reserved cocktail");
            var projectileCount = FindObjectsByType<CocktailProjectile>(FindObjectsSortMode.None).Length;
            Assert(brewer.ProcessControls(0, false, false, true), "RMB cancellation consumes its combat click");
            Assert(brewer.ConfirmedRecipe == null && inventory.GetCount(IngredientType.Sugar) == 4 &&
                   inventory.GetCount(IngredientType.LifeEssence) == 10, "Cancellation refunds the exact reservation");
            brewer.ProcessControls(0, false, false, true);
            Assert(inventory.GetCount(IngredientType.Sugar) == 4 && inventory.GetCount(IngredientType.LifeEssence) == 10,
                "Repeated cancellation cannot duplicate ingredients");
            Assert(brewer.ProcessControls(0, false, true, false), "Release after cancel remains captured");
            Assert(FindObjectsByType<CocktailProjectile>(FindObjectsSortMode.None).Length == projectileCount,
                "A cancelled cocktail never launches on mouse release");
            Assert(!brewer.ProcessControls(0, false, false, false), "Neutral input returns control to combat");

            brewer.ProcessControls(8, true, false, false);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 7 && beers.PhysicalCount == physicalBefore - 1 &&
                   effects.InfiniteBeerSecondsLeft > 9f, "AYCD drinks on press, spends one physical beer and lasts ten seconds");
            brewer.ProcessControls(8, true, false, false);
            brewer.ProcessControls(0, false, true, false);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 7 && beers.PhysicalCount == physicalBefore - 1,
                "Holding and releasing a drink cannot consume it twice");
            Assert(beers.TryTakeRightmost(out var full) && full == 1f && beers.PhysicalCount == physicalBefore - 1,
                "AYCD serves a full glass without depleting the physical supply");
            Assert(!beers.TryAddGlass(1f), "Temporary infinite servings cannot be banked into physical stock");
            brewer.ProcessControls(8, true, false, false);
            brewer.ProcessControls(0, false, true, false);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 7 && beers.PhysicalCount == physicalBefore - 1,
                "Active AYCD cannot fund or extend itself");

            effects.Clear();
            Assert(beers.TryConsumeForRecipe(beers.PhysicalCount), "Fixture can empty its real beer supply");
            Assert(!inventory.TryConsume(CocktailRecipes.Find(IngredientMask.LifeEssence), beers, out _) &&
                   inventory.GetCount(IngredientType.LifeEssence) == 7, "AYCD fails atomically when no physical beer remains");
            beers.GrantInfiniteBeer(0.08f);
            Assert(beers.HasBeer && beers.PhysicalCount == 0 && beers.Glasses.Count == beers.MaxGlasses,
                "An infinite supply provides ready glasses even with empty physical inventory");
            health.GrantInvulnerability(0.08f);
            var hp = health.CurrentHealth;
            health.TakeDamage(20f);
            Assert(health.CurrentHealth == hp, "Steel-Gut protection prevents incoming damage");
            yield return new WaitForSeconds(0.12f);
            Assert(!beers.HasInfiniteBeer && !beers.HasBeer && effects.InfiniteBeerSecondsLeft == 0f,
                "Infinite beer expires back to the original empty inventory");
            health.TakeDamage(20f);
            Assert(health.CurrentHealth == hp - 20f && !health.IsInvulnerable,
                "Damage resumes when the protection expires");

            inventory.Add(IngredientType.Gunpowder, 1);
            brewer.ProcessControls(16, true, false, false);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 5 && inventory.GetCount(IngredientType.Gunpowder) == 0 &&
                   effects.InvulnerableSecondsLeft > 5f, "Steel-Gut uses two life essence and one gunpowder for six seconds");
            brewer.ProcessControls(0, false, true, false);
            beers.GrantInfiniteBeer(10f);
            brewer.ProcessControls(1, true, false, false);
            Assert(brewer.ConfirmedRecipe != null, "A throw can be pending when death occurs");
            health.Die();
            Assert(!beers.HasInfiniteBeer && !health.IsInvulnerable && brewer.ConfirmedRecipe == null &&
                   inventory.GetCount(IngredientType.Sugar) == 4 && inventory.GetCount(IngredientType.LifeEssence) == 5,
                "Death clears both drink buffs and refunds a pending throw once");
            Debug.Log($"COCKTAIL INVENTORY CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            InvokeLifecycle(brewer, "OnDisable");
            InvokeLifecycle(effects, "OnDisable");
            Destroy(fixture);
            Destroy(gameObject);
        }
    }

    private static void InvokeLifecycle(MonoBehaviour component, string method)
    {
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
    }

    private void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Cocktail check failed: " + description);
        assertions++;
    }
}
#endif
