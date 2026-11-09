using UnityEngine;

/// <summary>
/// Starts gameplay once the opening storyboard is over: the story camera steps aside and
/// the player, enemies and anything else held back during the menu are switched on.
/// </summary>
public class GameplayStarter : MonoBehaviour
{
    [Tooltip("The storyboard that plays after New Game. Gameplay starts when it finishes.")]
    [SerializeField] private CutsceneCameraPath storyboard;
    [Tooltip("Switched off in the scene while the menu is up, and switched on when gameplay starts.")]
    [SerializeField] private GameObject[] enableOnStart;

    public bool HasStarted { get; private set; }

    private void OnEnable()
    {
        if (storyboard) storyboard.onFinished.AddListener(StartGameplay);
    }

    private void OnDisable()
    {
        if (storyboard) storyboard.onFinished.RemoveListener(StartGameplay);
    }

    public void StartGameplay()
    {
        if (HasStarted) return;
        HasStarted = true;

        // Hands the screen over to the player's camera
        if (storyboard) storyboard.Stop();

        foreach (var target in enableOnStart)
        {
            if (target) target.SetActive(true);
        }
    }
}
