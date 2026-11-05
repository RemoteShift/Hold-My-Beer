using UnityEngine;

/// <summary>World-space health bar above an enemy.</summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private RectTransform fillBar;

    private void LateUpdate()
    {
        fillBar.anchorMax = new Vector2(enemy.HealthFraction, 1f);

        var mainCamera = Camera.main;
        if (mainCamera) transform.rotation = mainCamera.transform.rotation;
    }
}
