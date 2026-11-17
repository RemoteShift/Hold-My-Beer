#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Exercises the real InputHandler and HUD with temporary input devices in a running level.</summary>
public class CocktailInputChecks : MonoBehaviour
{
    private int assertions;

    [MenuItem("Hold My Beer/Run Cocktail Input Checks (Play Mode)")]
    public static void Run()
    {
        var player = FindFirstObjectByType<CocktailBrewer>();
        if (!Application.isPlaying || !player || Time.timeScale <= 0f || InputHandler.Instance.ActionsBlocked)
        {
            Debug.LogError("Start unpaused gameplay outside a tutorial prompt before checking cocktail input.");
            return;
        }
        new GameObject("Cocktail Input Checks").AddComponent<CocktailInputChecks>();
    }

    private IEnumerator Start()
    {
        var brewer = FindFirstObjectByType<CocktailBrewer>();
        var inventory = brewer.GetComponent<IngredientInventory>();
        var input = InputHandler.Instance;
        var hud = brewer.GetComponent<CocktailHud>();
        var savedInventory = new int[IngredientInfo.Count];
        for (var i = 0; i < savedInventory.Length; i++) savedInventory[i] = inventory.GetCount((IngredientType)i);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var beerPresses = 0;
        var unbeerPresses = 0;
        var pushPresses = 0;
        var interactPresses = 0;
        Action beer = () => beerPresses++;
        Action unbeer = () => unbeerPresses++;
        Action push = () => pushPresses++;
        Action interact = () => interactPresses++;
        input.OnBeerPressed += beer;
        input.OnUnbeerPressed += unbeer;
        input.OnPushPressed += push;
        input.OnInteractPressed += interact;
        GameObject wall = null;
        IngredientPickup pickup = null;
        CocktailProjectile launched = null;
        try
        {
            SetCounts(inventory, new int[IngredientInfo.Count]);
            yield return null;
            Check(hud.IsSelectionEmpty, "The one cocktail window is empty when no key is held");
            yield return new WaitForEndOfFrame();
            Capture("/tmp/hmb-cocktail-idle.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            Check(brewer.HeldHotkey == 0 && pushPresses == 1 && interactPresses == 1,
                "E is restored for pushing and refilling");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.Z, Key.C));
            yield return null;
            Check(brewer.HeldHotkey == 0 && hud.IsSelectionEmpty, "Former ingredient keys do not select anything");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            Check(brewer.HeldHotkey == 1 && !hud.IsSelectionEmpty && brewer.ConfirmedRecipe == null,
                "Holding 1 previews Honey before LMB");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1, Key.Digit3));
            yield return null;
            Check(brewer.HeldHotkey == 3, "The newest held cocktail key takes priority");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1, Key.Digit3, Key.Digit2));
            yield return null;
            Check(brewer.HeldHotkey == 2, "A lower-numbered key overrides an older higher-numbered key");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1, Key.Digit3));
            yield return null;
            Check(brewer.HeldHotkey == 3, "Releasing the newest key restores the next most recent held key");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            Check(brewer.HeldHotkey == 1, "Fallback continues to the remaining held key");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(hud.IsSelectionEmpty, "Releasing every number key clears the window");
            for (var i = 0; i < CocktailRecipes.HotkeyCount; i++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState((Key)((int)Key.Digit1 + i)));
                yield return null;
                Check(brewer.SelectedRecipe.Hotkey == i + 1, "Every number key previews its own cocktail");
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Check(brewer.ConfirmedRecipe == null && hud.IsShowingFailure && beerPresses == 0 &&
                inventory.GetCount(IngredientType.Sugar) == 0, "An unaffordable LMB brew shows X and spends nothing");
            yield return new WaitForEndOfFrame();
            Capture("/tmp/hmb-cocktail-error.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            for (var i = 0; i < IngredientInfo.Count; i++) inventory.Add((IngredientType)i, 8);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            Check(!hud.IsShowingFailure, "Changing selection clears stale failure feedback");
            yield return new WaitForEndOfFrame();
            CheckIcons(hud);
            Capture("/tmp/hmb-cocktail-preview.png");
            var sugarBefore = inventory.GetCount(IngredientType.Sugar);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Check(brewer.ConfirmedRecipe?.Id == CocktailKind.HoneyBrew && beerPresses == 0 &&
                inventory.GetCount(IngredientType.Sugar) == sugarBefore - 2,
                "1 and LMB reserve Honey through the real input router without beering");
            yield return new WaitForEndOfFrame();
            Capture("/tmp/hmb-cocktail-selected.png");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(brewer.HeldHotkey == 0 && brewer.ConfirmedRecipe != null,
                "Releasing 1 keeps the held throw confirmed");
            var arc = brewer.transform.Find("Cocktail aim arc").GetComponent<LineRenderer>();
            var ring = brewer.transform.Find("Cocktail landing radius").GetComponent<LineRenderer>();
            Check(arc.enabled && arc.positionCount > 1 && ring.enabled, "A held throw shows its trajectory and floor radius");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            Check(brewer.HeldHotkey == 2 && brewer.SelectedRecipe.Id == CocktailKind.HoneyBrew,
                "A confirmed throw stays locked while another cocktail key is held");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            yield return null;
            Check(brewer.ConfirmedRecipe == null && inventory.GetCount(IngredientType.Sugar) == sugarBefore &&
                unbeerPresses == 0 && !arc.enabled && !ring.enabled,
                "RMB cancels, refunds and hides aim without unbeering");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            Check(FindObjectsByType<CocktailProjectile>(FindObjectsSortMode.None).Length == 0,
                "Releasing a cancelled throw creates no projectile");

            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Check(beerPresses == 1, "A fresh LMB with no cocktail selected still triggers normal beering");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            Check(pushPresses == 2 && interactPresses == 2, "E reaches the existing push and refill handlers");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            launched = FindFirstObjectByType<CocktailProjectile>();
            Check(launched && brewer.ConfirmedRecipe == null && !arc.enabled && !ring.enabled &&
                inventory.GetCount(IngredientType.Sugar) == sugarBefore - 2 && beerPresses == 1,
                "Releasing LMB launches the locked recipe exactly once, keeping its cost and hiding the preview");
            Destroy(launched.gameObject);

            var count = inventory.GetCount(IngredientType.Water);
            pickup = IngredientPickup.Spawn(IngredientType.Water, 3, brewer.transform.position);
            yield return new WaitForSeconds(0.4f);
            Check(!pickup && inventory.GetCount(IngredientType.Water) == count + 3,
                "Walking onto a visible pickup adds its entire quantity once");
            yield return new WaitForSeconds(0.15f);
            Check(inventory.GetCount(IngredientType.Water) == count + 3, "A collected drop cannot award twice");

            var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Pickup line of sight test wall";
            wall.transform.position = brewer.transform.position + forward * 0.75f;
            wall.transform.rotation = Quaternion.LookRotation(forward);
            wall.transform.localScale = new Vector3(2f, 3f, 0.15f);
            pickup = IngredientPickup.Spawn(IngredientType.Water, 2, brewer.transform.position + forward * 1.5f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.4f);
            Check(pickup && inventory.GetCount(IngredientType.Water) == count + 3, "Solid walls prevent collecting through them");
            Destroy(wall);
            yield return new WaitForSeconds(0.2f);
            Check(!pickup && inventory.GetCount(IngredientType.Water) == count + 5, "Clearing the path makes the nearby drop collectable");
            Debug.Log($"COCKTAIL INPUT CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            brewer.CancelConfirmed();
            SetCounts(inventory, savedInventory);
            input.OnBeerPressed -= beer;
            input.OnUnbeerPressed -= unbeer;
            input.OnPushPressed -= push;
            input.OnInteractPressed -= interact;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            if (wall) Destroy(wall);
            if (pickup) Destroy(pickup.gameObject);
            if (launched) Destroy(launched.gameObject);
            Destroy(gameObject);
        }
    }

    private static void SetCounts(IngredientInventory inventory, int[] counts)
    {
        var data = new SerializedObject(inventory);
        var names = new[] { "lifeEssence", "water", "sugar", "gunpowder" };
        for (var i = 0; i < counts.Length; i++) data.FindProperty(names[i]).intValue = counts[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private void CheckIcons(CocktailHud hud)
    {
        var icons = hud.transform.Find("Cocktail HUD").GetComponentsInChildren<CocktailIcon>();
        Check(icons.Length == 7, "Inventory, cocktail logo and two recipe ingredients are the only visible icons");
        foreach (var icon in icons)
        {
            var renderer = icon.GetComponent<CanvasRenderer>();
            Check(renderer, "Each icon has its required canvas renderer");
            var mesh = renderer.GetMesh();
            Check(mesh && mesh.vertexCount > 0 && icon.color.a > 0f, "Each icon has visible vector geometry");
        }
    }

    private static void Capture(string path)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        Destroy(image);
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Cocktail input check failed: " + description);
        assertions++;
    }
}
#endif
