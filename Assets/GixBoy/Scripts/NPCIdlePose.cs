using UnityEngine;

// Pose diam untuk NPC hiasan (mis. anak buah Opa Hendrik): memutar state Animator tertentu.
public class NPCIdlePose : MonoBehaviour
{
    public string stateName = "Idle_LeaningAgainstWall";
    void Start()
    {
        var a = GetComponentInChildren<Animator>();
        if (a != null && a.HasState(0, Animator.StringToHash(stateName))) a.Play(stateName, 0, Random.value);
    }
}
