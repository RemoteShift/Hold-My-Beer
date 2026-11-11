using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The first game over screen: the frozen bar fades to black, then a red "DEAD" settles
/// into place. After a hold it hands over to whatever comes next, such as the stats screen.
/// Runs on real time, since the game itself is frozen.
/// </summary>
public class DeadScreen : MonoBehaviour
{
    [SerializeField] private GameOverController gameOver;
    [Tooltip("The screen's canvas. Switched off until the player dies.")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private CanvasGroup blackout;
    [SerializeField] private CanvasGroup title;

    [Header("Timing (real seconds)")]
    [SerializeField, Min(0f)] private float fadeToBlackSeconds = 1f;
    [SerializeField, Min(0f)] private float titleSeconds = 0.6f;
    [SerializeField, Min(0f)] private float holdSeconds = 3f;

    [Tooltip("The title starts this much bigger and shrinks into place as it fades in.")]
    [SerializeField, Min(1f)] private float titleStartScale = 1.3f;

    [Tooltip("Raised once the screen has been held, e.g. to show the stats.")]
    public UnityEvent onFinished = new UnityEvent();

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        if (screenRoot) screenRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameOver) gameOver.onGameOver.AddListener(Show);
    }

    private void OnDisable()
    {
        if (gameOver) gameOver.onGameOver.RemoveListener(Show);
    }

    public void Show()
    {
        if (IsShowing) return;
        IsShowing = true;

        StartCoroutine(Play());
    }

    /// <summary>Hides the screen, e.g. when the stats screen takes over.</summary>
    public void Hide()
    {
        if (screenRoot) screenRoot.SetActive(false);
    }

    private IEnumerator Play()
    {
        screenRoot.SetActive(true);
        blackout.alpha = 0f;
        title.alpha = 0f;

        for (var t = 0f; t < fadeToBlackSeconds; t += Time.unscaledDeltaTime)
        {
            blackout.alpha = t / fadeToBlackSeconds;
            yield return null;
        }

        blackout.alpha = 1f;

        var titleTransform = title.transform;
        for (var t = 0f; t < titleSeconds; t += Time.unscaledDeltaTime)
        {
            var progress = Mathf.SmoothStep(0f, 1f, t / titleSeconds);
            title.alpha = progress;
            titleTransform.localScale = Vector3.one * Mathf.Lerp(titleStartScale, 1f, progress);
            yield return null;
        }

        title.alpha = 1f;
        titleTransform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(holdSeconds);

        onFinished.Invoke();
    }
}
