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
    public float stunDuration = 0.5f;

    [Header("Beer")]
    [Tooltip("How long a full glass freezes this enemy. Partly full glasses freeze for proportionally less.")]
    [Min(0.01f)] public float fullBeerFreezeSeconds = 3f;
    public Color beeredColor = new Color(0.95f, 0.65f, 0.15f);
    [SerializeField] private GameObject heldGlassPrefab;
    [SerializeField] private Transform glassHoldPoint;

    [Header("Pathfinding Settings")] [SerializeField]
    private float repathInterval = 0.15f;
    private float _nextRepathTime;

    private Renderer _bodyRenderer;
    private Color _bodyColor = Color.white;
    private float appliedMaxHealth;
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

    protected void Update()
    {
        if (IsDead) return;
        RefreshHealthCapacity();
        agent.speed = Mathf.Max(0f, moveSpeed) * Buffs.moveSpeed;
        currentState?.Update();
    }

    protected void FixedUpdate()
    {
        if (!IsDead) currentState?.FixedUpdate();
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
        if (!target || !CanNavigate) return;

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

    public virtual void Stun()
    {
        // SwitchState(new EnemyStunState(this, stunDuration));
    }

    public virtual void KnockbackFrom(Vector3 origin, float distance)
    {
        var awayFromOrigin = transform.position - origin;
        awayFromOrigin.y = 0f;

        if (awayFromOrigin.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var desiredPosition = transform.position + awayFromOrigin.normalized * distance;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(desiredPosition, out var navHit, distance, agent.areaMask))
            {
                // Stop at walls instead of warping through them during a shove.
                var end = agent.Raycast(navHit.position, out var obstruction) ? obstruction.position : navHit.position;
                agent.Warp(end);
                return;
            }
        }

        transform.position = desiredPosition;
    }
    
    public virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;
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
