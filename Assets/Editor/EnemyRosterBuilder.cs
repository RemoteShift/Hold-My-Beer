using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Rebuildable prototype art and a separate encounter, preserving the original level.</summary>
public static class EnemyRosterBuilder
{
    private const string Folder = "Assets/_Game/Prefabs/Enemies/";
    private const string Props = "Assets/_Game/Prefabs/Combat/";
    private const string Pack = "Assets/Plugins/polyperfect/Poly Universal Pack/- Prefabs/";

    [MenuItem("Hold My Beer/Build Enemy Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Props);
        Directory.CreateDirectory("Assets/_Game/Materials/Enemies");
        var chair = Furniture("Throwable Chair", "Steampunk/chair-dining-simple-steampunk.prefab", 1.1f);
        Save(chair.gameObject, Props + "Throwable Chair.prefab");
        var stool = Furniture("Pushable Stool", "Fantasy/Alchemy Fantasy/stool-wooden.prefab", 0.6f);
        Save(stool.gameObject, Props + "Pushable Stool.prefab");
        var chandelier = Furniture("Falling Chandelier", "Fantasy/Light Fantasy/chandelier-candle-a-fantasy.prefab", 1f);
        chandelier.canBePickedUp = false; chandelier.impactDamage = 100f;
        chandelier.GetComponent<Rigidbody>().isKinematic = true;
        chandelier.gameObject.AddComponent<FallingChandelier>();
        Save(chandelier.gameObject, Props + "Falling Chandelier.prefab");

        var hay = Enemy<HaymakerEnemy>("Haymaker", new Color(0.6f, 0.25f, 0.12f));
        hay.maxHealth = 120f; hay.moveSpeed = 1.8f; hay.damage = 35f;
        hay.transform.localScale = new Vector3(1.25f, 1.15f, 1.25f);
        hay.swingPivot = Child(hay.transform, "Swing shoulder", new Vector3(0f, 0.35f, 0f));
        Part(hay.swingPivot, "Swing arm", new Vector3(0f, 0f, 0.8f), new Vector3(0.32f, 0.32f, 1.4f), "Skin", new Color(0.75f, 0.5f, 0.3f));
        Part(hay.swingPivot, "Bare fist", new Vector3(0f, 0f, 1.6f), Vector3.one * 0.5f, "Skin", new Color(0.75f, 0.5f, 0.3f));
        hay.swingPivot.localRotation = Quaternion.Euler(0f, -75f, 0f);
        Part(hay.transform, "Guard fist", new Vector3(-0.65f, 0.2f, 0.45f), Vector3.one * 0.4f, "Skin", new Color(0.75f, 0.5f, 0.3f));
        Save(hay.gameObject, Folder + "Haymaker.prefab");

        var thrower = Enemy<ChairThrowerEnemy>("Chair Thrower", new Color(0.3f, 0.5f, 0.7f));
        thrower.maxHealth = 60f; thrower.moveSpeed = 4f; thrower.damage = 30f;
        thrower.chairHoldPoint = Child(thrower.transform, "Overhead chair grip", new Vector3(0f, 1.75f, 0.25f));
        foreach (var side in new[] { -1f, 1f })
            Part(thrower.transform, "Raised arm", new Vector3(side * 0.5f, 0.9f, 0.25f), new Vector3(0.2f, 1.1f, 0.2f), "Skin", new Color(0.75f, 0.5f, 0.3f));
        var held = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Throwable Chair.prefab"));
        held.transform.SetParent(thrower.chairHoldPoint, false);
        thrower.startingChair = held.GetComponent<ThrowableFurniture>();
        Save(thrower.gameObject, Folder + "Chair Thrower.prefab");

        var keg = Enemy<PowderKegEnemy>("Powder Keg", new Color(0.8f, 0.15f, 0.1f));
        keg.maxHealth = 50f; keg.moveSpeed = 7f; keg.damage = 80f;
        var bundle = Model(keg.transform, "Survival/dynamite-bundle-old.prefab", 0.65f);
        bundle.localPosition = new Vector3(0f, 0.15f, 0.55f);
        keg.fuseVisual = Child(keg.transform, "Burning fuse", new Vector3(0f, 0.45f, 0.7f));
        Part(keg.fuseVisual, "Fuse", new Vector3(0f, 0.15f, 0f), new Vector3(0.04f, 0.3f, 0.04f), "Fuse", Color.yellow);
        var spark = Child(keg.fuseVisual, "Spark", new Vector3(0f, 0.3f, 0f));
        keg.fuseLight = spark.gameObject.AddComponent<Light>();
        keg.fuseLight.color = new Color(1f, 0.5f, 0.1f); keg.fuseLight.range = 2f;
        Save(keg.gameObject, Folder + "Powder Keg.prefab");

        var pianist = Enemy<PianoPlayerEnemy>("Piano Player", new Color(0.55f, 0.25f, 0.7f));
        pianist.maxHealth = 30f; pianist.moveSpeed = 0f; pianist.damage = 0f;
        // Torso + bent legs communicate a seated pose without requiring an animation controller.
        pianist.GetComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Cube);
        var body = pianist.GetComponent<MeshRenderer>(); body.enabled = false;
        Part(pianist.transform, "Seated torso", new Vector3(0f, 0f, 0f), new Vector3(0.65f, 0.8f, 0.45f), "Pianist", new Color(0.55f, 0.25f, 0.7f));
        Part(pianist.transform, "Head", new Vector3(0f, 0.6f, 0f), Vector3.one * 0.4f, "Skin", new Color(0.75f, 0.5f, 0.3f));
        foreach (var side in new[] { -1f, 1f })
        {
            Part(pianist.transform, "Seated leg", new Vector3(side * 0.2f, -0.45f, 0.3f), new Vector3(0.2f, 0.2f, 0.65f), "Trousers", Color.black);
            Part(pianist.transform, "Shin", new Vector3(side * 0.2f, -0.65f, 0.55f), new Vector3(0.2f, 0.5f, 0.2f), "Trousers", Color.black);
        }
        pianist.leftHand = Part(pianist.transform, "Left hand", new Vector3(-0.3f, 0.1f, 0.6f), new Vector3(0.2f, 0.2f, 0.6f), "Skin", new Color(0.75f, 0.5f, 0.3f));
        pianist.rightHand = Part(pianist.transform, "Right hand", new Vector3(0.3f, 0.1f, 0.6f), new Vector3(0.2f, 0.2f, 0.6f), "Skin", new Color(0.75f, 0.5f, 0.3f));
        Part(pianist.transform, "Piano cabinet", new Vector3(0f, 0f, 1.25f), new Vector3(2.2f, 1.6f, 0.65f), "Piano wood", new Color(0.18f, 0.08f, 0.035f));
        Part(pianist.transform, "Keyboard", new Vector3(0f, 0f, 0.75f), new Vector3(2f, 0.12f, 0.4f), "Ivory", Color.white);
        for (var i = 0; i < 14; i++)
            Part(pianist.transform, "Black key", new Vector3(-0.9f + i * 0.135f, 0.09f, 0.85f), new Vector3(0.05f, 0.08f, 0.2f), "Trousers", Color.black);
        var seat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Pushable Stool.prefab"));
        seat.transform.SetParent(pianist.transform, false); seat.transform.localPosition = new Vector3(0f, -0.7f, -0.2f);
        // Independent physics prop: it detaches on Start and can be shoved into the musician.
        seat.AddComponent<DetachFurnitureOnStart>();
        pianist.music = pianist.gameObject.AddComponent<AudioSource>();
        pianist.music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/PianoLoop.wav");
        pianist.music.loop = true; pianist.music.playOnAwake = false; pianist.music.spatialBlend = 1f;
        pianist.music.maxDistance = pianist.musicRadius * 1.5f; pianist.music.volume = 0.5f;
        Save(pianist.gameObject, Folder + "Piano Player.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("Built all four enemy prefabs and shared combat furniture.");
    }

    private static T Enemy<T>(string name, Color color) where T : EnemyBrain
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Charger.prefab"));
        root.name = name;
        var old = root.GetComponent<ChargerEnemy>();
        var source = new SerializedObject(old);
        var glass = source.FindProperty("heldGlassPrefab").objectReferenceValue;
        var grip = source.FindProperty("glassHoldPoint").objectReferenceValue;
        Object.DestroyImmediate(old);
        var enemy = root.AddComponent<T>(); enemy.agent = root.GetComponent<NavMeshAgent>();
        var data = new SerializedObject(enemy);
        data.FindProperty("heldGlassPrefab").objectReferenceValue = glass;
        data.FindProperty("glassHoldPoint").objectReferenceValue = grip;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(component is EnemyHealthBar) && !(component is EnemyFreezeTimer)) continue;
            var serialized = new SerializedObject(component);
            serialized.FindProperty("enemy").objectReferenceValue = enemy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        root.GetComponent<Renderer>().sharedMaterial = Material(name, color);
        return enemy;
    }
    private static ThrowableFurniture Furniture(string name, string model, float height)
    {
        var root = new GameObject(name);
        Model(root.transform, model, height);
        var bounds = new Bounds(root.transform.position, Vector3.zero);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
        var rb = root.AddComponent<Rigidbody>(); rb.mass = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        return root.AddComponent<ThrowableFurniture>();
    }
    private static Transform Model(Transform parent, string path, float height)
    {
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path), parent);
        model.name = Path.GetFileNameWithoutExtension(path);
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        model.transform.localScale *= height / Mathf.Max(0.01f, bounds.size.y);
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        model.transform.position += parent.position - bounds.center;
        return model.transform;
    }
    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position;
        return child;
    }
    private static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, string material, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
        part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = Material(material, color);
        return part.transform;
    }
    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var primitive = GameObject.CreatePrimitive(type); var mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive); return mesh;
    }
    private static Material Material(string name, Color color)
    {
        var path = "Assets/_Game/Materials/Enemies/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color; return material;
    }
    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
    }

    [MenuItem("Hold My Beer/Create Enemy Sandbox From Current Level")]
    public static void CreateSandbox()
    {
        const string path = "Assets/_Testing/Scenes/Enemy Sandbox.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path, true);
        var scene = EditorSceneManager.OpenScene(path);
        var player = Object.FindFirstObjectByType<LocomotionController>();
        if (player && !player.GetComponent<ShoveHandler>()) player.gameObject.AddComponent<ShoveHandler>();
        foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            var propName = body.name.ToLowerInvariant();
            if ((propName.Contains("chair") || propName.Contains("stool")) &&
                !body.GetComponentInParent<EnemyBrain>() && !body.GetComponent<ThrowableFurniture>())
                body.gameObject.AddComponent<ThrowableFurniture>();
        }
        var origin = player ? player.transform.position : Vector3.zero;
        var offsets = new[] { new Vector3(-5,0,5), new Vector3(5,0,6), new Vector3(-6,0,-5), new Vector3(6,0,-5) };
        var names = new[] { "Haymaker", "Chair Thrower", "Powder Keg", "Piano Player" };
        for (var i = 0; i < names.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + names[i] + ".prefab"));
            var desired = origin + offsets[i];
            if (NavMesh.SamplePosition(desired, out var hit, 5f, NavMesh.AllAreas)) desired = hit.position + Vector3.up;
            root.transform.position = desired;
            var facing = origin - desired; facing.y = 0f; root.transform.rotation = Quaternion.LookRotation(facing);
            if (i == 3)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Falling Chandelier.prefab"));
                lamp.transform.position = desired + Vector3.up * 2.6f;
            }
        }
        for (var i = 0; i < 5; i++)
        {
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Pushable Stool.prefab"));
            prop.transform.position = origin + new Vector3(i * 1.5f - 3f, -0.6f, 3f);
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Created Enemy Sandbox with all four enemies, shared furniture, and shoving.");
    }
}
