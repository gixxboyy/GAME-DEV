using UnityEngine;
using UnityEngine.Playables;

// Challenge Modul 8: Timeline perkenalan karakter baru hanya diputar
// saat Player masuk ke area ini (saat karakter diperkenalkan ke dalam game).
[RequireComponent(typeof(Collider))]
public class Modul8CharacterTrigger : MonoBehaviour
{
    public PlayableDirector characterIntro;
    public Modul8Cutscene openingCutscene;

    bool played;

    void OnTriggerEnter(Collider other) { TryPlay(other); }
    void OnTriggerStay(Collider other) { TryPlay(other); }

    void TryPlay(Collider other)
    {
        if (played || characterIntro == null || other.GetComponentInParent<PlayerAvatarController>() == null) return;
        if (openingCutscene != null && openingCutscene.IsPlaying) return;
        played = true;
        characterIntro.Play();
    }
}
