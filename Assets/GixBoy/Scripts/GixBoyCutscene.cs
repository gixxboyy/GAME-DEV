using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

// Dipasang di objek Timeline (PlayableDirector). Selama cutscene berjalan:
// player tidak bisa bergerak, ChaseCamera dimatikan (Cinemachine yang memegang kamera),
// dan cutscene bisa dilewati dengan Spasi / Esc.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayableDirector))]
public class GixBoyCutscene : MonoBehaviour
{
    static int playingCount;
    public static bool IsPlaying => playingCount > 0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { playingCount = 0; }

    public Behaviour chaseCamera;
    public Behaviour fadeObstructions;
    public Behaviour playerController;
    public NavMeshAgent playerAgent;
    public bool skippable = true;
    public GameObject skipHint;

    PlayableDirector director;
    bool active;
    bool seenPlaying;
    float beginTime;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        if (director.playOnAwake) Begin();
    }

    void OnDisable() { if (active) End(); }

    void Begin()
    {
        if (active) return;
        active = true; playingCount++; seenPlaying = false; beginTime = Time.unscaledTime;
        if (chaseCamera != null) chaseCamera.enabled = false;
        if (fadeObstructions != null) fadeObstructions.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (playerAgent != null && playerAgent.isOnNavMesh) { playerAgent.ResetPath(); playerAgent.velocity = Vector3.zero; }
        if (skipHint != null) skipHint.SetActive(skippable);
    }

    void End()
    {
        if (!active) return;
        active = false; playingCount = Mathf.Max(0, playingCount - 1);
        if (chaseCamera != null) chaseCamera.enabled = true;
        if (fadeObstructions != null) fadeObstructions.enabled = true;
        if (playerController != null) playerController.enabled = true;
        if (skipHint != null) skipHint.SetActive(false);
    }

    void Update()
    {
        bool playing = director.state == PlayState.Playing
            || (active && director.time > 0.01 && director.time < director.duration - 0.05); // jeda sesaat (mis. editor) tidak dianggap selesai
        if (playing && !active) Begin();
        if (playing) seenPlaying = true;
        if (active)
        {
            if (playerAgent != null && playerAgent.isOnNavMesh && playerAgent.hasPath) playerAgent.ResetPath();
            if (skippable && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
            {
                director.time = director.duration;
                director.Evaluate();
                director.Stop();
                playing = false;
            }
        }
        if (!playing && active && (seenPlaying || Time.unscaledTime - beginTime > 1.5f)) End();
    }
}
