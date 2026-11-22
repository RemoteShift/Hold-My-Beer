using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

/// <summary>Pooled, unlit flipbook player for the imported sheets; never changes gameplay.</summary>
[DefaultExecutionOrder(220)]
public sealed class CocktailPackEffect : MonoBehaviour
{
    private static readonly Dictionary<CocktailVfxLibrary.Clip, UnityEngine.Pool.ObjectPool<CocktailPackEffect>> pools =
        new Dictionary<CocktailVfxLibrary.Clip, UnityEngine.Pool.ObjectPool<CocktailPackEffect>>();
    private static Transform poolRoot;
    private UnityEngine.Pool.ObjectPool<CocktailPackEffect> pool;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;
    private Transform follow;
    private Vector3 position, normal, followOffset, direction;
    private float age, width, opacity;
    private bool loop, ground, followRequired;
    public CocktailVfxLibrary.Clip Clip { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool Paused { get; set; }
    public float CameraOffset { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPools() { pools.Clear(); poolRoot = null; }

    public static CocktailPackEffect Play(CocktailVfxLibrary.Clip clip, Vector3 point,
        Vector3 surfaceNormal, float worldWidth, bool repeating, bool onGround, float alpha,
        Transform target = null, Vector3 offset = default)
    {
        if (!Application.isPlaying || clip == null || !clip.material || clip.frameCount < 1) return null;
        if (!poolRoot)
        {
            pools.Clear();
            var root = new GameObject("Cocktail VFX pool");
            DontDestroyOnLoad(root);
            poolRoot = root.transform;
        }
        if (!pools.TryGetValue(clip, out var pool))
        {
            pool = new UnityEngine.Pool.ObjectPool<CocktailPackEffect>(() => Create(clip), null,
                effect => effect.gameObject.SetActive(false),
                effect => { if (effect) Destroy(effect.gameObject); }, true, 4, 64);
            pools.Add(clip, pool);
        }
        var item = pool.Get();
        item.pool = pool;
        item.position = point;
        item.normal = surfaceNormal.sqrMagnitude > .001f ? surfaceNormal.normalized : Vector3.up;
        item.width = Mathf.Max(.01f, worldWidth);
        item.loop = repeating;
        item.ground = onGround;
        item.opacity = Mathf.Clamp01(alpha);
        item.follow = target;
        item.followRequired = target;
        item.followOffset = offset;
        item.direction = Vector3.zero;
        item.CameraOffset = .015f;
        item.age = 0f;
        item.Paused = false;
        item.IsPlaying = true;
        item.gameObject.SetActive(true);
        item.RenderFrame();
        return item;
    }

    private static CocktailPackEffect Create(CocktailVfxLibrary.Clip clip)
    {
        var go = new GameObject("VFX " + clip.name);
        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);
        var effect = go.AddComponent<CocktailPackEffect>();
        effect.Clip = clip;
        effect.properties = new MaterialPropertyBlock();
        effect.mesh = new Mesh { name = "Cocktail pack quad" };
        float x = clip.pivot.x, y = clip.pivot.y;
        effect.mesh.vertices = new[] { new Vector3(-x,-y,0), new Vector3(-x,1-y,0),
            new Vector3(1-x,1-y,0), new Vector3(1-x,-y,0) };
        effect.mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        effect.mesh.triangles = new[] { 0,1,2,0,2,3 };
        effect.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
        effect.meshRenderer = go.AddComponent<MeshRenderer>();
        effect.meshRenderer.sharedMaterial = clip.material;
        effect.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        effect.meshRenderer.receiveShadows = false;
        return effect;
    }

    public void SetDirection(Vector3 value) { direction = value; }
    public void SetFollowOffset(Vector3 value) { followOffset = value; }
    public void SetGroundPose(Vector3 point, Vector3 surfaceNormal, float diameter)
    {
        position = point;
        normal = surfaceNormal;
        width = Mathf.Max(.01f, diameter);
        RenderFrame();
    }
    public void Sample(float seconds) { age = Mathf.Max(0f, seconds); Paused = true; RenderFrame(); }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        if (followRequired && (!follow || !follow.gameObject.activeInHierarchy)) { Release(); return; }
        if (!Paused) age += Time.deltaTime;
        if (!loop && age >= Clip.frameCount / Clip.framesPerSecond) { Release(); return; }
        RenderFrame();
    }

    private void RenderFrame()
    {
        if (!IsPlaying || !meshRenderer) return;
        int frame = Mathf.FloorToInt(age * Clip.framesPerSecond);
        frame = loop ? frame % Clip.frameCount : Mathf.Min(frame, Clip.frameCount - 1);
        var texture = Clip.material.mainTexture;
        float u = (float)Clip.frameSize.x / texture.width, v = (float)Clip.frameSize.y / texture.height;
        properties.SetVector("_Frame", new Vector4(u, v, frame % Clip.columns * u,
            1f - (frame / Clip.columns + 1) * v));
        properties.SetFloat("_Opacity", opacity);
        meshRenderer.SetPropertyBlock(properties);
        Vector3 point = follow ? follow.position + followOffset : position;
        if (ground)
        {
            var up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (up.sqrMagnitude < .001f) up = Vector3.right;
            transform.SetPositionAndRotation(point + normal * .04f, Quaternion.LookRotation(normal, up));
            transform.localScale = new Vector3(width, width, 1f);
        }
        else
        {
            var camera = Camera.main;
            var rotation = camera ? camera.transform.rotation : Quaternion.identity;
            if (camera && direction.sqrMagnitude > .001f)
            {
                var projected = new Vector2(Vector3.Dot(direction, camera.transform.right),
                    Vector3.Dot(direction, camera.transform.up));
                rotation *= Quaternion.AngleAxis(Mathf.Atan2(projected.y, projected.x) * Mathf.Rad2Deg, Vector3.forward);
            }
            if (camera) point += (camera.transform.position - point).normalized * CameraOffset;
            transform.SetPositionAndRotation(point, rotation);
            transform.localScale = new Vector3(width, width * Clip.frameSize.y / Clip.frameSize.x, 1f);
        }
    }

    public void Release()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        follow = null;
        followRequired = false;
        pool.Release(this);
    }

    private void OnDestroy() { if (mesh) Destroy(mesh); }
}
