#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Runs the actual Q, wheel and LMB input route and checks the resulting HUD and AOE preview.</summary>
public class CocktailInputChecks : MonoBehaviour
{
    private int assertions;
    [MenuItem("Hold My Beer/Run Cocktail Input Checks (Play Mode)")]
    public static void Run()
    {
        var player = FindFirstObjectByType<CocktailBrewer>();
        if (!Application.isPlaying || !player || Time.timeScale <= 0f || InputHandler.Instance.ActionsBlocked)
        {
            Debug.LogError("Start unpaused gameplay with the tutorial off before checking cocktail input.");
            return;
        }
        new GameObject("Cocktail Input Checks").AddComponent<CocktailInputChecks>();
    }

    private IEnumerator Start()
    {
        var brewer = FindFirstObjectByType<CocktailBrewer>();
        var inventory = brewer.GetComponent<IngredientInventory>();
        var health = brewer.GetComponent<PlayerHealth>();
        var hud = brewer.GetComponent<CocktailHud>();
        var input = InputHandler.Instance;
        var counts = new int[IngredientInfo.Count];
        for (int i = 0; i < counts.Length; i++) counts[i] = inventory.GetCount((IngredientType)i);
        var selected = brewer.SelectedRecipe;
        var oldProtection = health.InvulnerableSecondsLeft;
        health.GrantInvulnerability(30f);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var beerPresses = 0;
        Action beer = () => beerPresses++;
        input.OnBeerPressed += beer;
        try
        {
            SetCounts(inventory, new int[IngredientInfo.Count]);
            Select(brewer, CocktailKind.HoneyBrew);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frame();
            Check(!hud.IsSelectionEmpty && !hud.IsCraftingVisible && brewer.HeldRecipe == null,
                "Idle HUD shows one selected recipe without entering crafting");
            Capture("idle");
            for (int key = 0; key < 5; key++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState((Key)((int)Key.Digit1 + key)));
                yield return Frame();
                Check(!brewer.IsCrafting && brewer.SelectedRecipe.Id == CocktailKind.HoneyBrew,
                    "Number keys no longer select or craft");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
            yield return Frame();
            Check(brewer.SelectedRecipe.Id == CocktailKind.FizzySodaSplash && !brewer.OwnsCombatInput,
                "Wheel down selects the next recipe while idle without blocking combat");
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
            yield return Frame();
            Check(brewer.SelectedRecipe.Id == CocktailKind.HoneyBrew, "Wheel up selects the previous recipe");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return Frame();
            var arc = brewer.transform.Find("Cocktail aim arc").GetComponent<LineRenderer>();
            var ring = brewer.transform.Find("Cocktail landing radius").GetComponent<LineRenderer>();
            Check(brewer.IsCrafting && hud.IsCraftingVisible && arc.enabled && ring.enabled,
                "Holding Q expands costs and shows the AOE aim before confirmation");
            Capture("crafting-missing");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frame();
            Check(hud.IsShowingFailure && beerPresses == 0 && inventory.GetCount(IngredientType.Sugar) == 0,
                "An unaffordable craft shows feedback, spends nothing and consumes LMB");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frame();
            Check(!brewer.IsCrafting && !hud.IsCraftingVisible && !arc.enabled && !ring.enabled &&
                !hud.IsSelectionEmpty, "Releasing Q hides preview and costs but preserves the selected slot");
            for (int i = 0; i < counts.Length; i++) inventory.Add((IngredientType)i, 12);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
            yield return Frame();
            Check(brewer.SelectedRecipe.Id == CocktailKind.FizzySodaSplash && brewer.IsCrafting && arc.enabled &&
                !hud.IsShowingFailure, "Scrolling during Q updates the recipe and clears its previous error");
            Capture("crafting-ready");
            int water = inventory.GetCount(IngredientType.Water);
            int sugar = inventory.GetCount(IngredientType.Sugar);
            int projectiles = FindObjectsByType<CocktailProjectile>(FindObjectsSortMode.None).Length;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frame();
            Check(brewer.ConfirmedRecipe == null && beerPresses == 0 &&
                inventory.GetCount(IngredientType.Water) == water - 2 &&
                inventory.GetCount(IngredientType.Sugar) == sugar - 1 &&
                FindObjectsByType<CocktailProjectile>(FindObjectsSortMode.None).Length == projectiles + 1,
                "Q plus LMB immediately crafts and throws the selected drink once");
            yield return Frame();
            Check(inventory.GetCount(IngredientType.Water) == water - 2, "Holding LMB does not repeatedly spend");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frame();
            Check(!brewer.IsCrafting && !arc.enabled && brewer.OwnsCombatInput,
                "Releasing Q stops preview while the held mouse click stays captured");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frame();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frame();
            Check(beerPresses == 1 && inventory.GetCount(IngredientType.Water) == water - 2,
                "A fresh LMB without Q reaches normal combat and does not brew");

            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frame();
            Select(brewer, CocktailKind.SteelGutVodka);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return Frame();
            Check(!arc.enabled && !ring.enabled && hud.IsCraftingVisible,
                "Drinkable cocktails show costs without an inappropriate AOE marker");
            int essence = inventory.GetCount(IngredientType.LifeEssence);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frame();
            Check(inventory.GetCount(IngredientType.LifeEssence) == essence - 2 &&
                brewer.GetComponent<PlayerCocktailEffects>().InvulnerableSecondsLeft > 0f,
                "Q plus LMB crafts and uses a drinkable cocktail immediately");

            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frame();
            Select(brewer, CocktailKind.HoneyBrew);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return Frame();
            input.ActionsBlocked = true;
            yield return Frame();
            Check(!brewer.IsCrafting && !arc.enabled && !ring.enabled,
                "Blocked input clears the crafting preview");
            input.ActionsBlocked = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frame();
            Canvas.ForceUpdateCanvases();
            Check(hud.SelectedSlot.rect.width > 0 && hud.SelectedSlot.rect.height > 0,
                "Selected slot has positive layout dimensions");
            var beerCanvas = FindFirstObjectByType<BeerHud>().GetComponent<Canvas>();
            Check(hud.SelectedSlot.GetComponentInParent<Canvas>() == beerCanvas &&
                hud.IngredientPanel.GetComponentInParent<Canvas>() == beerCanvas,
                "Ingredients and selected cocktail share the saved health/beer canvas");
            var uiCamera = beerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : beerCanvas.worldCamera;
            Vector2 selectedScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, hud.SelectedSlot.position);
            Vector2 ingredientScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, hud.IngredientPanel.position);
            Vector3 playerStart = brewer.transform.position;
            Quaternion cameraStart = Camera.main.transform.rotation;
            health.GrantInvulnerability(30f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int i = 0; i < 20; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(4f, 1f) });
                yield return Frame();
                Check(Vector2.Distance(selectedScreen, RectTransformUtility.WorldToScreenPoint(uiCamera,
                    hud.SelectedSlot.position)) < .75f &&
                    Vector2.Distance(ingredientScreen, RectTransformUtility.WorldToScreenPoint(uiCamera,
                    hud.IngredientPanel.position)) < .75f,
                    "Saved HUD stays screen-locked while moving and turning");
            }
            Check(Vector3.Distance(playerStart, brewer.transform.position) > .05f &&
                Quaternion.Angle(cameraStart, Camera.main.transform.rotation) > .1f,
                "Movement test actually moved the player and turned the camera");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frame();
            Capture("movement-stable");
            Debug.Log($"COCKTAIL INPUT CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            input.ActionsBlocked = false;
            brewer.Suspend();
            if (selected != null) Select(brewer, selected.Id);
            SetCounts(inventory, counts);
            health.ClearInvulnerability();
            if (oldProtection > 0f) health.GrantInvulnerability(oldProtection);
            input.OnBeerPressed -= beer;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Destroy(gameObject);
        }
    }

    private static IEnumerator Frame() { yield return null; yield return new WaitForEndOfFrame(); }
    private static void Select(CocktailBrewer brewer, CocktailKind kind)
    {
        for (int i = 0; i < CocktailRecipes.All.Count && brewer.SelectedRecipe.Id != kind; i++) brewer.ScrollSelection(1);
    }
    private static void SetCounts(IngredientInventory inventory, int[] counts)
    {
        var data = new SerializedObject(inventory);
        var names = new[] { "lifeEssence", "water", "sugar", "gunpowder" };
        for (var i = 0; i < counts.Length; i++) data.FindProperty(names[i]).intValue = counts[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Capture(string name)
    {
        var folder = System.IO.Path.GetFullPath("Temp/UIReview/CocktailSceneHud");
        System.IO.Directory.CreateDirectory(folder);
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        Destroy(image);
    }
    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Cocktail input check failed: " + description);
        assertions++;
    }
}
#endif
