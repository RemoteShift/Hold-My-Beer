using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Ends the level once every enemy is out of the fight. When only one enemy is left,
/// it gives up and runs for the exit, and the level ends when it gets there.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Tooltip("A point on the floor just inside the entrance. The last enemy runs here and despawns.")]
    [SerializeField] private Transform exitPoint;
    [Tooltip("Shown when the level ends. Optional.")]
    [SerializeField] private GameObject levelCompleteBanner;

    [Tooltip("Hook for whatever comes after the level, such as loading the next scene.")]
    public UnityEvent onLevelComplete;

    public bool IsComplete { get; private set; }

    private int mostEnemiesSeen;

    private void Awake()
    {
        if (levelCompleteBanner) levelCompleteBanner.SetActive(false);
    }

    private void Update()
    {
        if (IsComplete) return;

        var remaining = 0;
        EnemyBrain last = null;

        foreach (var enemy in EnemyBrain.All)
        {
            if (!enemy || enemy.IsDead) continue;

            remaining++;
            last = enemy;
        }

        mostEnemiesSeen = Mathf.Max(mostEnemiesSeen, remaining);

        // Enemies may not be in play yet, for example while the main menu is up
        if (mostEnemiesSeen == 0) return;

        if (remaining == 0)
        {
            CompleteLevel();
        }
        else if (remaining == 1 && mostEnemiesSeen > 1 && exitPoint)
        {
            last.LeaveThrough(exitPoint.position);
        }
    }

    private void CompleteLevel()
    {
        IsComplete = true;
        if (levelCompleteBanner) levelCompleteBanner.SetActive(true);
        onLevelComplete?.Invoke();
    }
}
