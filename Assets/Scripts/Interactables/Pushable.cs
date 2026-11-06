using UnityEngine;

/// <summary>Mass-based floor slide. ThrowableFurniture owns pickup and impact damage.</summary>
[RequireComponent(typeof(Rigidbody), typeof(ThrowableFurniture))]
public class Pushable : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float slideDeceleration = 12f;
    private Rigidbody body;
    private ThrowableFurniture furniture;
    private Vector3 slideDirection;
    private Vector3 lastPosition;
    private float distanceLeft;
    private float expectedStep;
    private bool restoreKinematic;
    public float Weight => Mathf.Max(0.01f, body.mass);
    public bool IsSliding { get; private set; }
    public bool CanPush => isActiveAndEnabled && !IsSliding && furniture && !furniture.IsHeld;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        furniture = GetComponent<ThrowableFurniture>();
        if (!furniture) furniture = gameObject.AddComponent<ThrowableFurniture>();
    }

    public bool TryPush(Vector3 direction, float pushStrength)
    {
        direction.y = 0f;
        if (!CanPush || pushStrength <= 0f || !float.IsFinite(pushStrength) || direction.sqrMagnitude < 0.0001f) return false;
        slideDirection = direction.normalized;
        distanceLeft = pushStrength / Weight;
        lastPosition = body.position;
        expectedStep = 0f;
        restoreKinematic = body.isKinematic;
        IsSliding = true;
        body.isKinematic = false;
        furniture.Arm(furniture.impactDamage);
        return true;
    }

    /// <summary>External throws, pickups and blasts take exclusive ownership of physics.</summary>
    public void ReleaseToPhysics()
    {
        IsSliding = false;
        distanceLeft = expectedStep = 0f;
        body.isKinematic = false;
    }

    private void FixedUpdate()
    {
        if (!IsSliding) return;
        if (furniture.IsHeld) { IsSliding = false; return; }
        var displacement = body.position - lastPosition;
        displacement.y = 0f;
        var moved = displacement.magnitude;
        lastPosition = body.position;
        distanceLeft -= moved;
        if (distanceLeft <= 0f || SlideMotion.IsBlocked(moved, expectedStep)) { StopSliding(); return; }
        var speed = SlideMotion.SpeedToStopWithin(distanceLeft, Mathf.Max(0.1f, slideDeceleration));
        furniture.SetSlideVelocity(slideDirection * speed + Vector3.up * body.linearVelocity.y);
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
        if (!body.isKinematic) body.linearVelocity = Vector3.up * body.linearVelocity.y;
        body.isKinematic = restoreKinematic;
        IsSliding = false;
    }
}
