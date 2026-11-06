using UnityEngine;

/// <summary>Finds what the player is looking at within arm's reach.</summary>
public class PlayerAim : MonoBehaviour
{
    [SerializeField] private float reach = 3f;
    [SerializeField] private float aimRadius = 0.3f;
    [Tooltip("Layers that can be aimed at. Leave the player's own layer out.")]
    [SerializeField] private LayerMask aimMask = ~(1 << 3);

    /// <summary>
    /// The way the player is facing along the floor. Comes from the player's yaw rather than
    /// the camera, which has no flat direction when looking straight down.
    /// </summary>
    public Vector3 FlatForward => transform.forward;

    public T FindAimed<T>() where T : Component
    {
        var aimed = FindAimedCollider();
        return aimed ? aimed.GetComponentInParent<T>() : null;
    }

    public Collider FindAimedCollider()
    {
        var mainCamera = Camera.main;
        if (!mainCamera) return null;

        var aim = mainCamera.transform;

        return Physics.SphereCast(aim.position, aimRadius, aim.forward, out var hit, reach, aimMask,
            QueryTriggerInteraction.Ignore)
            ? hit.collider
            : null;
    }
}
