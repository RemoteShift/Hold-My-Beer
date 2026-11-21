using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Scroll selects a recipe; hold Q to aim, then click LMB to craft and use it.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IngredientInventory), typeof(PlayerCocktailEffects), typeof(CocktailTrajectoryPreview))]
[RequireComponent(typeof(CocktailHud))]
public class CocktailBrewer : MonoBehaviour
{
    [SerializeField, Min(0)] private int selectedIndex;
    public bool IsCrafting { get; private set; }
    public CocktailRecipe SelectedRecipe
    {
        get
        {
            var recipes = CocktailRecipes.All;
            if (recipes.Count == 0) return null;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, recipes.Count - 1);
            return ConfirmedRecipe ?? recipes[selectedIndex];
        }
    }
    public CocktailRecipe HeldRecipe => IsCrafting ? SelectedRecipe : null;
    public CocktailRecipe ConfirmedRecipe { get; private set; }
    public bool OwnsCombatInput => IsCrafting || primaryCaptured || ConfirmedRecipe != null;
    public event Action<string, bool> OnFeedback;

    private IngredientInventory ingredients;
    private BeerInventory beers;
    private PlayerCocktailEffects effects;
    private PlayerHealth health;
    private CocktailTrajectoryPreview preview;
    private bool primaryCaptured;

    private void Awake()
    {
        ingredients = GetComponent<IngredientInventory>();
        beers = GetComponent<BeerInventory>();
        effects = GetComponent<PlayerCocktailEffects>();
        health = GetComponent<PlayerHealth>();
        preview = GetComponent<CocktailTrajectoryPreview>();
    }

    private void OnEnable() { if (health) health.OnDied += HandleDeath; }
    private void OnDisable()
    {
        if (health) health.OnDied -= HandleDeath;
        Suspend();
    }
    private void HandleDeath() => Suspend();
    private void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }

    private void Update()
    {
        var input = InputHandler.Instance;
        if (!input || !input.isActiveAndEnabled || input.ActionsBlocked || Time.timeScale <= 0f ||
            AudioListener.pause || (health && health.IsDead))
            Suspend();
    }

    private void LateUpdate() { RefreshPreview(); }

    private void RefreshPreview()
    {
        if (!preview) return;
        var camera = Camera.main;
        if (HeldRecipe != null && camera)
            preview.Show(HeldRecipe, camera.transform, transform);
        else preview.Hide();
    }

    /// <returns>True while cocktail crafting owns combat input, including failed crafting clicks.</returns>
    public bool PollInput(bool blocked)
    {
        if (!isActiveAndEnabled || blocked || Time.timeScale <= 0f || AudioListener.pause || (health && health.IsDead))
        {
            Suspend();
            return false;
        }
        var mouse = Mouse.current;
        // Losing focus may lose a release edge. Do not leave a mouse click captured forever.
        if (primaryCaptured && mouse?.leftButton.isPressed != true && mouse?.leftButton.wasReleasedThisFrame != true)
        {
            CancelConfirmed();
            primaryCaptured = false;
        }
        var scroll = mouse?.scroll.y.ReadValue() ?? 0f;
        var steps = scroll > 0f ? -1 : scroll < 0f ? 1 : 0;
        return ProcessControls(Keyboard.current?.qKey.isPressed == true,
            mouse?.leftButton.wasPressedThisFrame == true, mouse?.leftButton.wasReleasedThisFrame == true, steps);
    }

    /// <summary>Explicit input edges also allow integration checks without simulating combat.</summary>
    public bool ProcessControls(bool craftHeld, bool confirmPressed, bool released, int scrollSteps = 0)
    {
        if (scrollSteps != 0) ScrollSelection(scrollSteps);
        IsCrafting = craftHeld;
        var consumed = OwnsCombatInput;
        if (confirmPressed && !primaryCaptured && HeldRecipe != null)
        {
            primaryCaptured = true;
            consumed = true;
            // Match the existing immediate craft-and-use behaviour for throws and drinks.
            if (ConfirmSelection(HeldRecipe)) ReleaseConfirmed();
        }
        if (released)
        {
            consumed |= primaryCaptured;
            primaryCaptured = false;
        }
        RefreshPreview();
        return consumed;
    }

    /// <summary>Positive steps select the next recipe; negative steps select the previous. Wraps both ways.</summary>
    public void ScrollSelection(int steps)
    {
        var count = CocktailRecipes.All.Count;
        if (count == 0 || ConfirmedRecipe != null) return;
        selectedIndex = ((selectedIndex + steps % count) % count + count) % count;
    }

    public bool ConfirmSelection(CocktailRecipe recipe)
    {
        if (ConfirmedRecipe != null || recipe == null) return false;
        if (recipe.IsThrowable && !Camera.main) return false;
        if (!ingredients.TryConsume(recipe, beers, out var error))
        {
            OnFeedback?.Invoke(error, true);
            return false;
        }
        if (recipe.IsThrowable)
        {
            ConfirmedRecipe = recipe;
            OnFeedback?.Invoke(recipe.Name + " ready", false);
        }
        else
        {
            effects.Apply(recipe);
            OnFeedback?.Invoke(recipe.Name + $"  -  {recipe.Duration:0}s", false);
        }
        return true;
    }

    public void ReleaseConfirmed()
    {
        if (ConfirmedRecipe == null) return;
        var camera = Camera.main;
        if (!camera) { CancelConfirmed(); return; }
        var recipe = ConfirmedRecipe;
        ConfirmedRecipe = null;
        preview.Hide();
        CocktailProjectile.Launch(recipe, camera.transform, transform);
        OnFeedback?.Invoke(recipe.Name + " thrown", false);
    }

    public void CancelConfirmed()
    {
        if (ConfirmedRecipe == null) return;
        ingredients.Refund(ConfirmedRecipe);
        ConfirmedRecipe = null;
        preview.Hide();
        OnFeedback?.Invoke("Cancelled - ingredients returned", false);
    }

    public void Suspend()
    {
        CancelConfirmed();
        IsCrafting = false;
        // Keep the selected recipe visible and remember it across focus changes and pauses.
        primaryCaptured = Mouse.current?.leftButton.isPressed == true;
        if (preview) preview.Hide();
    }
}
