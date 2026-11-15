using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Centang jika baris ini diucapkan GixBoy. Kosongkan jika diucapkan NPC.")]
    public bool gixBoySpeaks;
    [TextArea(2, 4)] public string text;
}

// Pasang di setiap NPC. Saat GixBoy mendekat muncul tulisan "Tekan E untuk bicara".
// Tekan E -> cutscene dialog: kamera bergantian menyorot NPC dan GixBoy.
public class DialogueCutscene : MonoBehaviour
{
    [Header("Identitas")]
    public string npcDisplayName = "NPC";

    [Header("Isi dialog (berurutan)")]
    public List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Pengaturan")]
    [Tooltip("Jarak (meter) GixBoy bisa mulai bicara")]
    public float talkRadius = 3.5f;
    [Tooltip("Tinggi kepala karakter dari kaki (untuk bidikan kamera)")]
    public float headHeight = 1.8f;
    [Tooltip("Sembunyikan balon dialog lama (Canvas bawaan NPC)")]
    public bool hideOldBubble = true;

    [Header("Event")]
    public UnityEvent onDialogueFinished;

    Transform player;
    bool inRange;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        if (hideOldBubble)
        {
            var c = transform.Find("Canvas");
            if (c != null) c.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null || lines.Count == 0 || GameHUD.IsPaused) return;
        if (DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying || PickupTruck.PlayerInside) { if (inRange) DialogueManager.Instance.SetPrompt(this, false); inRange = false; return; }

        bool now = Vector3.Distance(player.position, transform.position) <= talkRadius;
        if (now != inRange)
        {
            inRange = now;
            DialogueManager.Instance.SetPrompt(this, now);
        }
        if (inRange && Input.GetKeyDown(KeyCode.E))
            DialogueManager.Instance.Play(this, player);
    }

    void OnDisable()
    {
        if (inRange && DialogueManager.HasInstance) DialogueManager.Instance.SetPrompt(this, false);
        inRange = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
