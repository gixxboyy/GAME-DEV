using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// HUD in-game: health, uang, jeriken, waktu, misi aktif + jarak, notifikasi, layar tertangkap & TAMAT.
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public string mainMenuScene = "MainMenu";

    CanvasGroup hudGroup;
    Image healthFill;
    Image staminaFill;
    TextMeshProUGUI staminaLabel;
    TextMeshProUGUI healthText, moneyText, cansText, litersText, timeText, missionTitle, missionObjective, distanceText, toastText;
    CanvasGroup toastGroup;
    GameObject caughtPanel, endPanel, pausePanel;
    public static bool IsPaused { get; private set; }
    TextMeshProUGUI endSummary;
    Coroutine toastCo;
    Transform target;
    GameObject menuButton;
    CanvasGroup toastCG;
    SettingsPanel settings;
    TextMeshProUGUI fpsText;
    float fpsTimer; int fpsFrames; int settingsClosedFrame = -1;

    void Awake()
    {
        Instance = this;
        Build();
    }

    void Start() { AudioManager.PlayMusic("music_city"); }

    public static void Toast(string msg) { Toast(msg, Color.white); }
    public static void Toast(string msg, Color c)
    {
        if (Instance == null) return;
        if (Instance.toastCo != null) Instance.StopCoroutine(Instance.toastCo);
        Instance.toastCo = Instance.StartCoroutine(Instance.ShowToast(msg, c));
    }

    IEnumerator ShowToast(string msg, Color c)
    {
        toastText.text = msg; toastText.color = c;
        float t = 0f;
        while (t < 0.25f) { t += Time.unscaledDeltaTime; toastGroup.alpha = t / 0.25f; yield return null; }
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(3.2f);
        t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - t / 0.5f; yield return null; }
        toastGroup.alpha = 0f;
    }

    public void SetMission(string title, string objective, Transform tgt)
    {
        missionTitle.text = title;
        missionObjective.text = objective;
        target = tgt;
    }

    public void SetTime(TimeOfDay t)
    {
        timeText.text = t == TimeOfDay.Pagi ? "PAGI" : t == TimeOfDay.Siang ? "SIANG" : "MALAM";
        timeText.color = t == TimeOfDay.Pagi ? new Color(1f, 0.8f, 0.45f) : t == TimeOfDay.Siang ? new Color(1f, 0.95f, 0.6f) : new Color(0.6f, 0.75f, 1f);
    }

    public void ShowCaught(bool show) { caughtPanel.SetActive(show); }

    public void ShowEnd(string summary)
    {
        endSummary.text = summary;
        endPanel.SetActive(true);
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
    }

    public void SetPaused(bool p)
    {
        IsPaused = p;
        pausePanel.SetActive(p);
        Time.timeScale = p ? 0f : 1f;
    }

    void OnDestroy() { if (IsPaused) { IsPaused = false; Time.timeScale = 1f; } AudioManager.Siren(false); }

    void OpenSettings()
    {
        pausePanel.SetActive(false);
        if (settings == null) settings = SettingsPanel.Create(pausePanel.transform.parent, () => { settingsClosedFrame = Time.frameCount; if (IsPaused) pausePanel.SetActive(true); });
        settings.gameObject.SetActive(true);
        settings.transform.SetAsLastSibling();
    }

    void Update()
    {
        bool settingsOpen = settings != null && settings.gameObject.activeSelf;
        if (Input.GetKeyDown(KeyCode.Escape) && !settingsOpen && Time.frameCount != settingsClosedFrame && !GixBoyCutscene.IsPlaying && !DialogueManager.IsPlaying && !endPanel.activeSelf)
            SetPaused(!IsPaused);
        bool hide = DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying;
        menuButton.SetActive(!hide && !IsPaused && !endPanel.activeSelf);
        if (toastCG == null) { var tn = GameObject.Find("Notifikasi"); if (tn != null) { toastCG = tn.GetComponent<CanvasGroup>(); if (toastCG == null) toastCG = tn.AddComponent<CanvasGroup>(); } }
        if (toastCG != null) toastCG.alpha = hide ? 0f : 1f;
        fpsText.gameObject.SetActive(GameSettings.ShowFps);
        if (GameSettings.ShowFps)
        {
            fpsFrames++; fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f) { fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS"; fpsFrames = 0; fpsTimer = 0f; }
        }
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, hide ? 0f : 1f, Time.unscaledDeltaTime * 4f);

        var s = GixBoyStats.Instance;
        if (s != null)
        {
            float k = s.maxHealth > 0 ? s.health / s.maxHealth : 0f;
            healthFill.fillAmount = k;
            healthFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), k);
            healthText.text = Mathf.CeilToInt(s.health) + " / " + Mathf.RoundToInt(s.maxHealth);
            moneyText.text = "Rp " + s.money.ToString("N0").Replace(',', '.');
            cansText.text = "Jeriken dibawa: <b>" + s.carried + "</b>  (" + s.CarriedLiters + " L)";
            litersText.text = "Bensin terkumpul: <b>" + s.DeliveredLiters + " L</b>";
        }
        var sp = PlayerSprint.Instance;
        if (sp != null)
        {
            staminaFill.fillAmount = sp.Stamina01;
            staminaFill.color = sp.Exhausted ? new Color(0.9f, 0.35f, 0.25f) : sp.IsRunning ? new Color(1f, 0.85f, 0.3f) : new Color(0.35f, 0.75f, 1f);
            staminaLabel.text = sp.Exhausted ? "CAPEK..." : sp.IsRunning ? "LARI" : "STAMINA  <size=70%>[Shift] lari</size>";
        }

        if (target != null && s != null)
        {
            float d = Vector3.Distance(s.transform.position, InteriorPortal.Resolve(target.position));
            distanceText.text = Mathf.RoundToInt(d) + " m";
        }
        else distanceText.text = "";
    }

    // ---------------- membangun UI ----------------
    void Build()
    {
        var cgo = new GameObject("HUDCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        var hud = Rect("HUD", cgo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
        hudGroup.blocksRaycasts = false;
        hudGroup.alpha = 0f;

        // panel kiri atas: status GixBoy
        var st = Panel("Status", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(130, -30), new Vector2(470, 270));
        Label("Nama", st, new Vector2(22, -14), new Vector2(300, 44), 36, FontStyles.Bold, "GIXBOY", new Color(1f, 0.78f, 0.2f));
        timeText = Label("Waktu", st, new Vector2(300, -16), new Vector2(150, 40), 28, FontStyles.Bold, "PAGI", Color.white);
        timeText.alignment = TextAlignmentOptions.TopRight;
        var hb = Rect("HealthBg", st, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -66), new Vector2(426, 30));
        hb.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var hf = Rect("HealthFill", hb, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        healthFill = hf.gameObject.AddComponent<Image>();
        healthFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal; healthFill.color = new Color(0.3f, 0.85f, 0.35f);
        healthText = Label("HealthText", hb, new Vector2(0, 0), new Vector2(426, 30), 22, FontStyles.Bold, "100 / 100", Color.white);
        healthText.alignment = TextAlignmentOptions.Center;
        var sbg = Rect("StaminaBg", st, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -104), new Vector2(426, 24));
        sbg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var sfl = Rect("StaminaFill", sbg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        staminaFill = sfl.gameObject.AddComponent<Image>();
        staminaFill.sprite = healthFill.sprite;
        staminaFill.type = Image.Type.Filled; staminaFill.fillMethod = Image.FillMethod.Horizontal; staminaFill.color = new Color(0.35f, 0.75f, 1f);
        staminaLabel = Label("StaminaText", sbg, new Vector2(0, 0), new Vector2(426, 24), 17, FontStyles.Bold, "STAMINA", Color.white);
        staminaLabel.alignment = TextAlignmentOptions.Center;
        moneyText = Label("Uang", st, new Vector2(22, -140), new Vector2(426, 40), 32, FontStyles.Bold, "Rp 0", new Color(0.55f, 1f, 0.55f));
        cansText = Label("Jeriken", st, new Vector2(22, -184), new Vector2(426, 36), 26, FontStyles.Normal, "", Color.white);
        litersText = Label("Liter", st, new Vector2(22, -222), new Vector2(426, 36), 26, FontStyles.Normal, "", Color.white);

        // panel kanan atas: misi
        var ms = Panel("Misi", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(620, 170));
        ms.pivot = new Vector2(1, 1);
        missionTitle = Label("JudulMisi", ms, new Vector2(22, -14), new Vector2(576, 44), 30, FontStyles.Bold, "", new Color(1f, 0.78f, 0.2f));
        missionObjective = Label("TujuanMisi", ms, new Vector2(22, -60), new Vector2(576, 80), 26, FontStyles.Normal, "", Color.white);
        distanceText = Label("Jarak", ms, new Vector2(22, -128), new Vector2(576, 32), 24, FontStyles.Italic, "", new Color(1f, 1f, 1f, 0.75f));
        distanceText.alignment = TextAlignmentOptions.TopRight;

        // notifikasi tengah
        var tr = Rect("Notifikasi", cgo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(1300, 80));
        tr.pivot = new Vector2(0.5f, 1f);
        tr.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        toastGroup = tr.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false;
        toastText = Label("Teks", tr, Vector2.zero, new Vector2(1300, 80), 34, FontStyles.Bold, "", Color.white);
        toastText.alignment = TextAlignmentOptions.Center;

        // layar tertangkap
        var cp = Rect("Tertangkap", cgo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        cp.gameObject.AddComponent<Image>().color = new Color(0.35f, 0f, 0f, 0.65f);
        var ct = Label("Teks", cp, Vector2.zero, Vector2.zero, 90, FontStyles.Bold, "TERTANGKAP!", Color.white);
        Stretch(ct.rectTransform); ct.alignment = TextAlignmentOptions.Center;
        caughtPanel = cp.gameObject; caughtPanel.SetActive(false);

        // layar TAMAT
        var ep = Rect("Tamat", cgo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        ep.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
        var et = Label("Judul", ep, Vector2.zero, Vector2.zero, 110, FontStyles.Bold, "TAMAT", new Color(1f, 0.78f, 0.2f));
        et.rectTransform.anchorMin = new Vector2(0, 0.62f); et.rectTransform.anchorMax = new Vector2(1, 0.85f);
        et.rectTransform.offsetMin = Vector2.zero; et.rectTransform.offsetMax = Vector2.zero; et.alignment = TextAlignmentOptions.Center;
        endSummary = Label("Ringkasan", ep, Vector2.zero, Vector2.zero, 34, FontStyles.Normal, "", Color.white);
        endSummary.rectTransform.anchorMin = new Vector2(0.15f, 0.35f); endSummary.rectTransform.anchorMax = new Vector2(0.85f, 0.62f);
        endSummary.rectTransform.offsetMin = Vector2.zero; endSummary.rectTransform.offsetMax = Vector2.zero; endSummary.alignment = TextAlignmentOptions.Center;
        Button("Main Lagi", ep, new Vector2(-170, -330), () => { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        Button("Menu Utama", ep, new Vector2(170, -330), () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(string.IsNullOrEmpty(mainMenuScene) ? "MainMenu" : mainMenuScene);
        });
        endPanel = ep.gameObject; endPanel.SetActive(false);

        // tombol menu (kiri atas)
        var mb = Rect("TombolMenu", cgo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(84, 84));
        mb.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
        var mbo = mb.gameObject.AddComponent<Outline>(); mbo.effectColor = new Color(1f, 0.78f, 0.2f, 0.7f); mbo.effectDistance = new Vector2(2, -2);
        var mbb = mb.gameObject.AddComponent<UnityEngine.UI.Button>(); mbb.onClick.AddListener(() => SetPaused(true));
        mb.gameObject.AddComponent<UIClickSound>();
        for (int gi = 0; gi < 3; gi++)
        {
            var bar = Rect("Garis" + gi, mb, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18 - gi * 18), new Vector2(46, 8));
            bar.pivot = new Vector2(0.5f, 0.5f);
            var bimg = bar.gameObject.AddComponent<Image>(); bimg.color = new Color(1f, 0.78f, 0.2f); bimg.raycastTarget = false;
        }
        menuButton = mb.gameObject;

        // FPS
        fpsText = Label("FPS", cgo.transform, Vector2.zero, new Vector2(220, 36), 24, FontStyles.Bold, "", new Color(0.6f, 1f, 0.6f));
        fpsText.rectTransform.anchorMin = new Vector2(0.5f, 1f); fpsText.rectTransform.anchorMax = new Vector2(0.5f, 1f); fpsText.rectTransform.pivot = new Vector2(0.5f, 1f);
        fpsText.rectTransform.anchoredPosition = new Vector2(0, -8); fpsText.alignment = TextAlignmentOptions.Center;

        // menu jeda
        var pp = Rect("Jeda", cgo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pp.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var box = Rect("Kotak", pp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 660));
        box.pivot = new Vector2(0.5f, 0.5f);
        box.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
        var bo = box.gameObject.AddComponent<Outline>(); bo.effectColor = new Color(1f, 0.78f, 0.2f, 0.7f); bo.effectDistance = new Vector2(3, -3);
        var pt = Label("Judul", box, Vector2.zero, Vector2.zero, 72, FontStyles.Bold, "JEDA", new Color(1f, 0.78f, 0.2f));
        pt.rectTransform.anchorMin = new Vector2(0, 1); pt.rectTransform.anchorMax = new Vector2(1, 1); pt.rectTransform.pivot = new Vector2(0.5f, 1);
        pt.rectTransform.anchoredPosition = new Vector2(0, -30); pt.rectTransform.sizeDelta = new Vector2(0, 100); pt.alignment = TextAlignmentOptions.Center;
        Button("Lanjutkan", box, new Vector2(0, 110), () => SetPaused(false), true);
        Button("Pengaturan", box, new Vector2(0, 10), OpenSettings, false);
        Button("Menu Utama", box, new Vector2(0, -90), () => { SetPaused(false); SceneManager.LoadScene(string.IsNullOrEmpty(mainMenuScene) ? "MainMenu" : mainMenuScene); }, false);
        Button("Keluar Game", box, new Vector2(0, -190), () => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }, false);
        var hint = Label("Hint", box, Vector2.zero, Vector2.zero, 22, FontStyles.Italic, "Tekan Esc untuk kembali ke game", new Color(1f, 1f, 1f, 0.55f));
        hint.rectTransform.anchorMin = new Vector2(0, 0); hint.rectTransform.anchorMax = new Vector2(1, 0); hint.rectTransform.pivot = new Vector2(0.5f, 0);
        hint.rectTransform.anchoredPosition = new Vector2(0, 24); hint.rectTransform.sizeDelta = new Vector2(0, 40); hint.alignment = TextAlignmentOptions.Center;
        pausePanel = pp.gameObject; pausePanel.SetActive(false);
    }

    RectTransform Rect(string n, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = new Vector2(aMin.x, aMax.y);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    RectTransform Panel(string n, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var r = Rect(n, parent, aMin, aMax, pos, size);
        r.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.78f);
        var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.45f); o.effectDistance = new Vector2(2, -2);
        return r;
    }

    TextMeshProUGUI Label(string n, Transform parent, Vector2 pos, Vector2 size, float fs, FontStyles style, string text, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;
        t.fontSize = fs; t.fontStyle = style; t.text = text; t.color = c; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

    void Button(string label, RectTransform parent, Vector2 pos, UnityEngine.Events.UnityAction onClick, bool primary = true)
    {
        var r = Rect("Tombol_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(440, 78));
        r.pivot = new Vector2(0.5f, 0.5f);
        var img = r.gameObject.AddComponent<Image>(); img.color = primary ? new Color(1f, 0.78f, 0.2f) : new Color(0.12f, 0.13f, 0.18f);
        var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.8f); o.effectDistance = new Vector2(2, -2);
        var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.onClick.AddListener(onClick);
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.92f, 0.7f); cb.pressedColor = new Color(0.8f, 0.7f, 0.4f); cb.selectedColor = Color.white; b.colors = cb;
        r.gameObject.AddComponent<UIClickSound>();
        var t = Label("Teks", r, Vector2.zero, Vector2.zero, 32, FontStyles.Bold, label.ToUpper(), primary ? new Color(0.1f, 0.08f, 0.05f) : Color.white);
        Stretch(t.rectTransform); t.alignment = TextAlignmentOptions.Center;
    }
}
