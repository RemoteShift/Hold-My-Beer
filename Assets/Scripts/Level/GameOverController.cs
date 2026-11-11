using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The level's loss condition: when the player's health reaches 0 the run ends. The stats stop,
/// the game freezes, the player's controls switch off and the cursor is freed for the game over screens.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [Tooltip("The player only exists once this has started gameplay. Leave empty if the player is there from the start.")]
    [SerializeField] private GameplayStarter starter;
    [SerializeField] private RunStats stats;

    [Tooltip("Raised once the game has frozen, for the game over screens.")]
    public UnityEvent onGameOver = new UnityEvent();

    public bool IsGameOver { get; private set; }

    private PlayerHealth playerHealth;

    private void Update()
    {
        if (!playerHealth && (!starter || starter.HasStarted)) HookUpPlayer();
    }

    private void OnDestroy()
    {
        if (playerHealth) playerHealth.OnDied -= HandlePlayerDied;

        // Never leave the next scene frozen
        if (IsGameOver) Time.timeScale = 1f;
    }

    private void HookUpPlayer()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (!playerHealth) return;

        // This level ends the run on death instead of respawning the player
        playerHealth.RespawnOnDeath = false;
        playerHealth.OnDied += HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (stats) stats.End();

        FreezeGame();
        onGameOver.Invoke();
    }

    private void FreezeGame()
    {
        // Stops enemies, physics and timers in one go
        Time.timeScale = 0f;

        var locomotion = FindFirstObjectByType<LocomotionController>();
        if (locomotion) locomotion.canMove = false;

        // Also stops the mouse from turning the camera
        var look = FindFirstObjectByType<CameraLook>();
        if (look) look.UnlockCursor();

        // No more beering, pushing or jumping. Menu clicks use the UI's own input, so they still work.
        var input = FindFirstObjectByType<InputHandler>();
        if (input) input.enabled = false;
    }
}
