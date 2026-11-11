using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The second game over screen, after "DEAD": how the run went (enemies beered, enemies remaining,
/// refills used, time survived) with buttons to try again or go back to the main menu.
/// Runs on real time, since the game itself is frozen.
/// </summary>
public class StatsScreen : MonoBehaviour
{
    [SerializeField] private DeadScreen deadScreen;
    [SerializeField] private RunStats stats;

    [Header("Screen")]
    [Tooltip("The screen's canvas. Switched off until the DEAD screen finishes.")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private CanvasGroup group;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

    [Header("Stats")]
    [SerializeField] private TMP_Text enemiesBeeredValue;
    [SerializeField] private TMP_Text enemiesRemainingValue;
    [SerializeField] private TMP_Text refillsUsedValue;
    [SerializeField] private TMP_Text timeSurvivedValue;

    [Header("Buttons")]
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button mainMenuButton;

    public UnityEvent onTryAgain = new UnityEvent();
    public UnityEvent onMainMenu = new UnityEvent();

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        if (screenRoot) screenRoot.SetActive(false);

        if (tryAgainButton) tryAgainButton.onClick.AddListener(() => onTryAgain.Invoke());
        if (mainMenuButton) mainMenuButton.onClick.AddListener(() => onMainMenu.Invoke());
    }

    private void OnEnable()
    {
        if (deadScreen) deadScreen.onFinished.AddListener(Show);
    }

    private void OnDisable()
    {
        if (deadScreen) deadScreen.onFinished.RemoveListener(Show);
    }

    public void Show()
    {
        if (IsShowing) return;
        IsShowing = true;

        if (stats)
        {
            enemiesBeeredValue.text = stats.EnemiesBeered.ToString();
            if (enemiesRemainingValue) enemiesRemainingValue.text = stats.EnemiesRemaining.ToString();
            refillsUsedValue.text = stats.RefillsUsed.ToString();
            timeSurvivedValue.text = FormatTime(stats.SecondsSurvived);
        }

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        screenRoot.SetActive(true);
        group.alpha = 0f;
        group.interactable = false;

        for (var t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeSeconds;
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;

        // Nothing of the DEAD screen shows through any more
        if (deadScreen) deadScreen.Hide();

        // Lets keyboard and controller players pick a button straight away
        if (EventSystem.current && tryAgainButton) EventSystem.current.SetSelectedGameObject(tryAgainButton.gameObject);
    }

    private static string FormatTime(float seconds)
    {
        var whole = Mathf.FloorToInt(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }
}
