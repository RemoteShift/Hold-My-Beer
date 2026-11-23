using UnityEngine;

/// <summary>Level furniture, independent of its optional performer.</summary>
[DisallowMultipleComponent]
public sealed class PianistStation : MonoBehaviour
{
    public Transform performerAnchor;
    public Transform piano;
    public Transform chair;
    [HideInInspector] public PianistPlacement occupant;
    private bool capturedChairState, holdingChair;
    private bool wasKinematic, couldBePickedUp;
    private Rigidbody chairBody;
    private ThrowableFurniture chairFurniture;

    public void SetOccupied(bool occupied)
    {
        if (!Application.isPlaying || !chair) return;
        if (!capturedChairState)
        {
            chairBody = chair.GetComponent<Rigidbody>();
            chairFurniture = chair.GetComponent<ThrowableFurniture>();
            if (!chairBody) return;
            wasKinematic = chairBody.isKinematic;
            couldBePickedUp = chairFurniture && chairFurniture.canBePickedUp;
            capturedChairState = true;
        }
        chairBody.isKinematic = occupied || wasKinematic;
        if (chairFurniture) chairFurniture.canBePickedUp = !occupied && couldBePickedUp;
        holdingChair = occupied;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        var enemy = occupant ? occupant.GetComponent<PianoPlayerEnemy>() : null;
        var occupied = enemy && enemy.isActiveAndEnabled && !enemy.IsDead && !enemy.IsLeaving;
        if (occupied != holdingChair) SetOccupied(occupied);
    }
}
