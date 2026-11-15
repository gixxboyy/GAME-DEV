using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mini map bulat di kiri bawah: peta kota (gambar top-down), panah GixBoy,
// ikon pom bensin, dan tanda misi (menempel di tepi peta kalau di luar jangkauan).
public class MinimapUI : MonoBehaviour
{
    public Texture2D mapTexture;
    [Tooltip("Koordinat dunia (x,z) sudut kiri-bawah gambar peta")]
    public Vector2 worldMin = new Vector2(-160f, -105f);
    [Tooltip("Lebar/tinggi area dunia yang dicakup gambar (meter)")]
    public float worldSize = 400f;
    [Tooltip("Radius area yang terlihat di mini map (meter)")]
    public float viewRadius = 65f;
    public float minRadius = 30f, maxRadius = 120f;
    public float uiSize = 330f;

    static readonly Color Gold = new Color(1f, 0.78f, 0.2f);

    Transform player;
    RectTransform root, mapArea, arrow, targetIcon, targetArrow;
    RawImage map;
    TextMeshProUGUI distText, zoomText;
    Transform[] poms;
    RectTransform[] pomIcons;
    static Sprite circle, tri, diamond;

    void Start()
    {
        var p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        var canvasGo = GameObject.Find("HUDCanvas");
        if (canvasGo == null) { enabled = false; return; }
        Build(canvasGo.transform);
    }

    void Build(Transform canvas)
    {
        if (circle == null) circle = MakeCircle(128);
        if (tri == null) tri = MakeTriangle(64);
        if (diamond == null) diamond = MakeDiamond(64);

        root = R("MiniMap", canvas, new Vector2(uiSize + 16, uiSize + 70));
        root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(24, 24);
        root.SetAsFirstSibling();

        var ring = Img("Bingkai", root, circle, Gold, new Vector2(uiSize + 12, uiSize + 12));
        ring.rectTransform.anchoredPosition = new Vector2(0, 30);
        var mask = Img("Masker", root, circle, Color.white, new Vector2(uiSize, uiSize));
        mask.rectTransform.anchoredPosition = new Vector2(0, 30);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        mapArea = mask.rectTransform;

        var bg = Img("Laut", mapArea, null, new Color(0.09f, 0.25f, 0.38f), new Vector2(uiSize, uiSize));
        bg.rectTransform.anchoredPosition = Vector2.zero;
        var mgo = new GameObject("Peta", typeof(RectTransform));
        mgo.transform.SetParent(mapArea, false);
        map = mgo.AddComponent<RawImage>(); map.texture = mapTexture; map.raycastTarget = false;
        map.rectTransform.sizeDelta = new Vector2(uiSize, uiSize);

        // ikon pom bensin
        var pomRoot = GameObject.Find("PomBensin");
        if (pomRoot != null)
        {
            poms = new Transform[pomRoot.transform.childCount];
            pomIcons = new RectTransform[poms.Length];
            for (int i = 0; i < poms.Length; i++)
            {
                poms[i] = pomRoot.transform.GetChild(i);
                var ic = Img("Pom" + i, mapArea, circle, new Color(0.15f, 0.55f, 1f), new Vector2(22, 22));
                var t = Txt("P", ic.rectTransform, "P", 16, Color.white); t.rectTransform.sizeDelta = new Vector2(22, 22);
                pomIcons[i] = ic.rectTransform;
            }
        }

        // panah GixBoy
        arrow = Img("Panah", mapArea, tri, Color.white, new Vector2(28, 32)).rectTransform;
        var arrowFill = Img("Isi", arrow, tri, new Color(0.3f, 1f, 0.45f), new Vector2(20, 23));
        arrowFill.rectTransform.anchoredPosition = new Vector2(0, -1);

        // tanda misi (di luar masker agar bisa menempel di tepi)
        var tIcon = Img("TandaMisi", root, diamond, Gold, new Vector2(40, 40));
        var tInner = Img("Isi", tIcon.rectTransform, diamond, new Color(0.15f, 0.1f, 0.02f), new Vector2(28, 28));
        var ex = Txt("Seru", tIcon.rectTransform, "!", 26, Gold); ex.rectTransform.sizeDelta = new Vector2(40, 40);
        targetIcon = tIcon.rectTransform;
        targetArrow = Img("ArahMisi", targetIcon, tri, Gold, new Vector2(18, 14)).rectTransform;

        // utara
        var n = Img("Utara", root, circle, new Color(0.05f, 0.06f, 0.09f), new Vector2(34, 34));
        n.rectTransform.anchoredPosition = new Vector2(0, 30 + uiSize * 0.5f);
        Txt("U", n.rectTransform, "U", 20, Gold).rectTransform.sizeDelta = new Vector2(34, 34);

        // teks jarak & zoom
        distText = Txt("Jarak", root, "", 24, Color.white);
        distText.rectTransform.sizeDelta = new Vector2(uiSize + 40, 34);
        distText.rectTransform.anchoredPosition = new Vector2(0, -uiSize * 0.5f - 12);
        zoomText = Txt("Zoom", root, "[ - ]  /  [ = ]  zoom", 16, new Color(1, 1, 1, 0.5f));
        zoomText.rectTransform.sizeDelta = new Vector2(uiSize, 24);
        zoomText.rectTransform.anchoredPosition = new Vector2(0, -uiSize * 0.5f - 38);
    }

    void Update()
    {
        if (root == null || player == null) return;
        bool hide = GameHUD.IsPaused || DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying || InteriorPortal.InsideRoom
                    || (MissionManager.Instance != null && MissionManager.Instance.Finished);
        if (root.gameObject.activeSelf == hide) root.gameObject.SetActive(!hide);
        if (hide) return;

        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) viewRadius += 40f * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) viewRadius -= 40f * Time.unscaledDeltaTime;
        viewRadius = Mathf.Clamp(viewRadius, minRadius, maxRadius);

        Vector3 pp = player.position;
        float scale = uiSize / (viewRadius * 2f); // piksel UI per meter
        // gambar peta dipindah agar posisi pemain di tengah
        float mapPx = worldSize * scale;
        map.rectTransform.sizeDelta = new Vector2(mapPx, mapPx);
        Vector2 uv = new Vector2((pp.x - worldMin.x) / worldSize, (pp.z - worldMin.y) / worldSize);
        map.rectTransform.anchoredPosition = new Vector2((0.5f - uv.x) * mapPx, (0.5f - uv.y) * mapPx);

        arrow.anchoredPosition = Vector2.zero;
        arrow.localEulerAngles = new Vector3(0, 0, -player.eulerAngles.y);

        if (poms != null)
            for (int i = 0; i < poms.Length; i++)
            {
                Vector2 d = new Vector2(poms[i].position.x - pp.x, poms[i].position.z - pp.z) * scale;
                pomIcons[i].anchoredPosition = d;
            }

        // tanda misi
        var mm = MissionManager.Instance;
        bool has = mm != null && mm.marker != null && mm.marker.gameObject.activeInHierarchy;
        targetIcon.gameObject.SetActive(has);
        if (has)
        {
            Vector3 tp = mm.marker.transform.position;
            Vector2 d = new Vector2(tp.x - pp.x, tp.z - pp.z);
            float meters = d.magnitude;
            Vector2 ui = d * scale;
            float edge = uiSize * 0.5f - 6f;
            bool outside = ui.magnitude > edge;
            if (outside) ui = ui.normalized * edge;
            targetIcon.anchoredPosition = new Vector2(0, 30) + ui;
            targetIcon.localScale = Vector3.one * (outside ? 0.85f : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.08f);
            targetArrow.gameObject.SetActive(outside);
            if (outside)
            {
                Vector2 dir = d.normalized;
                targetArrow.anchoredPosition = dir * 30f;
                targetArrow.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }
            distText.text = "<color=#FFC733>◆</color> Tujuan misi: <b>" + Mathf.RoundToInt(meters) + " m</b>";
        }
        else distText.text = "";
    }

    // ---------- helper ----------
    static RectTransform R(string n, Transform parent, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.sizeDelta = size;
        return r;
    }

    static Image Img(string n, Transform parent, Sprite s, Color c, Vector2 size)
    {
        var r = R(n, parent, size);
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = s; i.color = c; i.raycastTarget = false;
        return i;
    }

    static TextMeshProUGUI Txt(string n, Transform parent, string text, float size, Color c)
    {
        var r = R(n, parent, new Vector2(100, 30));
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Sprite MakeCircle(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float r = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
        tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeTriangle(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float fy = (y + 0.5f) / s; // 0 bawah, 1 atas (ujung)
                float half = (1f - fy) * 0.5f;
                float fx = Mathf.Abs((x + 0.5f) / s - 0.5f);
                float notch = fy < 0.25f ? (0.25f - fy) * 1.2f : 0f; // lekukan ekor
                float a = Mathf.Clamp01((half - fx) * s) * (fx < notch ? 0f : 1f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeDiamond(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float h = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Abs(x + 0.5f - h) + Mathf.Abs(y + 0.5f - h);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(h - d)));
            }
        tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }
}
