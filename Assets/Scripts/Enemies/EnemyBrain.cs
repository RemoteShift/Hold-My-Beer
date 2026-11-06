using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBrain : MonoBehaviour, IDamageable
{
    private EnemyState currentState;
    public EnemyState CurrentState => currentState;
    
    [SerializeField] private string currentStateName;
    
    [Header("Base References")]
    public NavMeshAgent agent;
    public Transform target;
    
    [Header("Base Stats & Detection")]
    [Min(0.01f)] public float maxHealth = 100f;
    [Tooltip("Base navigation speed in metres/second. Music multiplies this value; it does not overwrite it.")]
    [FormerlySerializedAs("sprintSpeed"), Min(0f)] public float moveSpeed = 4f;
    [Tooltip("Facing interpolation rate. Independent of movement and attack speed.")]
    [Min(0f)] public float turnSpeed = 10f;
    [Tooltip("Base outgoing attack damage, before music buffs.")]
    [FormerlySerializedAs("explosionDamage"), Min(0f)] public float damage = 20f;
    [Tooltip("Incoming damage is divided by this value and the music defense multiplier.")]
    [Min(1f)] public float defense = 1f;
    public float currentHealth;
    public float detectRange = 25f;
    public float attackCooldown = 1.5f;
    public float stunDuration = 1.5f;
    public Color stunnedColor = new Color(0.45f, 0.6f, 1f);

    [Header("Beer")]
    [Tooltip("How long a full glass freezes this enemy. Partly full glasses freeze for proportionally less.")]
    [Min(0.01f)] public float fullBeerFreezeSeconds = 3f;
    public Color beeredColor = new Color(0.95f, 0.65f, 0.15f);
    [Header("Push Physics")]
    [Tooltip("Enemy mass. Push travel = strength / mass: mass 50 travels one tenth as far as mass 5.")]
    [FormerlySerializedAs("pushWeight"), Min(0.01f)] public float mass = 10f;
    [Min(0.1f)] public float pushSlideDeceleration = 12f;
    [SerializeField] private float pushHitRadius = 0.35f;
    [SerializeField] private float pushHitForwardOffset = 0.5f;
    [SerializeField] private GameObject heldGlassPrefab;
    [SerializeField] private Transform glassHoldPoint;

    [Header("Pathfinding Settings")] [SerializeField]
    private float repathInterval = 0.15f;
    private float _nextRepathTime;

    /// <summary>Shared by non-allocating overlap checks. Only valid until the next check runs.</summary>
    protected static readonly Collider[] OverlapBuffer = new Collider[16];

    private Renderer _bodyRenderer;
    private Color _bodyColor = Color.white;
    private float appliedMaxHealth;
    private Vector3 pushDirection;
    private const float PushFinishDistance = 0.001f;
    private float pushDistanceLeft;
    private float pushImpactDamage;
    private float beerImpactStunLeft;
    public bool IsImpactStunned => beerImpactStunLeft > 0f;
    public bool IsBeingPushed => pushDistanceLeft > 0f;
    public float PushDistanceLeft => pushDistanceLeft;
    public float PushDistanceFor(float strength) => Mathf.Max(0f, strength) / Mathf.Max(0.01f, mass);
    public bool IsDead { get; private set; }
    public EnemyBuffs Buffs => PianoPlayerEnemy.GetBuffs(this);
    public float MoveSpeedMultiplier => Buffs.moveSpeed;
    public float WindupDeltaTime => Time.deltaTime * Buffs.windupSpeed;
    public float AttackDeltaTime => Time.deltaTime * Buffs.attackSpeed;
    public float RecoveryDeltaTime => Time.deltaTime * Buffs.recoverySpeed;
    public float EffectiveDamage => damage * Buffs.damage;
    public float EffectiveMaxHealth => Mathf.Max(0.01f, maxHealth * Buffs.maxHealth);
    public bool CanNavigate => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

    /// <summary>Preserve HP percentage when capacity changes, so toggling an aura cannot heal wounds.</summary>
    public void RefreshHealthCapacity()
    {
        if (IsDead) return;
        var capacity = EffectiveMaxHealth;
        if (appliedMaxHealth > 0f && !Mathf.Approximately(capacity, appliedMaxHealth))
            currentHealth *= capacity / appliedMaxHealth;
        appliedMaxHealth = capacity;
    }

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        appliedMaxHealth = Mathf.Max(0.01f, maxHealth);

        if (!agent) agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        _bodyRenderer = GetComponent<Renderer>();
        if (_bodyRenderer) _bodyColor = _bodyRenderer.material.color;
    }

    protected virtual void Start()
    {
        agent.updateRotation = false;
        if (!target)
        {
            var player = FindFirstObjectByType<LocomotionController>();
            if (player) target = player.transform;
        }
        ReturnToDefaultState();
    }

    protected virtual void Update()
    {
        if (IsDead) return;
        RefreshHealthCapacity();
        agent.speed = Mathf.Max(0f, moveSpeed) * Buffs.moveSpeed;
        if (IsImpactStunned)
        {
            beerImpactStunLeft = Mathf.Max(0f, beerImpactStunLeft - Time.deltaTime);
            if (!IsImpactStunned && IsBeered) SetBodyColor(beeredColor);
            return;
        }
        currentState?.Update();
        if (!IsDead) UpdatePush();
    }

    protected void FixedUpdate()
    {
        if (!IsDead && !IsImpactStunned) currentState?.FixedUpdate();
    }

    #region State Machine Logic

    public void SwitchState(EnemyState newState)
    {
        if (IsDead && newState != null) return;
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();

        currentStateName = currentState != null ? currentState.GetType().Name : "None";
    }
    
    public abstract void ReturnToDefaultState();

    #endregion

    #region Beer

    public bool CanBeBeered => !IsDead && isActiveAndEnabled && currentState != null && currentState.IsAttacking;
    public bool IsBeered => currentState is EnemyBeeredState;

    /// <summary>Beer left in the glass this enemy is holding, from 0 to 1.</summary>
    public float BeerFill => currentState is EnemyBeeredState beered ? beered.Fill : 0f;
    public float BeerSecondsLeft => BeerFill * fullBeerFreezeSeconds;

    /// <summary>Freezes the current attack for as long as the glass lasts.</summary>
    /// <returns>False if the enemy isn't attacking, so the glass stays with the player.</returns>
    public bool TryReceiveBeer(float fill)
    {
        if (!CanBeBeered || fill <= 0f || float.IsNaN(fill)) return false;
        fill = Mathf.Clamp01(fill);

        SwitchState(new EnemyBeeredState(this, fill, currentState));
        return true;
    }

    /// <summary>Unbeers the enemy, resuming their attack.</summary>
    /// <returns>False if the enemy isn't holding a beer.</returns>
    public bool TryTakeBackBeer(out float fill)
    {
        fill = 0f;
        if (currentState is not EnemyBeeredState beered) return false;

        fill = beered.TakeGlassBack();
        return true;
    }

    /// <summary>Shoves a beered enemy across the floor. They stay frozen and keep drinking.</summary>
    /// <returns>False if the enemy isn't beered.</returns>
    public bool TryPush(Vector3 direction, float pushStrength, float impactDamage = 0f)
    {
        if (!IsBeered || IsBeingPushed || IsImpactStunned || !CanNavigate) return false;
        return BeginPush(direction, pushStrength, impactDamage);
    }

    private EnemyBrain FindPushVictim(Vector3 direction)
    {
        var center = transform.position + direction * pushHitForwardOffset;
        var count = Physics.OverlapSphereNonAlloc(center, pushHitRadius, OverlapBuffer, ~0,
            QueryTriggerInteraction.Ignore);
        for (var i = 0; i < count; i++)
        {
            var other = OverlapBuffer[i].GetComponentInParent<EnemyBrain>();
            if (other && other != this && !other.IsDead) return other;
        }
        return null;
    }

    private void ReceivePushImpact(float impactDamage)
    {
        if (IsDead) return;
        pushDistanceLeft = 0f;
        StopPathfinding();
        TakeDamage(impactDamage);
        if (IsDead) return;
        if (IsBeered)
        {
            // Keep the glass and interrupted attack intact, pausing their update together.
            beerImpactStunLeft = Mathf.Max(beerImpactStunLeft, stunDuration);
            SetBodyColor(stunnedColor);
        }
        else Stun();
    }

    private void ResolvePushImpact(EnemyBrain other)
    {
        var impactDamage = pushImpactDamage;
        pushDistanceLeft = 0f;
        pushImpactDamage = 0f; // Consume before damage can trigger a death explosion.
        ReceivePushImpact(impactDamage);
        if (other && other != this) other.ReceivePushImpact(impactDamage);
    }

    public GameObject SpawnHeldGlass()
    {
        if (!heldGlassPrefab || !glassHoldPoint) return null;

        var glass = Instantiate(heldGlassPrefab, glassHoldPoint);

        foreach (var glassCollider in glass.GetComponentsInChildren<Collider>())
        {
            glassCollider.enabled = false;
        }

        return glass;
    }

    #endregion

    #region Shared helper states

    public virtual bool CanSeeTarget()
    {
        if (!target) return false;
        return Vector3.Distance(transform.position, target.position) <= detectRange;
    }

    public virtual void PathfindToTarget()
    {
        if (!target || !CanNavigate || IsBeingPushed) return;

        agent.isStopped = false;

        if (Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(target.position);
        }
    }

    public virtual void StopPathfinding()
    {
        if (CanNavigate)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public virtual void LookAtTarget()
    {
        if (!target) return;
        
        var dir = target.position - transform.position;
        dir.y = 0f;

        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 
                Time.deltaTime * turnSpeed * Buffs.turnSpeed);
        }
    }

    #endregion

    #region IDamageable Implementation

    /// <summary>Health remaining, from 0 to 1.</summary>
    public float HealthFraction
    {
        get { RefreshHealthCapacity(); return Mathf.Clamp01(currentHealth / EffectiveMaxHealth); }
    }

    public virtual void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        RefreshHealthCapacity();
        currentHealth -= damage / (Mathf.Max(1f, defense) * Buffs.defense);

        StartCoroutine(BlinkRed());
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>Interrupts the enemy. A stun doesn't affect someone already frozen by a beer.</summary>
    public virtual void Stun()
    {
        if (IsDead || IsBeered) return;

        SwitchState(new EnemyStunState(this, stunDuration));
    }

    /// <summary>All forced enemy movement uses the same mass response, including explosions.</summary>
    public virtual void KnockbackFrom(Vector3 origin, float pushStrength)
    {
        BeginPush(transform.position - origin, pushStrength);
    }

    private bool BeginPush(Vector3 direction, float strength, float impactDamage = 0f)
    {
        if (IsDead || !isActiveAndEnabled || strength <= 0f || float.IsNaN(strength) || float.IsInfinity(strength)) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        pushImpactDamage = Mathf.Max(0f, impactDamage);
        pushDirection = direction.normalized;
        pushDistanceLeft = PushDistanceFor(strength);
        StopPathfinding();
        return true;
    }

    private void UpdatePush()
    {
        if (pushDistanceLeft <= PushFinishDistance) { pushDistanceLeft = 0f; return; }
        if (!CanNavigate) { pushDistanceLeft = 0f; return; }
        var speed = SlideMotion.SpeedToStopWithin(pushDistanceLeft, Mathf.Max(0.1f, pushSlideDeceleration));
        var travel = Mathf.Min(speed * Time.deltaTime, pushDistanceLeft);
        // Substeps prevent light enemies skipping past patrons or walls at high slide speed.
        while (travel > 0.0001f)
        {
            var step = Mathf.Min(travel, Mathf.Max(0.05f, pushHitRadius * 0.5f));
            var before = transform.position;
            var blocked = agent.Raycast(before + pushDirection * step, out _);
            // Baked navigation keeps the agent centre away from walls by its radius.
            // Include a small bake tolerance when a navigation edge stops this step.
            var collisionRadius = Mathf.Max(pushHitRadius, agent.radius);
            var collisionTravel = step + (blocked ? 0.2f : 0f);
            RaycastHit? firstHit = null;
            foreach (var hit in Physics.SphereCastAll(before, collisionRadius, pushDirection, collisionTravel, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (!firstHit.HasValue || hit.distance < firstHit.Value.distance) firstHit = hit;
            }
            if (firstHit.HasValue)
            {
                ResolvePushImpact(firstHit.Value.collider.GetComponentInParent<EnemyBrain>());
                return;
            }
            // A NavMesh boundary alone is not a physical wall impact.
            if (blocked) { pushDistanceLeft = 0f; return; }
            var victim = FindPushVictim(pushDirection);
            if (victim) { ResolvePushImpact(victim); return; }
            agent.Move(pushDirection * step);
            var moved = Vector3.Distance(before, transform.position);
            if (SlideMotion.IsBlocked(moved, step)) { pushDistanceLeft = 0f; return; }
            pushDistanceLeft = Mathf.Max(0f, pushDistanceLeft - moved);
            if (pushDistanceLeft <= PushFinishDistance) { pushDistanceLeft = 0f; return; }
            travel -= step;
        }
    }

    public virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;
        pushDistanceLeft = 0f;
        StopPathfinding();
        SwitchState(null);
        Destroy(gameObject);
    }
    
    #endregion

    #region Coroutines

    private IEnumerator BlinkRed()
    {
        if (!_bodyRenderer) yield break;

        _bodyRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.25f);

        _bodyRenderer.material.color = _bodyColor;
    }

    #endregion

    #region Body Color

    public void SetBodyColor(Color color)
    {
        _bodyColor = color;
        if (_bodyRenderer) _bodyRenderer.material.color = color;
    }

    #endregion
    
}
