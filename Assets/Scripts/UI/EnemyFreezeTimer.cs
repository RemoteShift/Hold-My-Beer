using TMPro;
using UnityEngine;

/// <summary>World-space countdown above an enemy, showing how long their beer will hold them.</summary>
public class EnemyFreezeTimer : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private TMP_Text secondsText;

    private void LateUpdate()
    {
        var isBeered = enemy.IsBeered;
        if (panel.activeSelf != isBeered) panel.SetActive(isBeered);
        if (!isBeered) return;

        fillBar.anchorMax = new Vector2(enemy.BeerFill, 1f);
        secondsText.text = $"{enemy.BeerSecondsLeft:0.0}s";

        var mainCamera = Camera.main;
        if (mainCamera) transform.rotation = mainCamera.transform.rotation;
    }
}
