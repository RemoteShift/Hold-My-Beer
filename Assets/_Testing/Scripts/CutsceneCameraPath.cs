using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Flies this camera from a start object (MenuPlaceholder) to every cutscene frame in hierarchy
/// order, then stops on the last one.
///
/// Frames are found by name each time Play() runs, and their CURRENT position, rotation and scale
/// are used while flying. Moving, rotating or rescaling frames never breaks the camera path.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CutsceneCameraPath : MonoBehaviour
{
    // Outer size of the frame prefab in its own local units (the backing panel).
    private const float FrameWidth = 2.3f;
    private const float FrameHeight = 1.425f;

    [Header("Targets (looked up by name)")]
    [SerializeField] private string startObjectName = "MenuPlaceholder";
    [Tooltip("Every object whose name starts with this is a frame. They are visited in hierarchy order.")]
    [SerializeField] private string framePrefix = "CutsceneFrame1";

    [Header("Timing (seconds)")]
    [SerializeField] private float startHoldTime = 1f;
    [SerializeField] private float travelTime = 2f;
    [SerializeField] private float holdTime = 3f;

    [Header("Framing")]
    [Tooltip("How much of the screen the frame fills. Lower = more wall around it.")]
    [Range(0.3f, 1f)] [SerializeField] private float fillAmount = 0.75f;
    [SerializeField] private float fieldOfView = 60f;
    [SerializeField] private float nearClip = 0.02f;
    [Tooltip("Higher than the player camera so this camera renders on top of it.")]
    [SerializeField] private float cameraDepth = 10f;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("Raised once the last frame has been held, e.g. to start gameplay.")]
    public UnityEvent onFinished = new UnityEvent();

    private Camera cam;
    private Coroutine running;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = nearClip;
        cam.depth = cameraDepth;
        SetCameraActive(playOnStart);
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    /// <summary>Starts (or restarts) the camera move from the start object.</summary>
    public void Play()
    {
        BeginPlayback(null);
    }

    /// <summary>Continues from the visible menu view without snapping to the placeholder.</summary>
    public void PlayFrom(Camera source)
    {
        BeginPlayback(source);
    }

    public void Stop()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
        SetCameraActive(false);
    }

    private void SetCameraActive(bool active)
    {
        if (cam == null) cam = GetComponent<Camera>();
        cam.enabled = active;
        var listener = GetComponent<AudioListener>();
        if (listener != null) listener.enabled = active;
    }

    private void BeginPlayback(Camera source)
    {
        Stop();
        cam.fieldOfView = source != null ? source.fieldOfView : fieldOfView;
        if (source != null)
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        SetCameraActive(true);
        running = StartCoroutine(Run(source != null));
    }

    private IEnumerator Run(bool continueFromMenu)
    {
        GameObject startObject = continueFromMenu ? null : GameObject.Find(startObjectName);
        List<Transform> frames = FindFrames();

        if (startObject != null)
        {
            SnapTo(startObject.transform);
        }
        else if (!continueFromMenu)
        {
            Debug.LogWarning($"[CutsceneCameraPath] No object named '{startObjectName}' found. Starting from the camera's current pose.", this);
        }

        if (frames.Count == 0)
        {
            Debug.LogWarning($"[CutsceneCameraPath] No active objects whose name starts with '{framePrefix}' were found.", this);
            running = null;
            onFinished.Invoke();
            yield break;
        }

        if (startObject != null)
        {
            yield return new WaitForSeconds(startHoldTime);
        }

        for (int i = 0; i < frames.Count; i++)
        {
            Transform frame = frames[i];
            if (frame == null)
            {
                continue;
            }

            Vector3 fromPosition = transform.position;
            Quaternion fromRotation = transform.rotation;

            float fromFieldOfView = cam.fieldOfView;
            float t = 0f;
            while (t < 1f)
            {
                if (frame == null)
                {
                    break;
                }

                t += Time.deltaTime / Mathf.Max(travelTime, 0.01f);
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                cam.fieldOfView = Mathf.Lerp(fromFieldOfView, fieldOfView, eased);

                // Re-read the frame's pose every tick so the path follows it if it moves.
                GetPose(frame, out Vector3 position, out Quaternion rotation);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(fromPosition, position, eased),
                    Quaternion.Slerp(fromRotation, rotation, eased));

                yield return null;
            }

            if (frame != null)
            {
                SnapTo(frame);
            }

            // Hold on every frame, including the last, so it can be read before the story ends
            yield return new WaitForSeconds(holdTime);
        }

        running = null;
        onFinished.Invoke();
    }

    private void SnapTo(Transform target)
    {
        GetPose(target, out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// The pose that looks straight at a frame (or the menu board, which uses the same layout).
    /// A frame's front faces its local -Z, so the camera sits on the -forward side looking along +forward.
    /// </summary>
    private void GetPose(Transform target, out Vector3 position, out Quaternion rotation)
    {
        Vector3 scale = target.lossyScale;
        float width = FrameWidth * Mathf.Abs(scale.x);
        float height = FrameHeight * Mathf.Abs(scale.y);

        float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distanceForHeight = height / (2f * tanHalfFov);
        float distanceForWidth = width / (2f * tanHalfFov * cam.aspect);
        float distance = Mathf.Max(distanceForHeight, distanceForWidth) / fillAmount;

        position = target.position - target.forward * distance;
        rotation = Quaternion.LookRotation(target.forward, target.up);
    }

    private List<Transform> FindFrames()
    {
        var result = new List<Transform>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            CollectFrames(root.transform, result);
        }
        return result;
    }

    // Depth-first walk, so the order matches the Hierarchy window top to bottom.
    private void CollectFrames(Transform node, List<Transform> result)
    {
        if (!node.gameObject.activeInHierarchy)
        {
            return;
        }

        if (node.name.StartsWith(framePrefix))
        {
            result.Add(node);
            return;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            CollectFrames(node.GetChild(i), result);
        }
    }
}
