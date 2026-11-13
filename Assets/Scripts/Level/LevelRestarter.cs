using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The game over buttons. Try Again reloads the level straight into gameplay, skipping the
/// main menu and storyboard. Main Menu reloads it as normal, which opens on the menu.
/// </summary>
public class LevelRestarter : MonoBehaviour
{
    [SerializeField] private StatsScreen statsScreen;
    [SerializeField] private GameplayStarter starter;
    [Tooltip("Switched off when a retry skips the menu.")]
    [SerializeField] private Camera menuCamera;

    // Survives the reload, telling the fresh copy of the level to skip its intro
    private static bool skipIntroOnLoad;

    // Static fields can outlive a Play session in the Editor, so start each one clean
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        skipIntroOnLoad = false;
    }

    private void OnEnable()
    {
        if (!statsScreen) return;

        statsScreen.onTryAgain.AddListener(TryAgain);
        statsScreen.onMainMenu.AddListener(GoToMainMenu);
    }

    private void OnDisable()
    {
        if (!statsScreen) return;

        statsScreen.onTryAgain.RemoveListener(TryAgain);
        statsScreen.onMainMenu.RemoveListener(GoToMainMenu);
    }

    private IEnumerator Start()
    {
        if (!skipIntroOnLoad) yield break;
        skipIntroOnLoad = false;

        // Waits a frame so the main menu has finished setting itself up before it's put away
        yield return null;

        if (menuCamera)
        {
            menuCamera.enabled = false;

            var listener = menuCamera.GetComponent<AudioListener>();
            if (listener) listener.enabled = false;
        }

        if (starter) starter.StartGameplay();
    }

    public void TryAgain()
    {
        skipIntroOnLoad = true;
        ReloadLevel();
    }

    public void GoToMainMenu()
    {
        skipIntroOnLoad = false;
        ReloadLevel();
    }

    private static void ReloadLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // The player and HUD are kept between scenes, but the reloaded level brings its own
        var player = FindFirstObjectByType<LocomotionController>();
        if (player) Destroy(player.transform.root.gameObject);

        var level = SceneManager.GetActiveScene();
        if (level.buildIndex >= 0) SceneManager.LoadScene(level.buildIndex);
        else SceneManager.LoadScene(level.name);
    }
}
