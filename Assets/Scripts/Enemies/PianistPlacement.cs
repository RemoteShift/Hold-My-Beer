using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Pairs a pianist with scene-owned furniture without tying its lifetime to the enemy.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(PianoPlayerEnemy))]
[DefaultExecutionOrder(-100)]
public sealed class PianistPlacement : MonoBehaviour
{
    public PianistStation furniturePrefab;
    public PianistStation station;
    private PianoPlayerEnemy enemy;
    private float seatedHeight;
    private bool initialized;

    private void Awake()
    {
        if (Application.isPlaying) Initialize();
    }

    private void OnEnable()
    {
        if (Application.isPlaying) Initialize();
#if UNITY_EDITOR
        else UnityEditor.EditorApplication.delayCall += EnsureEditorFurniture;
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += EnsureEditorFurniture;
    }

    private void EnsureEditorFurniture()
    {
        if (!this || Application.isPlaying || !gameObject.scene.IsValid() ||
            !gameObject.scene.isLoaded || UnityEditor.EditorUtility.IsPersistent(this) ||
            UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null ||
            UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
        EnsureFurniture();
    }
#endif

    public void EnsureFurniture()
    {
        if (station && station.occupant == this) return;
        if (station && station.occupant && station.occupant != this) station = null;
        if (!station && furniturePrefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                station = ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(
                    furniturePrefab.gameObject, gameObject.scene)).GetComponent<PianistStation>();
                UnityEditor.Undo.RegisterCreatedObjectUndo(station.gameObject, "Place pianist furniture");
                UnityEditor.Undo.RecordObject(this, "Link pianist furniture");
            }
            else
#endif
            {
                station = Instantiate(furniturePrefab);
                SceneManager.MoveGameObjectToScene(station.gameObject, gameObject.scene);
            }
            // The prefab's performer anchor defines its origin relative to the enemy.
            station.transform.SetPositionAndRotation(transform.position, transform.rotation);
            if (station.performerAnchor)
                station.transform.position += transform.position - station.performerAnchor.position;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == "Environment") { station.transform.SetParent(root.transform, true); break; }
        }
        if (!station) return;
        station.occupant = this;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(station);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void OnDisable()
    {
        if (Application.isPlaying && station && station.occupant == this) station.SetOccupied(false);
    }

    private void Initialize()
    {
        if (initialized) return;
        EnsureFurniture();
        enemy = GetComponent<PianoPlayerEnemy>();
        if (station) station.SetOccupied(true);
        seatedHeight = station && station.performerAnchor
            ? station.performerAnchor.position.y : transform.position.y;
        var body = GetComponent<Rigidbody>();
        if (body) { body.isKinematic = true; body.useGravity = false; }
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent)
        {
            agent.updateUpAxis = false;
            agent.updatePosition = false;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        if (station)
            foreach (var own in GetComponentsInChildren<Collider>())
                foreach (var furniture in station.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(own, furniture);
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !initialized || !enemy || enemy.IsDead || enemy.IsLeaving) return;
        // Keep the authored seated height; NavMesh projection must not lift him onto the stool.
        var position = transform.position;
        position.y = seatedHeight;
        transform.position = position;
        var body = GetComponent<Rigidbody>();
        if (body) body.position = position;
        if (enemy.CanNavigate) enemy.agent.nextPosition = position;
    }
}
