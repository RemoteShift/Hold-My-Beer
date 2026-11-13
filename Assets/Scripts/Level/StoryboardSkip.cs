using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Testing shortcut: pressing the skip key while the opening storyboard plays jumps straight
/// into gameplay. It isn't shown anywhere in the game.
/// </summary>
[RequireComponent(typeof(GameplayStarter))]
public class StoryboardSkip : MonoBehaviour
{
    [SerializeField] private Key skipKey = Key.L;
    [Tooltip("The storyboard's camera. It's only switched on while the storyboard plays.")]
    [SerializeField] private Camera storyboardCamera;

    private GameplayStarter starter;

    private void Awake()
    {
        starter = GetComponent<GameplayStarter>();
    }

    private void Update()
    {
        if (starter.HasStarted || !storyboardCamera || !storyboardCamera.enabled) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[skipKey].wasPressedThisFrame)
        {
            starter.StartGameplay();
        }
    }
}
