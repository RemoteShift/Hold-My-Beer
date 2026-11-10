using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-off tips that show up when they become relevant, without freezing the game:
/// pushing, jumping over furniture and refilling beer. They run alongside the lessons
/// on their own line, and each one goes away once the player tries it.
/// </summary>
public class TutorialHints : MonoBehaviour
{
    [SerializeField] private TutorialPrompter prompter;
    [Tooltip("Real seconds a tip stays up if the player doesn't try it.")]
    [SerializeField, Min(0f)] private float hintSeconds = 8f;
    [Tooltip("Real seconds between one tip going away and the next showing.")]
    [SerializeField, Min(0f)] private float gapSeconds = 1.5f;
    [Tooltip("Real seconds the main lesson line has to be gone before a tip can show.")]
    [SerializeField, Min(0f)] private float quietSeconds = 2f;
    [Tooltip("How close furniture in front of the player has to be for the jump tip.")]
    [SerializeField, Min(0f)] private float jumpTipDistance = 1.5f;
    [Tooltip("The refill tip shows once the player is down to this many glasses.")]
    [SerializeField, Min(0)] private int lowBeerCount = 1;

    private static readonly Collider[] Nearby = new Collider[16];

    private InputHandler input;
    private Transform player;
    private PushHandler pushHandler;
    private BeerInventory inventory;
    private int pushPresses;

    private sealed class Hint
    {
        public string Text;
        public Func<bool> IsRelevant;
        public Func<Func<bool>> StartTracking;
    }

    private void OnEnable()
    {
        input = InputHandler.Instance;
        if (input) input.OnPushPressed += CountPushPress;
    }

    private void OnDisable()
    {
        if (input) input.OnPushPressed -= CountPushPress;
        if (prompter) prompter.HideHint();
    }

    private void CountPushPress() => pushPresses++;

    private IEnumerator Start()
    {
        var locomotion = FindFirstObjectByType<LocomotionController>();
        player = locomotion ? locomotion.transform : null;
        pushHandler = FindFirstObjectByType<PushHandler>();
        inventory = FindFirstObjectByType<BeerInventory>();

        var pending = new List<Hint>
        {
            new Hint
            {
                Text = "[RMB] Push furniture or beered patrons. Whatever they slam into gets stunned.",
                IsRelevant = () => pushHandler && !string.IsNullOrEmpty(pushHandler.Prompt),
                StartTracking = () =>
                {
                    var pressesBefore = pushPresses;
                    return () => pushPresses > pressesBefore;
                }
            },
            new Hint
            {
                Text = "[Space] Jump over furniture to get around the bar.",
                IsRelevant = IsFurnitureAhead,
                StartTracking = () => () => input && input.JumpPressed
            },
            new Hint
            {
                Text = "Running low! Refill at a tap or barrel with [E]. Taps never run dry, barrels do.",
                IsRelevant = () => inventory && inventory.Count <= lowBeerCount,
                StartTracking = () =>
                {
                    var countBefore = inventory.Count;
                    return () => !inventory || inventory.Count > countBefore;
                }
            }
        };

        while (pending.Count > 0)
        {
            var isQuiet = !prompter.IsFrozen && prompter.QuietSeconds >= quietSeconds;
            var next = isQuiet ? pending.FindIndex(hint => hint.IsRelevant()) : -1;
            if (next < 0)
            {
                yield return null;
                continue;
            }

            var hint = pending[next];
            pending.RemoveAt(next);

            yield return Show(hint.Text, hint.StartTracking());
            yield return new WaitForSecondsRealtime(gapSeconds);
        }
    }

    private IEnumerator Show(string text, Func<bool> tried)
    {
        var shownFor = 0f;

        while (shownFor < hintSeconds && !tried())
        {
            // Out of the way while a lesson is talking or has the game frozen
            if (prompter.IsFrozen || prompter.IsShowing)
            {
                prompter.HideHint();
            }
            else
            {
                prompter.ShowHint(text);
                shownFor += Time.unscaledDeltaTime;
            }

            yield return null;
        }

        prompter.HideHint();
    }

    private bool IsFurnitureAhead()
    {
        if (!player) return false;

        var center = player.position + player.forward * (jumpTipDistance * 0.5f);
        var count = Physics.OverlapSphereNonAlloc(center, jumpTipDistance * 0.5f, Nearby, ~0,
            QueryTriggerInteraction.Ignore);

        // Every piece of furniture carves the NavMesh, which tells it apart from walls and patrons
        for (var i = 0; i < count; i++)
        {
            if (Nearby[i].GetComponentInParent<UnityEngine.AI.NavMeshObstacle>()) return true;
        }

        return false;
    }
}
