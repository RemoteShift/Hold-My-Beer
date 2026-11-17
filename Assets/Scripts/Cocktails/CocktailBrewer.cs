using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owns selection, reservation and release. InputHandler routes cocktail clicks before combat.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IngredientInventory), typeof(PlayerCocktailEffects), typeof(CocktailTrajectoryPreview))]
[RequireComponent(typeof(CocktailHud))]
public class CocktailBrewer : MonoBehaviour
{
    public int HeldHotkey { get; private set; }
    public CocktailRecipe HeldRecipe => CocktailRecipes.GetByHotkey(HeldHotkey);
    public CocktailRecipe ConfirmedRecipe { get; private set; }
    public CocktailRecipe SelectedRecipe => ConfirmedRecipe ?? HeldRecipe;
    public bool OwnsCombatInput => HeldHotkey != 0 || primaryCaptured || ConfirmedRecipe != null;
    public event Action<string, bool> OnFeedback;

    private IngredientInventory ingredients;
    private BeerInventory beers;
    private PlayerCocktailEffects effects;
    private PlayerHealth health;
    private CocktailTrajectoryPreview preview;
    private bool primaryCaptured;
    private int previousHeldKeys;
    private long pressSequence;
    private long[] keyOrder = new long[CocktailRecipes.HotkeyCount];

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
        if (!input || !input.isActiveAndEnabled || input.ActionsBlocked || Time.timeScale <= 0f || (health && health.IsDead))
            Suspend();
    }

    private void LateUpdate()
    {
        var camera = Camera.main;
        if (ConfirmedRecipe != null && camera)
            preview.Show(ConfirmedRecipe, camera.transform, transform);
        else preview.Hide();
    }

    /// <returns>True when this frame's clicks belong to brewing, including failed or cancelled recipes.</returns>
    public bool PollInput(bool blocked)
    {
        if (!isActiveAndEnabled || blocked || Time.timeScale <= 0f || (health && health.IsDead))
        {
            Suspend();
            return false;
        }
        var keyboard = Keyboard.current;
        var held = 0;
        var pressed = 0;
        if (keyboard != null)
        {
            for (var i = 0; i < CocktailRecipes.HotkeyCount; i++)
            {
                var key = keyboard[(Key)((int)Key.Digit1 + i)];
                if (key.isPressed) held |= 1 << i;
                if (key.wasPressedThisFrame) pressed |= 1 << i;
            }
        }
        var mouse = Mouse.current;
        // Focus changes can lose the release edge. Never keep an abandoned reservation latched.
        if (primaryCaptured && mouse?.leftButton.isPressed != true && mouse?.leftButton.wasReleasedThisFrame != true)
        {
            CancelConfirmed();
            primaryCaptured = false;
        }
        return ProcessControls(held, mouse?.leftButton.wasPressedThisFrame == true,
            mouse?.leftButton.wasReleasedThisFrame == true, mouse?.rightButton.wasPressedThisFrame == true, pressed);
    }

    /// <summary>Explicit edges make the same state machine usable by gameplay and integration checks.</summary>
    public bool ProcessControls(int heldKeys, bool confirmPressed, bool released, bool cancelPressed, int pressedKeys = 0)
    {
        UpdateSelection(heldKeys, pressedKeys);
        var consumed = OwnsCombatInput;
        if (cancelPressed && (primaryCaptured || ConfirmedRecipe != null))
        {
            CancelConfirmed();
            // Keep owning LMB until release: cancelling must never hand a beer or throw later.
            primaryCaptured = true;
            consumed = true;
        }
        else if (confirmPressed && !primaryCaptured && HeldRecipe != null)
        {
            primaryCaptured = true;
            consumed = true;
            ConfirmSelection(HeldRecipe);
        }
        if (released)
        {
            consumed |= primaryCaptured;
            if (ConfirmedRecipe != null) ReleaseConfirmed();
            primaryCaptured = false;
        }
        return consumed;
    }

    /// <summary>Bit 0 is key 1, bit 1 is key 2, etc. Releasing the newest key restores the next held key.</summary>
    private void UpdateSelection(int heldKeys, int pressedKeys)
    {
        heldKeys &= (1 << CocktailRecipes.HotkeyCount) - 1;
        pressedKeys = (pressedKeys | (heldKeys & ~previousHeldKeys)) & heldKeys;
        if (keyOrder == null || keyOrder.Length != CocktailRecipes.HotkeyCount)
            keyOrder = new long[CocktailRecipes.HotkeyCount];
        HeldHotkey = 0;
        var newest = -1L;
        for (var i = 0; i < keyOrder.Length; i++)
        {
            if ((pressedKeys & (1 << i)) != 0) keyOrder[i] = ++pressSequence;
            if ((heldKeys & (1 << i)) == 0 || keyOrder[i] <= newest) continue;
            newest = keyOrder[i];
            HeldHotkey = i + 1;
        }
        previousHeldKeys = heldKeys;
    }

    public bool ConfirmSelection(CocktailRecipe recipe)
    {
        if (ConfirmedRecipe != null || recipe == null) return false;
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
        HeldHotkey = 0;
        previousHeldKeys = 0;
        pressSequence = 0;
        if (keyOrder != null) Array.Clear(keyOrder, 0, keyOrder.Length);
        primaryCaptured = Mouse.current?.leftButton.isPressed == true;
        if (preview) preview.Hide();
    }
}
