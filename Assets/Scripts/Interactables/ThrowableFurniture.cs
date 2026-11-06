using System.Collections.Generic;
using UnityEngine;

/// <summary>Furniture is armed by a throw/shove/blast, and disarmed by any non-damageable collision.</summary>
[RequireComponent(typeof(Rigidbody))]
public class ThrowableFurniture : MonoBehaviour
{
    public bool canBePickedUp = true;
    [Min(0f)] public float impactDamage = 30f;
    [Min(0f)] public float minimumImpactSpeed = 2f;

    [Header("Held Pose")]
    [Tooltip("Optional grip on this item. Its position and rotation align with the enemy's hold point. Without a grip, the visual bounds centre is aligned instead.")]
    public Transform gripPoint;
    public Vector3 heldPositionOffset;
    public Vector3 heldRotationOffset;

    public bool IsHeld => holder;
    public bool IsAvailableForPickup => isActiveAndEnabled && canBePickedUp && !IsHeld && Body &&
        (!Body.isKinematic || GetComponent<Pushable>());
    public bool IsDangerous { get; private set; }
    public Rigidbody Body { get; private set; }
    private EnemyBrain holder;
    private Transform holdPoint;
    private Vector3 heldLocalPosition;
    private Quaternion heldLocalRotation;
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private Collider[] colliders;
    private bool[] colliderEnabled;
    private float graceUntil;
    private EnemyBrain thrower;
    private float speedBeforePhysics;
    private float armedDamage;
    private Vector3 pendingVelocityChange;
    private RigidbodyInterpolation freeInterpolation;

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        colliderEnabled = new bool[colliders.Length];
    }

    /// <summary>Measure reach to the actual prop, not an arbitrary model/root pivot.</summary>
    public Vector3 GetPickupPoint(Vector3 from)
    {
        var closest = transform.position;
        var distance = float.PositiveInfinity;
        foreach (var collider in colliders)
        {
            if (!collider || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != Body) continue;
            var point = collider.ClosestPoint(from);
            var squared = (point - from).sqrMagnitude;
            if (squared >= distance) continue;
            closest = point; distance = squared;
        }
        return closest;
    }

    public bool TryHold(EnemyBrain owner, Transform point)
    {
        if (!canBePickedUp || IsHeld || !owner || !point || !isActiveAndEnabled) return false;
        GetComponent<Pushable>()?.ReleaseToPhysics();
        holder = owner; holdPoint = point;
        IsDangerous = false;
        pendingVelocityChange = Vector3.zero;
        if (!Body.isKinematic)
        { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
        freeInterpolation = Body.interpolation;
        Body.interpolation = RigidbodyInterpolation.None;
        Body.isKinematic = true;

        // Remove the previous physics pose before aligning. A per-item grip also supports off-centre model pivots.
        var gripRotation = gripPoint ? Quaternion.Inverse(transform.rotation) * gripPoint.rotation : Quaternion.identity;
        transform.SetParent(point, true);
        transform.localRotation = Quaternion.Euler(heldRotationOffset) * Quaternion.Inverse(gripRotation);
        transform.localPosition = heldPositionOffset;
        var center = transform.position;
        if (gripPoint) center = gripPoint.position;
        else
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                center = bounds.center;
            }
        }
        transform.position += point.TransformPoint(heldPositionOffset) - center;
        heldLocalPosition = transform.localPosition;
        heldLocalRotation = transform.localRotation;
        for (var i = 0; i < colliders.Length; i++)
        { colliderEnabled[i] = colliders[i].enabled; colliders[i].enabled = false; }
        SnapHeldPose();
        hitTargets.Clear();
        return true;
    }

    private void LateUpdate()
    {
        if (IsHeld) SnapHeldPose();
    }

    private void SnapHeldPose()
    {
        if (!holdPoint) return;
        transform.SetLocalPositionAndRotation(heldLocalPosition, heldLocalRotation);
        Body.position = transform.position;
        Body.rotation = transform.rotation;
    }

    public void Release(Vector3 velocity, float damage)
    {
        if (!IsHeld) return;
        SnapHeldPose();
        thrower = holder; holder = null; holdPoint = null; graceUntil = Time.time + 0.35f;
        transform.SetParent(null, true);
        Body.isKinematic = false;
        Body.interpolation = freeInterpolation;
        for (var i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderEnabled[i];
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        pendingVelocityChange = Vector3.zero;
        Body.linearVelocity = velocity;
        Body.angularVelocity = Vector3.right * 4f;
        Arm(damage);
    }

    public void Arm(float damage)
    {
        if (IsHeld) return;
        IsDangerous = true;
        // Scene stools may still be configured as Discrete, unlike the authored projectile prefab.
        // Continuous detection prevents fast props passing through enemy capsules between ticks.
        if (!Body.isKinematic) Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        armedDamage = Mathf.Max(0f, damage);
        hitTargets.Clear();
        speedBeforePhysics = Body.linearVelocity.magnitude;
    }

    public void Shove(Vector3 impulse)
    {
        if (IsHeld) return;
        GetComponent<Pushable>()?.ReleaseToPhysics();
        if (Body.isKinematic) return;
        thrower = null;
        Arm(impactDamage);
        TrackPendingImpulse(impulse / Body.mass);
        Body.AddForce(impulse, ForceMode.Impulse);
    }

    public void ApplyBlast(Vector3 velocityChange)
    {
        if (IsHeld || velocityChange.sqrMagnitude <= 0f) return;
        GetComponent<Pushable>()?.ReleaseToPhysics();
        if (Body.isKinematic) return;
        thrower = null;
        Arm(impactDamage);
        Body.WakeUp();
        TrackPendingImpulse(velocityChange);
        Body.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    public void SetSlideVelocity(Vector3 velocity)
    {
        Body.linearVelocity = velocity;
        speedBeforePhysics = velocity.magnitude;
    }

    private void TrackPendingImpulse(Vector3 velocityChange)
    {
        // AddForce is integrated by the next simulation. A hit in that first step must not
        // be rejected because the cached velocity was sampled before the shove/blast.
        pendingVelocityChange += velocityChange;
        speedBeforePhysics = (Body.linearVelocity + pendingVelocityChange).magnitude;
    }

    private void FixedUpdate()
    {
        speedBeforePhysics = (Body.linearVelocity + pendingVelocityChange).magnitude;
        pendingVelocityChange = Vector3.zero;
    }

    private void OnCollisionEnter(Collision collision) { HandleCollision(collision); }
    private void OnCollisionStay(Collision collision) { HandleCollision(collision); }

    private void HandleCollision(Collision collision)
    {
        if (IsHeld) return;
        var victim = collision.collider.GetComponentInParent<IDamageable>();
        // This comes before the speed check: even a gentle touch of floor/wall disarms it.
        if (victim == null) { IsDangerous = false; return; }
        // CCD contacts can report zero relativeVelocity after the solver has stopped the prop.
        // Its recorded pre-simulation speed is the impact speed, not the post-contact velocity.
        if (!IsDangerous || speedBeforePhysics < minimumImpactSpeed) return;
        if (Time.time < graceUntil && ReferenceEquals(victim, thrower)) return;
        if (hitTargets.Add(victim)) victim.TakeDamage(armedDamage);
    }
}
