using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Jeriken bensin di pom. GixBoy harus berdiri di dekatnya dan TAHAN E untuk mengisi.
// Waktu mengisi makin lama di misi yang lebih sulit (diatur MissionManager lewat FillTime).
public class JerrycanPickup : MonoBehaviour
{
    public int missionIndex;
    public static float FillTime = 1f;          // detik untuk mengisi satu jeriken
    public static bool PlayerFilling { get; private set; }
    static readonly List<JerrycanPickup> All = new List<JerrycanPickup>();
    static JerrycanPickup focus;
    static GameObject uiRoot; static Image uiFill; static TextMeshProUGUI uiText;
    static Transform player; static PlayerAvatarController controller;

    const float UseRadius = 1.8f;
    float progress;
    Transform visual;
    Light glow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); focus = null; uiRoot = null; player = null; PlayerFilling = false; FillTime = 1f; }

    public static JerrycanPickup Spawn(Vector3 pos, int missionIndex, Transform parent)
    {
        var go = new GameObject("Jeriken_Ambil");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var p = go.AddComponent<JerrycanPickup>();
        p.missionIndex = missionIndex;
        return p;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); if (focus == this) { focus = null; PlayerFilling = false; ShowUI(false); } }

    void Start()
    {
        visual = JerrycanVisual.Create(transform, 0.6f).transform;
        visual.localPosition = Vector3.zero;
        // cahaya kecil hangat supaya terlihat (lebih terang saat malam)
        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        glow.type = LightType.Point; glow.range = 3.5f; glow.color = new Color(1f, 0.6f, 0.2f);
        glow.shadows = LightShadows.None;
        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) { player = p.transform; controller = p.GetComponent<PlayerAvatarController>(); }
        }
    }

    void Update()
    {
        if (glow != null) glow.intensity = (DayNightController.Instance != null && DayNightController.Instance.current == TimeOfDay.Malam ? 2.2f : 0.8f) + Mathf.Sin(Time.time * 3f) * 0.25f;
        if (player == null) return;

        // hanya jeriken terdekat yang aktif
        if (focus == null || focus == this || !focus.isActiveAndEnabled)
        {
            JerrycanPickup best = null; float bd = UseRadius;
            foreach (var j in All)
            {
                float d = Flat(j.transform.position - player.position).magnitude;
                if (d < bd) { bd = d; best = j; }
            }
            if (focus != best) { if (focus != null) focus.progress = 0f; focus = best; if (best == null) { PlayerFilling = false; ShowUI(false); } }
        }
        if (focus != this) return;

        bool canUse = controller != null && controller.enabled && !PickupTruck.PlayerInside && !GameHUD.IsPaused
                      && !DialogueManager.IsPlaying && !GixBoyCutscene.IsPlaying
                      && Flat(transform.position - player.position).magnitude < UseRadius;
        if (!canUse) { PlayerFilling = false; progress = 0f; ShowUI(false); return; }

        bool hold = Input.GetKey(KeyCode.E);
        PlayerFilling = hold;
        if (hold) progress += Time.deltaTime / Mathf.Max(0.2f, FillTime);
        else progress = Mathf.Max(0f, progress - Time.deltaTime * 1.5f);
        ShowUI(true);
        uiFill.fillAmount = progress;
        uiText.text = hold ? "Mengisi jeriken... <b>" + Mathf.RoundToInt(progress * 100f) + "%</b>" : "Tahan  <b>[E]</b>  untuk mengisi jeriken";
        if (hold && visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 30f) * 2f);

        if (progress >= 1f)
        {
            PlayerFilling = false; ShowUI(false);
            AudioManager.Sfx("pickup");
            if (MissionManager.Instance != null) MissionManager.Instance.OnPickup(this);
            Destroy(gameObject);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static void ShowUI(bool on)
    {
        if (uiRoot == null) { if (!on) return; BuildUI(); if (uiRoot == null) return; }
        if (uiRoot.activeSelf != on) uiRoot.SetActive(on);
    }

    static void BuildUI()
    {
        var canvas = GameObject.Find("HUDCanvas");
        if (canvas == null) return;
        uiRoot = new GameObject("PromptIsiJeriken", typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        var rt = uiRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 150f); rt.sizeDelta = new Vector2(620f, 86f);
        uiRoot.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.85f);
        var o = uiRoot.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.6f);
        var tgo = new GameObject("Teks", typeof(RectTransform)); tgo.transform.SetParent(uiRoot.transform, false);
        var trt = tgo.GetComponent<RectTransform>(); trt.anchorMin = new Vector2(0f, 0.42f); trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        uiText = tgo.AddComponent<TextMeshProUGUI>(); uiText.fontSize = 26; uiText.alignment = TextAlignmentOptions.Center; uiText.color = Color.white; uiText.raycastTarget = false;
        var bg = new GameObject("BarBg", typeof(RectTransform)); bg.transform.SetParent(uiRoot.transform, false);
        var brt = bg.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0.06f, 0.14f); brt.anchorMax = new Vector2(0.94f, 0.36f); brt.offsetMin = brt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var fg = new GameObject("Bar", typeof(RectTransform)); fg.transform.SetParent(bg.transform, false);
        var frt = fg.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        uiFill = fg.AddComponent<Image>();
        uiFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        uiFill.type = Image.Type.Filled; uiFill.fillMethod = Image.FillMethod.Horizontal; uiFill.color = new Color(1f, 0.6f, 0.15f);
        uiRoot.SetActive(false);
    }
}
