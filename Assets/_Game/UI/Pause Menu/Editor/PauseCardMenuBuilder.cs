using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Creates reusable mesh, material and prefab assets from the supplied card silhouettes.</summary>
public static class PauseCardMenuBuilder
{
    private const string Root = "Assets/_Game/UI/Pause Menu";
    private const string Art = "Assets/_Game/UI/Art/Menus/Pause Menu";
    private static readonly string[] CardNames = { "Controls", "Audio", "Resume", "Restart", "Main_Menu", "Quit" };

    [MenuItem("Tools/Hold My Beer/Create Playing Card Pause Menu")]
    public static void BuildForActiveScene()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<PauseCardMenu>(FindObjectsInactive.Include);
        if (existing) { Selection.activeGameObject = existing.gameObject; return; }
        var menu = UnityEngine.Object.FindFirstObjectByType<GreenWellMenu>(FindObjectsInactive.Include);
        var restarter = UnityEngine.Object.FindFirstObjectByType<LevelRestarter>(FindObjectsInactive.Include);
        if (!menu || !restarter) throw new InvalidOperationException("The existing main menu and level restarter are required.");
        Directory.CreateDirectory(Root + "/Models"); Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs"); AssetDatabase.Refresh();
        foreach (var name in CardNames)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/Pause_" + name + ".png");
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        var mesh = CreateCardMesh(Art + "/Pause_" + CardNames[0] + ".png");
        AssetDatabase.CreateAsset(mesh, Root + "/Models/Playing Card.asset");
        var shader = Shader.Find("Hold My Beer/Pause Card");
        if (!shader) throw new InvalidOperationException("Pause Card shader has not imported.");
        var side = new Material(shader) { name = "Card stock edge and back" };
        side.SetColor("_BaseColor", new Color32(126, 42, 39, 255));
        AssetDatabase.CreateAsset(side, Root + "/Materials/Card stock edge and back.mat");
        var root = new GameObject("Playing Card Pause Menu");
        Undo.RegisterCreatedObjectUndo(root, "Create playing card pause menu");
        var controller = root.AddComponent<PauseCardMenu>();
        controller.backgroundBlurShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/PauseGaussianBlur.shader");
        var stage = new GameObject("Card Stage"); stage.transform.SetParent(root.transform, false);
        stage.transform.position = new Vector3(0, -6000, 0); stage.layer = PauseCardMenu.CardLayer;
        controller.stage = stage.transform;
        var cameraObject = new GameObject("Pause Card Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -5);
        cameraObject.layer = PauseCardMenu.CardLayer;
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = false; camera.fieldOfView = 40f;
        camera.nearClipPlane = .05f; camera.farClipPlane = 10; camera.cullingMask = 1 << PauseCardMenu.CardLayer;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.allowHDR = false; camera.allowMSAA = true; camera.enabled = false;
        var data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.renderShadows = false; data.volumeLayerMask = 1;
        data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
        controller.cardCamera = camera;
        controller.cards = new Transform[CardNames.Length];
        for (int i = 0; i < CardNames.Length; i++)
        {
            var name = CardNames[i].Replace('_', ' ');
            var cardMesh = mesh;
            if (i != 0)
            {
                cardMesh = CreateCardMesh(Art + "/Pause_" + CardNames[i] + ".png");
                AssetDatabase.CreateAsset(cardMesh, Root + "/Models/" + name + " Playing Card.asset");
            }
            var material = new Material(shader) { name = name + " original artwork" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Pause_" + CardNames[i] + ".png"));
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            var card = new GameObject(name + " Card"); card.layer = PauseCardMenu.CardLayer;
            var model = new GameObject("Seamless Card Model", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            model.layer = PauseCardMenu.CardLayer; model.transform.SetParent(card.transform, false);
            model.transform.localPosition = new Vector3(0, .6f, 0);
            model.GetComponent<MeshFilter>().sharedMesh = cardMesh;
            var renderer = model.GetComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { material, side };
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            model.GetComponent<MeshCollider>().sharedMesh = cardMesh;
            var path = Root + "/Prefabs/" + name + " Card.prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(card, path);
            UnityEngine.Object.DestroyImmediate(card);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, stage.transform);
            instance.transform.localPosition = new Vector3(i * .075f, -.60f, i * .055f);
            controller.cards[i] = instance.transform;
        }
        stage.SetActive(false);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, Root + "/Pause Card Menu.prefab", InteractionMode.AutomatedAction);
        controller.settingsMenus = UnityEngine.Object.FindFirstObjectByType<SharedSettingsMenus>(FindObjectsInactive.Include); controller.restarter = restarter;
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
    }

    private static Mesh CreateCardMesh(string path)
    {
        var texture = new Texture2D(2, 2);
        ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
        int width = texture.width, height = texture.height;
        var pixels = texture.GetPixels32(); var outline = new List<Vector2>();
        // Pixel boundaries, rather than a guessed corner radius, define the card stock outline.
        for (int y = 0; y < height; y++)
        {
            int left = 0, right = width - 1;
            while (left < width && pixels[y * width + left].a < 128) left++;
            while (right >= 0 && pixels[y * width + right].a < 128) right--;
            if (right < left) continue;
            outline.Add(new Vector2(left, y)); outline.Add(new Vector2(left, y + 1));
            outline.Add(new Vector2(right + 1, y)); outline.Add(new Vector2(right + 1, y + 1));
        }
        UnityEngine.Object.DestroyImmediate(texture);
        var hull = Hull(outline);
        float cardHeight = 1.65f, cardWidth = cardHeight * width / height;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>();
        var front = new List<int>(); var stock = new List<int>();
        Func<Vector2, float, float, int> add = (point, depth, inset) =>
        {
            Vector2 coords = new Vector2(point.x / width, point.y / height);
            float x = (coords.x - .5f) * cardWidth * inset;
            float y = (coords.y - .5f) * cardHeight * inset;
            float bend = -.004f * Mathf.Pow(x / (cardWidth * .5f), 2);
            vertices.Add(new Vector3(x, y, depth + bend)); uv.Add(coords); return vertices.Count - 1;
        };
        var center = new Vector2(width * .5f, height * .5f);
        int frontCenter = add(center, -.003f, 1);
        var frontRing = hull.Select(p => add(p, -.003f, 1)).ToArray();
        int backCenter = add(center, .003f, .998f);
        var backRing = hull.Select(p => add(p, .003f, .998f)).ToArray();
        var sideFront = hull.Select(p => add(p, -.0028f, .998f)).ToArray();
        for (int i = 0; i < hull.Count; i++)
        {
            int next = (i + 1) % hull.Count;
            front.AddRange(new[] { frontCenter, frontRing[next], frontRing[i] });
            stock.AddRange(new[] { backCenter, backRing[i], backRing[next] });
            stock.AddRange(new[] { sideFront[i], sideFront[next], backRing[next], sideFront[i], backRing[next], backRing[i] });
        }
        var mesh = new Mesh { name = "Playing Card - artwork silhouette, thin curved stock" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = 2;
        mesh.SetTriangles(front, 0); mesh.SetTriangles(stock, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector2> Hull(List<Vector2> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
        var hull = new List<Vector2>();
        foreach (var point in sorted)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }
        int lower = hull.Count;
        for (int i = sorted.Length - 2; i >= 0; i--)
        {
            while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(sorted[i]);
        }
        hull.RemoveAt(hull.Count - 1); return hull;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    { return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x); }
}
