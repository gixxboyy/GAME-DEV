using Unity.Cinemachine;
using UnityEngine;

// Kamera gameplay (Cinemachine): mengikuti GixBoy dari belakang-atas dengan sudut tetap.
// Scroll mouse untuk zoom in/out.
[RequireComponent(typeof(CinemachineCamera))]
public class GameplayCameraZoom : MonoBehaviour
{
    public float minDistance = 8f;
    public float maxDistance = 30f;
    public float distance = 17f;
    [Range(15f, 75f)] public float pitch = 42f;
    public float zoomSpeed = 4f;

    CinemachineFollow follow;

    void Awake() { follow = GetComponent<CinemachineFollow>(); Apply(); }

    void Update()
    {
        bool locked = DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying || GameHUD.IsPaused;
        if (!locked)
        {
            float s = Input.mouseScrollDelta.y;
            if (Mathf.Abs(s) > 0.01f) distance = Mathf.Clamp(distance - s * zoomSpeed * 0.5f, minDistance, maxDistance);
        }
        Apply();
    }

    void OnValidate() { if (follow == null) follow = GetComponent<CinemachineFollow>(); Apply(); }

    void Apply()
    {
        if (follow == null) return;
        // sudut dunia tetap (menghadap +Z) supaya tombol W selalu = maju di layar
        follow.FollowOffset = Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -distance);
    }
}
