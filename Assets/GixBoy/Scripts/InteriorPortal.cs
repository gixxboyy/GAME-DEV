using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Pintu masuk/keluar ruangan Opa Hendrik (markas Raja Minyak).
// Ruangan berada di lokasi terpisah; pintu memindahkan GixBoy dengan efek layar hitam.
public class InteriorPortal : MonoBehaviour
{
    public static InteriorPortal Instance { get; private set; }
    public static bool InsideRoom { get; private set; }

    public string roomName = "Markas Opa Hendrik";
    public Transform outsideDoor;   // titik pintu di depan gudang (luar)
    public Transform outsideSpawn;  // tempat keluar
    public Transform insideDoor;    // titik pintu di dalam ruangan
    public Transform insideSpawn;   // tempat muncul di dalam
    public Transform roomRoot;
    public Vector3 roomSize = new Vector3(18f, 8f, 14f);
    public float useRadius = 2.4f;

    Transform player;
    NavMeshAgent agent;
    PlayerAvatarController controller;
    CinemachineCamera cmCam;
    Image fade;
    GameObject promptRoot;
    TextMeshProUGUI prompt;
    bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { InsideRoom = false; Instance = null; }

    void Awake() { Instance = this; InsideRoom = false; }
    void OnDestroy() { if (Instance == this) { Instance = null; InsideRoom = false; } }

    void Start()
    {
        var p = GameObject.Find("Player");
        if (p != null) { player = p.transform; agent = p.GetComponent<NavMeshAgent>(); controller = p.GetComponent<PlayerAvatarController>(); }
        var cm = GameObject.Find("CM_Gameplay"); if (cm != null) cmCam = cm.GetComponent<CinemachineCamera>();
        BuildUI();
    }

    public bool InRoomBounds(Vector3 w)
    {
        if (roomRoot == null) return false;
        Vector3 l = roomRoot.InverseTransformPoint(w);
        return Mathf.Abs(l.x) < roomSize.x * 0.5f && Mathf.Abs(l.z) < roomSize.z * 0.5f && l.y > -3f && l.y < roomSize.y;
    }

    // Arah penanda misi: bila target ada di ruang lain, arahkan ke pintu.
    public static Vector3 Resolve(Vector3 target)
    {
        var i = Instance;
        if (i == null) return target;
        bool targetInside = i.InRoomBounds(target);
        if (targetInside && !InsideRoom && i.outsideDoor != null) return i.outsideDoor.position;
        if (!targetInside && InsideRoom && i.insideDoor != null) return i.insideDoor.position;
        return target;
    }

    void Update()
    {
        if (player == null || busy) return;
        bool blocked = GameHUD.IsPaused || DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying || PickupTruck.PlayerInside
                       || controller == null || !controller.enabled;
        Transform door = InsideRoom ? insideDoor : outsideDoor;
        bool near = !blocked && door != null && Vector3.Distance(Flat(player.position), Flat(door.position)) < useRadius;
        ShowPrompt(near ? (InsideRoom ? "Tekan  <b>[E]</b>  untuk keluar" : "Tekan  <b>[E]</b>  untuk masuk ke <b>" + roomName + "</b>") : null);
        if (near && Input.GetKeyDown(KeyCode.E)) StartCoroutine(Travel(!InsideRoom));
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    public void TravelNow(bool goInside) { if (!busy) StartCoroutine(Travel(goInside)); }

    IEnumerator Travel(bool goInside)
    {
        busy = true; ShowPrompt(null);
        if (controller != null) controller.enabled = false;
        AudioManager.Sfx("click");
        yield return Fade(0f, 1f, 0.35f);
        Transform dest = goInside ? insideSpawn : outsideSpawn;
        Vector3 from = player.position;
        if (agent != null && agent.isOnNavMesh) agent.Warp(dest.position); else player.position = dest.position;
        player.rotation = dest.rotation;
        InsideRoom = goInside;
        if (cmCam != null) { cmCam.OnTargetObjectWarped(player, player.position - from); cmCam.PreviousStateIsValid = false; }
        if (goInside)
        {
            var pol = FindAnyObjectByType<PolicePatrol>();
            if (pol != null && pol.IsChasing) pol.StopChase(true);
        }
        if (MissionManager.Instance != null) MissionManager.Instance.RefreshMarker();
        yield return new WaitForSeconds(0.15f);
        if (controller != null) controller.enabled = true;
        GameHUD.Toast(goInside ? roomName : "Kembali ke Malalayang", new Color(1f, 0.85f, 0.4f));
        yield return Fade(1f, 0f, 0.45f);
        busy = false;
    }

    IEnumerator Fade(float a, float b, float t)
    {
        if (fade == null) yield break;
        fade.gameObject.SetActive(true);
        float k = 0f;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / t;
            var c = fade.color; c.a = Mathf.Lerp(a, b, k); fade.color = c;
            yield return null;
        }
        if (b <= 0f) fade.gameObject.SetActive(false);
    }

    void BuildUI()
    {
        var canvas = GameObject.Find("HUDCanvas");
        if (canvas == null) return;
        var f = new GameObject("FadePintu", typeof(RectTransform));
        f.transform.SetParent(canvas.transform, false);
        var frt = f.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        fade = f.AddComponent<Image>(); fade.color = new Color(0f, 0f, 0f, 0f); fade.raycastTarget = false;
        f.SetActive(false);

        promptRoot = new GameObject("PromptPintu", typeof(RectTransform));
        promptRoot.transform.SetParent(canvas.transform, false);
        var rt = promptRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 150f); rt.sizeDelta = new Vector2(900f, 60f);
        promptRoot.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.82f);
        var o = promptRoot.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.6f);
        var tgo = new GameObject("Teks", typeof(RectTransform));
        tgo.transform.SetParent(promptRoot.transform, false);
        var trt = tgo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        prompt = tgo.AddComponent<TextMeshProUGUI>();
        prompt.fontSize = 28; prompt.alignment = TextAlignmentOptions.Center; prompt.color = Color.white; prompt.raycastTarget = false;
        promptRoot.SetActive(false);
    }

    void ShowPrompt(string text)
    {
        if (promptRoot == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (promptRoot.activeSelf != on) promptRoot.SetActive(on);
        if (on && prompt.text != text) prompt.text = text;
    }
}
