using UnityEngine;
using UnityEngine.UI;

// Pasang di Button supaya berbunyi "klik" saat ditekan.
[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    void Start() { GetComponent<Button>().onClick.AddListener(() => AudioManager.Sfx("click")); }
}
