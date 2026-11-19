using UnityEngine;

/// <summary>Amber pack bubbles identify beer sources while they can still dispense beer.</summary>
[DisallowMultipleComponent]
public sealed class BeerSourceVfx : MonoBehaviour
{
    [SerializeField] private BeerSource source;
    [SerializeField] private GameObject beerBubbles;

    private void LateUpdate()
    {
        // BeerSource is queried directly by BeerHandler, even when its component is unchecked.
        bool visible = source && source.gameObject.activeInHierarchy && !source.IsEmpty;
        if (beerBubbles && beerBubbles.activeSelf != visible) beerBubbles.SetActive(visible);
    }
    private void OnDisable() { if (beerBubbles) beerBubbles.SetActive(false); }
}
