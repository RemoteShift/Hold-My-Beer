using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
    public float maxHealth = 100f;
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

    protected virtual void Awake()
    {
        currentHealth = maxHealth;

        if (!agent) agent = GetComponent<NavMeshAgent>();

        _bodyRenderer = GetComponent<Renderer>();
        if (_bodyRenderer) _bodyColor = _bodyRenderer.material.color;
    }

    protected virtual void Start()
    {
        agent.updateRotation = false;
        target = LocomotionController.Instance.gameObject.transform;
        ReturnToDefaultState();
    }

    protected void Update()
    {
        currentState?.Update();
    }

    protected void FixedUpdate()
    {
        currentState?.FixedUpdate();
    }

    #region State Machine Logic

    public void SwitchState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();

        currentStateName = currentState != null ? currentState.GetType().Name : "None";
    }
    
    public abstract void ReturnToDefaultState();

    #endregion

    #region Beer

    public bool CanBeBeered => currentState != null && currentState.IsAttacking;
    public bool IsBeered => currentState is EnemyBeeredState;

    /// <summary>Beer left in the glass this enemy is holding, from 0 to 1.</summary>
    public float BeerFill => currentState is EnemyBeeredState beered ? beered.Fill : 0f;
    public float BeerSecondsLeft => BeerFill * fullBeerFreezeSeconds;

    /// <summary>Freezes the current attack for as long as the glass lasts.</summary>
    /// <returns>False if the enemy isn't attacking, so the glass stays with the player.</returns>
    public bool TryReceiveBeer(float fill)
    {
        if (!CanBeBeered) return false;

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
        if (!target || !agent.isActiveAndEnabled) return;

        agent.isStopped = false;

        if (Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(target.position);
        }
    }

    public virtual void StopPathfinding()
    {
        if (agent.isActiveAndEnabled)
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
                Time.deltaTime * 10f);
        }
    }

    #endregion

    #region IDamageable Implementation

    /// <summary>Health remaining, from 0 to 1.</summary>
    public float HealthFraction => Mathf.Clamp01(currentHealth / maxHealth);

    public virtual void TakeDamage(float damage)
    {
        Debug.Log("Enemy took damage: " + damage);
        currentHealth -= damage;

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
            if (NavMesh.SamplePosition(desiredPosition, out var navHit, distance, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
                return;
            }
        }

        transform.position = desiredPosition;
    }
    
    public virtual void Die()
    {
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
    
    #region Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }

    #endregion
}
