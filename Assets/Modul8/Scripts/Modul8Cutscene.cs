using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

// Modul 8: selama Timeline cutscene berjalan, Player tidak bisa bergerak, ChaseCamera
// dimatikan supaya Cinemachine Brain yang menggerakkan Main Camera, dan
// FadeCameraObstructions dimatikan supaya gedung tidak jadi transparan di cutscene.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayableDirector))]
public class Modul8Cutscene : MonoBehaviour
{
    public ChaseCamera chaseCamera;
    public NavMeshAgent playerAgent;
    public Behaviour fadeObstructions;

    PlayableDirector director;
    bool wasPlaying;

    public bool IsPlaying { get { return director != null && director.state == PlayState.Playing; } }

    void Awake() { director = GetComponent<PlayableDirector>(); }

    void Update()
    {
        bool playing = IsPlaying;
        if (playing)
        {
            InputManager.bCanPlayerMove = false;
            if (chaseCamera != null) chaseCamera.enabled = false;
            if (fadeObstructions != null) fadeObstructions.enabled = false;
            if (!wasPlaying && playerAgent != null && playerAgent.isOnNavMesh) playerAgent.ResetPath();
        }
        else if (wasPlaying)
        {
            InputManager.bCanPlayerMove = true;
            if (chaseCamera != null) chaseCamera.enabled = true;
            if (fadeObstructions != null) fadeObstructions.enabled = true;
        }
        wasPlaying = playing;
    }
}
