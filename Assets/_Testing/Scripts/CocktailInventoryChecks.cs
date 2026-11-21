#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Checks recipe selection, transactions and buffs on an inactive fixture.</summary>
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
        var fixture = new GameObject("Inactive cocktail inventory fixture");
        fixture.SetActive(false);
        var brewer = fixture.AddComponent<CocktailBrewer>();
        var inventory = fixture.GetComponent<IngredientInventory>();
        var beers = fixture.GetComponent<BeerInventory>();
        var health = fixture.GetComponent<PlayerHealth>();
        var effects = fixture.GetComponent<PlayerCocktailEffects>();
        try
        {
            Invoke(beers, "Awake"); Invoke(health, "Awake"); Invoke(effects, "Awake"); Invoke(brewer, "Awake");
            Invoke(effects, "OnEnable"); Invoke(brewer, "OnEnable");
            health.RespawnOnDeath = false;
            var honey = brewer.SelectedRecipe;
            Assert(honey.Id == CocktailKind.HoneyBrew, "Default selected recipe is Honey");
            brewer.ScrollSelection(-1);
            Assert(brewer.SelectedRecipe.Id == CocktailKind.SteelGutVodka, "Previous wraps to the last recipe");
            brewer.ScrollSelection(1);
            Assert(brewer.SelectedRecipe == honey, "Next wraps to the first recipe");
            brewer.ScrollSelection(int.MinValue);
            Assert(brewer.SelectedRecipe != null, "Large negative scrolling stays in range");
            Select(brewer, CocktailKind.HoneyBrew);
            brewer.ProcessControls(true, false, false);
            Assert(brewer.HeldRecipe == honey && brewer.OwnsCombatInput, "Q holds the selected recipe");
            brewer.ProcessControls(false, false, false);
            Assert(brewer.HeldRecipe == null && brewer.SelectedRecipe == honey && !brewer.OwnsCombatInput,
                "Releasing Q preserves selection and restores combat");

            inventory.Add(IngredientType.LifeEssence, 20);
            inventory.Add(IngredientType.Sugar, 1);
            Assert(brewer.ProcessControls(true, true, false) && inventory.GetCount(IngredientType.Sugar) == 1 &&
                inventory.GetCount(IngredientType.LifeEssence) == 20, "Failed craft is atomic and consumes its click");
            brewer.ProcessControls(false, false, true);
            inventory.Add(IngredientType.Sugar, 5);
            brewer.ProcessControls(false, true, false);
            Assert(inventory.GetCount(IngredientType.Sugar) == 6, "Idle LMB never spends ingredients");
            brewer.ProcessControls(false, false, true);

            // Explicit reservation API still supports safe refunds on interruption.
            Assert(brewer.ConfirmSelection(honey), "A throw can reserve its exact costs");
            Assert(inventory.GetCount(IngredientType.Sugar) == 4 && inventory.GetCount(IngredientType.LifeEssence) == 19,
                "Confirmation reserves once");
            brewer.ScrollSelection(1);
            Assert(brewer.SelectedRecipe == honey, "A reserved recipe cannot change");
            brewer.CancelConfirmed(); brewer.CancelConfirmed();
            Assert(inventory.GetCount(IngredientType.Sugar) == 6 && inventory.GetCount(IngredientType.LifeEssence) == 20,
                "Cancel refunds once without duplicating resources");

            var physical = beers.PhysicalCount;
            Select(brewer, CocktailKind.AllYouCanDrink);
            brewer.ProcessControls(true, true, false);
            Assert(beers.PhysicalCount == physical - 1 && effects.InfiniteBeerSecondsLeft > 9f &&
                inventory.GetCount(IngredientType.LifeEssence) == 17, "Q and LMB immediately apply AYCD at exact cost");
            brewer.ProcessControls(true, true, false);
            brewer.ProcessControls(false, false, true);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 17, "A held mouse button cannot double craft");
            brewer.ProcessControls(true, true, false);
            brewer.ProcessControls(false, false, true);
            Assert(inventory.GetCount(IngredientType.LifeEssence) == 17 && beers.PhysicalCount == physical - 1,
                "Active AYCD cannot pay for or extend itself");
            Assert(beers.TryTakeRightmost(out var full) && full == 1f && beers.PhysicalCount == physical - 1,
                "Infinite serving does not consume physical stock");
            Assert(!beers.TryAddGlass(1f), "Infinite servings cannot be banked");
            effects.Clear();
            Assert(beers.TryConsumeForRecipe(beers.PhysicalCount), "Fixture can empty physical beers");
            Assert(!inventory.TryConsume(CocktailRecipes.Find(IngredientMask.LifeEssence), beers, out _) &&
                inventory.GetCount(IngredientType.LifeEssence) == 17, "Missing physical beer fails atomically");
            beers.GrantInfiniteBeer(.08f);
            health.GrantInvulnerability(.08f);
            float hp = health.CurrentHealth;
            health.TakeDamage(20);
            Assert(health.CurrentHealth == hp, "Invulnerability prevents damage");
            yield return new WaitForSeconds(.12f);
            Assert(!beers.HasInfiniteBeer && !beers.HasBeer, "Infinite beer expires to the original empty stock");
            health.TakeDamage(20);
            Assert(health.CurrentHealth == hp - 20f, "Damage resumes after protection expires");

            inventory.Add(IngredientType.Gunpowder, 1);
            Select(brewer, CocktailKind.SteelGutVodka);
            brewer.ProcessControls(true, true, false);
            Assert(effects.InvulnerableSecondsLeft > 5f && inventory.GetCount(IngredientType.LifeEssence) == 15 &&
                inventory.GetCount(IngredientType.Gunpowder) == 0, "Steel-Gut drinks immediately with correct costs");
            brewer.ProcessControls(false, false, true);
            Assert(brewer.ConfirmSelection(honey), "Can reserve a throw before death");
            beers.GrantInfiniteBeer(10);
            health.Die();
            Assert(!beers.HasInfiniteBeer && !health.IsInvulnerable && brewer.ConfirmedRecipe == null &&
                !brewer.IsCrafting && inventory.GetCount(IngredientType.Sugar) == 6 &&
                inventory.GetCount(IngredientType.LifeEssence) == 15, "Death refunds pending costs and clears buffs");
            Debug.Log($"COCKTAIL INVENTORY CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            Invoke(brewer, "OnDisable"); Invoke(effects, "OnDisable");
            Destroy(fixture); Destroy(gameObject);
        }
    }
    private static void Select(CocktailBrewer brewer, CocktailKind kind)
    {
        for (int i = 0; i < CocktailRecipes.All.Count && brewer.SelectedRecipe.Id != kind; i++) brewer.ScrollSelection(1);
    }
    private static void Invoke(MonoBehaviour component, string method)
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
