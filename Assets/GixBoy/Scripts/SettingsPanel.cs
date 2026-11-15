using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel PENGATURAN (dibuat lewat kode, dipakai di Main Menu dan Menu Jeda).
// Volume Utama, Musik, Efek Suara, Kualitas Grafik, Layar Penuh, Tampilkan FPS.
public class SettingsPanel : MonoBehaviour
{
    public System.Action onBack;

    static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
    static Sprite white;

    TextMeshProUGUI qualityText, fullText, fpsText;

    public static SettingsPanel Create(Transform canvasRoot, System.Action onBack)
    {
        var go = new GameObject("PanelPengaturan", typeof(RectTransform));
        go.transform.SetParent(canvasRoot, false);
        var sp = go.AddComponent<SettingsPanel>();
        sp.onBack = onBack;
        sp.Build();
        return sp;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    public void Back()
    {
        gameObject.SetActive(false);
        if (onBack != null) onBack();
    }

    void Build()
    {
        if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        var root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        var dim = gameObject.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.55f);

        var box = R("Kotak", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 860));
        box.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
        var o = box.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.7f); o.effectDistance = new Vector2(3, -3);

        T("Judul", box, "PENGATURAN", 60, FontStyles.Bold, Gold, new Vector2(0, 355), new Vector2(900, 80), TextAlignmentOptions.Center);

        float y = 235;
        SliderRow(box, "Volume Utama", y, GameSettings.MasterVolume, GameSettings.SetMaster); y -= 90;
        SliderRow(box, "Musik", y, GameSettings.MusicVolume, GameSettings.SetMusic); y -= 90;
        SliderRow(box, "Efek Suara", y, GameSettings.SfxVolume, v => { GameSettings.SetSfx(v); }); y -= 100;

        qualityText = OptionRow(box, "Kualitas Grafik", y, GameSettings.QualityName,
            () => { GameSettings.SetQuality(GameSettings.Quality - 1); Refresh(); },
            () => { GameSettings.SetQuality(GameSettings.Quality + 1); Refresh(); }); y -= 90;
        fullText = OptionRow(box, "Layar Penuh", y, "",
            () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); Refresh(); },
            () => { GameSettings.SetFullscreen(!GameSettings.Fullscreen); Refresh(); }); y -= 90;
        fpsText = OptionRow(box, "Tampilkan FPS", y, "",
            () => { GameSettings.SetShowFps(!GameSettings.ShowFps); Refresh(); },
            () => { GameSettings.SetShowFps(!GameSettings.ShowFps); Refresh(); });

        var back = Btn(box, "KEMBALI", new Vector2(0, -360), new Vector2(360, 72), true);
        back.onClick.AddListener(Back);
        Refresh();
    }

    void Refresh()
    {
        qualityText.text = GameSettings.QualityName;
        fullText.text = GameSettings.Fullscreen ? "AKTIF" : "MATI";
        fpsText.text = GameSettings.ShowFps ? "AKTIF" : "MATI";
    }

    // ---------------- komponen ----------------
    void SliderRow(RectTransform box, string label, float y, float value, System.Action<float> onChange)
    {
        T("Label_" + label, box, label, 34, FontStyles.Normal, Color.white, new Vector2(-250, y), new Vector2(380, 50), TextAlignmentOptions.Left);
        var valueText = T("Nilai_" + label, box, Mathf.RoundToInt(value * 100) + "%", 30, FontStyles.Bold, Gold, new Vector2(400, y), new Vector2(110, 50), TextAlignmentOptions.Right);

        var sr = R("Slider_" + label, box, new Vector2(0.5f, 0.5f), new Vector2(150, y), new Vector2(380, 30));
        var bg = R("Latar", sr, Vector2.zero, Vector2.zero, Vector2.zero); Stretch(bg);
        var bgi = bg.gameObject.AddComponent<Image>(); bgi.sprite = white; bgi.color = new Color(1f, 1f, 1f, 0.15f);
        var fa = R("AreaIsi", sr, Vector2.zero, Vector2.zero, Vector2.zero); Stretch(fa); fa.offsetMin = new Vector2(4, 4); fa.offsetMax = new Vector2(-4, -4);
        var fill = R("Isi", fa, Vector2.zero, Vector2.zero, Vector2.zero); Stretch(fill);
        var fi = fill.gameObject.AddComponent<Image>(); fi.sprite = white; fi.color = Gold;
        var ha = R("AreaPegangan", sr, Vector2.zero, Vector2.zero, Vector2.zero); Stretch(ha); ha.offsetMin = new Vector2(10, 0); ha.offsetMax = new Vector2(-10, 0);
        var h = R("Pegangan", ha, Vector2.zero, Vector2.zero, new Vector2(26, 0)); h.anchorMin = new Vector2(0, 0); h.anchorMax = new Vector2(0, 1); h.offsetMin = new Vector2(-13, -8); h.offsetMax = new Vector2(13, 8);
        var hi = h.gameObject.AddComponent<Image>(); hi.sprite = white; hi.color = Color.white;

        var s = sr.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = h; s.targetGraphic = hi;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f;
        s.value = value;
        s.onValueChanged.AddListener(v => { onChange(v); valueText.text = Mathf.RoundToInt(v * 100) + "%"; });
        if (label == "Efek Suara") s.onValueChanged.AddListener(v => { if (Time.unscaledTime > 0.5f) AudioManager.Sfx("blip"); });
    }

    TextMeshProUGUI OptionRow(RectTransform box, string label, float y, string value, System.Action prev, System.Action next)
    {
        T("Label_" + label, box, label, 34, FontStyles.Normal, Color.white, new Vector2(-250, y), new Vector2(380, 50), TextAlignmentOptions.Left);
        var l = Btn(box, "<", new Vector2(10, y), new Vector2(60, 56), false); l.onClick.AddListener(() => prev());
        var r = Btn(box, ">", new Vector2(370, y), new Vector2(60, 56), false); r.onClick.AddListener(() => next());
        return T("Nilai_" + label, box, value, 32, FontStyles.Bold, Gold, new Vector2(190, y), new Vector2(280, 50), TextAlignmentOptions.Center);
    }

    static RectTransform R(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

    static TextMeshProUGUI T(string n, Transform p, string s, float size, FontStyles st, Color c, Vector2 pos, Vector2 dim, TextAlignmentOptions al)
    {
        var r = R(n, p, new Vector2(0.5f, 0.5f), pos, dim);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.fontStyle = st; t.color = c; t.alignment = al; t.raycastTarget = false;
        return t;
    }

    public static Button Btn(Transform p, string label, Vector2 pos, Vector2 size, bool primary)
    {
        if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        var r = R("Tombol_" + label, p, new Vector2(0.5f, 0.5f), pos, size);
        var img = r.gameObject.AddComponent<Image>(); img.sprite = white;
        img.color = primary ? Gold : new Color(0.1f, 0.11f, 0.15f, 1f);
        var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.8f); o.effectDistance = new Vector2(2, -2);
        var b = r.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.highlightedColor = new Color(1f, 0.92f, 0.7f); cb.pressedColor = new Color(0.8f, 0.7f, 0.4f); cb.selectedColor = Color.white; b.colors = cb;
        r.gameObject.AddComponent<UIClickSound>();
        var t = T("Teks", r, label, size.y > 60 ? 34 : 32, FontStyles.Bold, primary ? new Color(0.1f, 0.07f, 0.03f) : Color.white, Vector2.zero, size, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }
}
