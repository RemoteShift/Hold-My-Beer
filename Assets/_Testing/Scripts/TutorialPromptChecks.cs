#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Real input checks for self-paced reading, reach gating, cleanup and banner layout.</summary>
public class TutorialPromptChecks : MonoBehaviour
{
    private int assertions;
    [MenuItem("Hold My Beer/Run Tutorial Prompt Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        new GameObject("Tutorial prompt checks").AddComponent<TutorialPromptChecks>();
    }

    private IEnumerator Start()
    {
        var fixture = new GameObject("Tutorial UI fixture");
        fixture.SetActive(false);
        var p = fixture.AddComponent<TutorialPrompter>();
        var banner = new GameObject("Banner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        banner.transform.SetParent(fixture.transform, false);
        banner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        banner.GetComponent<Canvas>().sortingOrder = 100;
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(banner.transform, false);
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        var label = textObject.GetComponent<TextMeshProUGUI>();
        Set(p, "banner", banner);
        Set(p, "bannerText", label);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var oldScale = Time.timeScale;
        var oldFixed = Time.fixedDeltaTime;
        var input = InputHandler.Instance;
        var oldBlocked = input && input.ActionsBlocked;
        try
        {
            Time.timeScale = 1f;
            if (input) input.ActionsBlocked = false;
            fixture.SetActive(true);
            p.SetLesson("2 / 2  •  TURN THE PUNCH AGAINST THEM");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            p.StartCoroutine(p.Explain("An enemy is in his path. Take the beer back to release the punch.\nGet close, aim at the Haymaker and press [RMB]."));
            yield return new WaitForSecondsRealtime(.2f);
            Check(p.IsReading && Time.timeScale == 0f, "Holding Enter cannot skip a new card");
            Check(!input || input.ActionsBlocked, "Combat input is blocked while reading");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(5.2f);
            Check(p.IsReading && p.IsShowing, "Card remains beyond old timers and ignores combat clicks");
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            Check(label.preferredHeight <= label.rectTransform.rect.height, "Longest teaching card fits without clipping");
            Check(label.preferredWidth <= label.rectTransform.rect.width + 1f, "Card fits its available width");
            yield return new WaitForEndOfFrame();
            Capture("/tmp/hmb-tutorial-reading.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null; yield return null; yield return null;
            Check(!p.IsReading && !p.IsShowing && Time.timeScale == 1f, "Fresh Enter resumes the game");
            Check(!input || !input.ActionsBlocked, "Reading restores combat input");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            var ready = false;
            var done = false;
            p.StartCoroutine(p.FreezePrompt("Release the punch.\nAim at the Haymaker and press [RMB] to take your beer back.", () => done, () => ready));
            yield return null;
            Check(Time.timeScale > 0f && Time.timeScale < 1f, "Out-of-reach practice allows slow repositioning");
            Check(label.text.Contains("MOVE CLOSER"), "Out-of-reach prompt explains the required action");
            ready = true;
            yield return null; yield return null;
            Check(Time.timeScale == 0f && p.IsShowing, "In-reach practice pauses and waits for the real action");
            Check(label.text.Contains("IN REACH"), "Ready state has explicit feedback");
            yield return new WaitForEndOfFrame();
            Capture("/tmp/hmb-tutorial-practice.png");
            ready = false;
            yield return null; yield return null;
            Check(Time.timeScale > 0f, "Losing aim permits repositioning instead of trapping player");
            done = true;
            yield return new WaitForSecondsRealtime(.6f);
            Check(!p.IsShowing && !p.IsFrozen && Time.timeScale == 1f, "Completion restores normal time");
            p.StartCoroutine(p.Prompt("Already complete", () => true));
            Check(!p.IsShowing, "Already-completed objectives do not flash a prompt");
            p.StartCoroutine(p.Explain("Cleanup check"));
            yield return null;
            fixture.SetActive(false);
            Check(Time.timeScale == 1f && !p.IsReading, "Disabling tutorial releases paused time");
            Check(!input || !input.ActionsBlocked, "Disabling tutorial releases input lock");
            Debug.Log($"TUTORIAL PROMPT CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            Destroy(fixture);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Time.timeScale = oldScale;
            Time.fixedDeltaTime = oldFixed;
            if (input) input.ActionsBlocked = oldBlocked;
            Destroy(gameObject);
        }
    }
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Capture(string path)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        Destroy(image);
    }
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception("TUTORIAL PROMPT CHECK FAILED: " + message);
        assertions++;
    }
}
#endif
