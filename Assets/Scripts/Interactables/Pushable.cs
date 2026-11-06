using UnityEngine;

/// <summary>
/// Furniture the player can shove. It stays put when walked into, so it only moves when pushed.
/// Its Rigidbody mass is its weight: heavier pieces travel a shorter distance.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pushable : MonoBehaviour
{
    [Tooltip("How quickly a shoved piece slows down, in metres per second per second.")]
    [SerializeField, Min(0.1f)] private float slideDeceleration = 12f;

    private Rigidbody body;
    private Vector3 slideDirection;
    private Vector3 lastPosition;
    private float distanceLeft;
    private float expectedStep;

    public float Weight => body.mass;
    public bool IsSliding { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    /// <summary>Shoves the piece along the floor. It travels the push strength divided by its weight.</summary>
    /// <returns>False if the piece is already sliding.</returns>
    public bool TryPush(Vector3 direction, float pushStrength)
    {
        if (IsSliding) return false;

        slideDirection = direction;
        distanceLeft = pushStrength / Weight;
        lastPosition = body.position;
        expectedStep = 0f;
        IsSliding = true;
        body.isKinematic = false;
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsSliding) return;

        var moved = Vector3.Distance(lastPosition, body.position);
        lastPosition = body.position;
        distanceLeft -= moved;

        if (distanceLeft <= 0f || SlideMotion.IsBlocked(moved, expectedStep))
        {
            StopSliding();
            return;
        }

        var speed = SlideMotion.SpeedToStopWithin(distanceLeft, slideDeceleration);
        body.linearVelocity = slideDirection * speed;
        expectedStep = speed * Time.fixedDeltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsSliding) return;

        var enemy = collision.collider.GetComponentInParent<EnemyBrain>();
        if (!enemy) return;

        enemy.Stun();
        StopSliding();
    }

    private void StopSliding()
    {
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        IsSliding = false;
    }
}
