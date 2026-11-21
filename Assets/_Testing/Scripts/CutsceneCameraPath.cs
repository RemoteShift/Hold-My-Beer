using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// Plays the opening cutscene on the game's own camera - the same Main Camera object that ends
/// up on the player. There is no separate cutscene camera.
///
/// The camera starts life inside the player rig, which stays switched off during the menu. For the
/// cutscene this component lifts that camera out of the rig, lets Cinemachine drive it through a
/// Timeline (one Cinemachine shot per picture frame), and puts it back on the player's camera holder
/// - at the exact local pose and lens it had - when gameplay starts.
///
/// It is also the controller the rest of the game talks to (menu, gameplay starter, skip key):
/// <see cref="PlayFrom"/>, <see cref="Stop"/> and <see cref="onFinished"/>.
/// </summary>
public class CutsceneCameraPath : MonoBehaviour
{
    [Header("Timeline")]
    [Tooltip("Runs the cutscene timeline. Its Cinemachine track is bound to the game camera's brain.")]
    [SerializeField] private PlayableDirector director;
    [Tooltip("A Cinemachine camera that the first shot blends away from. It is placed at the menu view each time the cutscene starts.")]
    [SerializeField] private CinemachineCamera startView;

    [Header("The game's camera")]
    [Tooltip("The one camera used for the cutscene and for gameplay (the player's Main Camera).")]
    [SerializeField] private Camera gameCamera;
    [Tooltip("The Cinemachine brain on the game camera. Only switched on while the cutscene plays.")]
    [SerializeField] private CinemachineBrain brain;
    [Tooltip("Tells a stop for gameplay (hand the camera to the player) apart from a stop for the menu.")]
    [SerializeField] private GameplayStarter starter;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("Raised once the last frame has been held, e.g. to start gameplay.")]
    public UnityEvent onFinished = new UnityEvent();

    // Where the camera lives during gameplay, remembered so it can be put back exactly.
    private Transform home;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 homeScale;
    private float homeFieldOfView;
    private float homeNearClip;
    private float homeFarClip;

    private bool atHome = true;
    private bool playing;

    private void Awake()
    {
        if (gameCamera == null || brain == null)
        {
            Debug.LogError("[CutsceneCameraPath] The game camera and its Cinemachine brain must be assigned.", this);
            return;
        }

        Transform t = gameCamera.transform;
        home = t.parent;
        homePosition = t.localPosition;
        homeRotation = t.localRotation;
        homeScale = t.localScale;
        homeFieldOfView = gameCamera.fieldOfView;
        homeNearClip = gameCamera.nearClipPlane;
        homeFarClip = gameCamera.farClipPlane;

        // The menu is up first, so the camera waits (switched off) outside the player rig.
        Park();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!playing || director == null)
        {
            return;
        }

        // The director holds on its last frame, so reaching the end of the timeline means the
        // last picture has been held for its full time.
        if (director.duration > 0d && director.time >= director.duration)
        {
            playing = false;
            onFinished.Invoke();
        }
    }

    /// <summary>Starts (or restarts) the cutscene from wherever the game camera currently is.</summary>
    public void Play()
    {
        BeginPlayback(null);
    }

    /// <summary>Starts the cutscene from the menu camera's view, so the first move doesn't snap.</summary>
    public void PlayFrom(Camera source)
    {
        BeginPlayback(source);
    }

    /// <summary>
    /// Ends the cutscene. If gameplay has started the game camera goes back onto the player;
    /// otherwise (the menu is showing) it is put away again.
    /// </summary>
    public void Stop()
    {
        playing = false;
        if (director != null)
        {
            director.Stop();
        }

        if (gameCamera == null || brain == null)
        {
            return;
        }

        if (starter != null && starter.HasStarted)
        {
            HandOverToPlayer();
        }
        else
        {
            Park();
        }
    }

    private void BeginPlayback(Camera source)
    {
        if (director == null || director.playableAsset == null || gameCamera == null || brain == null)
        {
            Debug.LogWarning("[CutsceneCameraPath] The cutscene timeline or game camera isn't set up.", this);
            onFinished.Invoke();
            return;
        }

        playing = false;
        director.Stop();
        Park();

        // The first shot blends away from the view the player is looking at right now.
        Transform from = source != null ? source.transform : gameCamera.transform;
        float fieldOfView = source != null ? source.fieldOfView : gameCamera.fieldOfView;
        float nearClip = source != null ? source.nearClipPlane : gameCamera.nearClipPlane;
        float farClip = source != null ? source.farClipPlane : gameCamera.farClipPlane;

        gameCamera.transform.SetPositionAndRotation(from.position, from.rotation);
        gameCamera.fieldOfView = fieldOfView;
        PlaceStartView(from.position, from.rotation, fieldOfView, nearClip, farClip);

        SetCameraActive(true);
        brain.enabled = true;

        director.extrapolationMode = DirectorWrapMode.Hold;
        director.time = 0d;
        director.Play();
        playing = true;
    }

    private void PlaceStartView(Vector3 position, Quaternion rotation, float fieldOfView, float nearClip, float farClip)
    {
        if (startView == null)
        {
            return;
        }

        startView.transform.SetPositionAndRotation(position, rotation);
        LensSettings lens = startView.Lens;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = nearClip;
        lens.FarClipPlane = farClip;
        startView.Lens = lens;
    }

    /// <summary>Takes the camera out of the player rig and switches it off until the cutscene needs it.</summary>
    private void Park()
    {
        // Switched off first: the camera starts inside the inactive player rig, and moving it somewhere
        // active while it is still enabled would make it render and add a second audio listener.
        SetCameraActive(false);
        brain.enabled = false;

        if (gameCamera.transform.parent != transform)
        {
            gameCamera.transform.SetParent(transform, false);
        }
        atHome = false;
    }

    /// <summary>Puts the camera back on the player exactly as it was, with Cinemachine out of the way.</summary>
    private void HandOverToPlayer()
    {
        if (atHome)
        {
            return;
        }

        brain.enabled = false; // gameplay looks around through CameraLook, not Cinemachine

        Transform t = gameCamera.transform;
        if (home != null)
        {
            t.SetParent(home, false);
            t.localPosition = homePosition;
            t.localRotation = homeRotation;
            t.localScale = homeScale;
        }

        gameCamera.fieldOfView = homeFieldOfView;
        gameCamera.nearClipPlane = homeNearClip;
        gameCamera.farClipPlane = homeFarClip;
        SetCameraActive(true);
        atHome = true;
    }

    private void SetCameraActive(bool active)
    {
        gameCamera.enabled = active;
        var listener = gameCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = active;
        }
    }
}
