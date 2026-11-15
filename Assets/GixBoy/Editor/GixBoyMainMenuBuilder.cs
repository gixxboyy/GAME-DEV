#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// Menu: GixBoy > Buat Main Menu  — membuat scene MainMenu dan mengatur Build Settings.
public static class GixBoyMainMenuBuilder
{
    const string ScenePath = "Assets/GixBoy/Scenes/MainMenu.unity";
    const string GamePath = "Assets/GixBoy/Scenes/GixBoy_Manado.unity";
    static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
    static readonly Color Panel = new Color(0.04f, 0.05f, 0.08f, 0.82f);

    [MenuItem("GixBoy/Buat Main Menu")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // kamera (hanya untuk latar UI)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        var cgo = new GameObject("MenuCanvas", typeof(RectTransform));
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var root = cgo.GetComponent<RectTransform>();

        // latar belakang
        var bg = Img("Background", root, Color.white); Stretch(bg.rectTransform);
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GixBoy/UI/Menu_Background.png");
        bg.preserveAspect = false;
        var shade = Img("Gelap_Kiri", root, Color.white); Stretch(shade.rectTransform);
        shade.sprite = GradientSprite(); shade.color = new Color(0f, 0f, 0f, 0.85f);
        var bottom = Img("Gelap_Bawah", root, new Color(0f, 0f, 0f, 0.35f)); Stretch(bottom.rectTransform);

        var ctrl = cgo.AddComponent<MainMenuController>();
        ctrl.background = bg.rectTransform;

        // ---------- panel utama ----------
        var main = Rect("PanelUtama", root); Stretch(main);
        Txt("JudulAtas", main, "BENSIN ILEGAL", 120, FontStyles.Bold, Gold, new Vector2(140, -170), new Vector2(1100, 140), TextAlignmentOptions.Left);
        Txt("JudulBawah", main, "MANADO", 96, FontStyles.Bold, Color.white, new Vector2(146, -290), new Vector2(1100, 120), TextAlignmentOptions.Left);
        Txt("Tagline", main, "Kisah GixBoy, pemuda sebatang kara yang menjadi orang suruhan para bos mafia bensin.", 32, FontStyles.Italic, new Color(1f, 1f, 1f, 0.85f), new Vector2(150, -410), new Vector2(820, 100), TextAlignmentOptions.TopLeft);
        var b1 = Btn(main, "MULAI MAIN", new Vector2(150, -530), true);
        var b2 = Btn(main, "CARA BERMAIN", new Vector2(150, -620), false);
        var b5 = Btn(main, "PENGATURAN", new Vector2(150, -710), false);
        var b3 = Btn(main, "KREDIT", new Vector2(150, -800), false);
        var b4 = Btn(main, "KELUAR", new Vector2(150, -890), false);
        Txt("Versi", main, "Final Project  -  Intro to Game Development", 22, FontStyles.Normal, new Color(1f, 1f, 1f, 0.5f), new Vector2(150, -1010), new Vector2(900, 40), TextAlignmentOptions.Left);

        // ---------- panel cara bermain ----------
        var how = Box("PanelCaraBermain", root, "CARA BERMAIN");
        Txt("Isi", how,
            "<b><color=#FFC733>Kontrol</color></b>\n" +
            "WASD / panah  -  jalan          Klik kiri tanah  -  jalan ke titik itu\n" +
            "E  -  bicara dengan NPC          Klik / E / Spasi  -  lanjut dialog\n" +
            "Shift (tahan)  -  lari, menguras stamina          - / =  -  zoom mini map\n" +
            "E dekat pickup  -  naik/turun mobil (W/S gas, A/D belok, Spasi rem)          E di pintu  -  masuk/keluar ruangan\n" +
            "Spasi / Esc  -  lewati cutscene          Esc  -  menu jeda\n\n" +
            "<b><color=#FFC733>Tujuan</color></b>\n" +
            "Temui setiap bos mafia sesuai waktunya (pagi, siang, malam). Ambil jeriken bensin di belakang kios " +
            "Pom Pertaminix yang ditunjuk, lalu serahkan ke bos. Ikuti penanda kuning dan panel misi di kanan atas.\n\n" +
            "<b><color=#FFC733>Hati-hati</color></b>\n" +
            "Bripka Ucup berpatroli. Kalau ia melihat kamu membawa jeriken, ia akan mengejar dan health berkurang. " +
            "Kalau tertangkap, jeriken disita. Bicara dengan Oma Ani di Pasar Bersehati untuk memulihkan health.",
            30, FontStyles.Normal, Color.white, new Vector2(60, -130), new Vector2(1280, 560), TextAlignmentOptions.TopLeft);
        var backHow = Btn(how, "KEMBALI", new Vector2(60, -770), false);

        // ---------- panel kredit ----------
        var cred = Box("PanelKredit", root, "KREDIT");
        Txt("Isi", cred,
            "<b><color=#FFC733>Bensin Ilegal Manado</color></b>\n\n" +
            "Dibuat oleh: <b>Waladow, Geovani</b>\n" +
            "Mata kuliah: Intro to Game Development\n\n" +
            "Model GixBoy & Pom Bensin Pertaminix: dibuat di Blender\n" +
            "Aset kota & karakter: Simple World (SimpleTown, SimplePeople, SimpleCitizens)\n" +
            "Engine: Unity  -  Cinemachine  -  Timeline",
            32, FontStyles.Normal, Color.white, new Vector2(60, -130), new Vector2(1280, 520), TextAlignmentOptions.TopLeft);
        var backCred = Btn(cred, "KEMBALI", new Vector2(60, -770), false);

        // ---------- layar memuat ----------
        var load = Rect("PanelMemuat", root); Stretch(load);
        load.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        var lt = Txt("Teks", load, "Memuat kota Manado...", 46, FontStyles.Bold, Gold, Vector2.zero, new Vector2(1200, 80), TextAlignmentOptions.Center);
        Center(lt.rectTransform, new Vector2(0, 60));
        var barBg = Img("BarLatar", load, new Color(1f, 1f, 1f, 0.15f)); Center(barBg.rectTransform, new Vector2(0, -20)); barBg.rectTransform.sizeDelta = new Vector2(900, 22);
        var fill = Img("BarIsi", barBg.rectTransform, Gold); Stretch(fill.rectTransform);
        fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;

        ctrl.mainPanel = main.gameObject; ctrl.howToPanel = how.gameObject; ctrl.creditsPanel = cred.gameObject;
        ctrl.loadingPanel = load.gameObject; ctrl.loadingFill = fill;
        how.gameObject.SetActive(false); cred.gameObject.SetActive(false); load.gameObject.SetActive(false);

        UnityEventTools.AddPersistentListener(b1.onClick, ctrl.PlayGame);
        UnityEventTools.AddPersistentListener(b2.onClick, ctrl.ShowHowTo);
        UnityEventTools.AddPersistentListener(b3.onClick, ctrl.ShowCredits);
        UnityEventTools.AddPersistentListener(b5.onClick, ctrl.ShowSettings);
        UnityEventTools.AddPersistentListener(b4.onClick, ctrl.QuitGame);
        UnityEventTools.AddPersistentListener(backHow.onClick, ctrl.ShowMain);
        UnityEventTools.AddPersistentListener(backCred.onClick, ctrl.ShowMain);
        es.GetComponent<EventSystem>().firstSelectedGameObject = b1.gameObject;

        EditorSceneManager.SaveScene(scene, ScenePath);

        // Build Settings: MainMenu paling atas, lalu kota, lalu scene lain tetap
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != GamePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(GamePath, true));
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[GixBoy] Main menu dibuat: " + ScenePath);
    }

    // ---------- helper UI ----------
    static RectTransform Rect(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        return go.GetComponent<RectTransform>();
    }

    static Image Img(string n, Transform p, Color c)
    {
        var r = Rect(n, p); var i = r.gameObject.AddComponent<Image>(); i.color = c; return i;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
    static void Center(RectTransform r, Vector2 pos) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; }

    static TextMeshProUGUI Txt(string n, Transform p, string s, float size, FontStyles st, Color c, Vector2 pos, Vector2 dim, TextAlignmentOptions al)
    {
        var r = Rect(n, p);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = dim;
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.fontStyle = st; t.color = c; t.alignment = al; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.outlineWidth = 0.12f; t.outlineColor = new Color32(0, 0, 0, 180);
        return t;
    }

    static Button Btn(Transform p, string label, Vector2 pos, bool primary)
    {
        var r = Rect("Tombol_" + label.Replace(" ", ""), p);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = new Vector2(440, 76);
        var img = r.gameObject.AddComponent<Image>();
        img.color = primary ? Gold : new Color(0.06f, 0.07f, 0.1f, 0.85f);
        var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.8f); o.effectDistance = new Vector2(2, -2);
        var b = r.gameObject.AddComponent<Button>();
        r.gameObject.AddComponent<UIClickSound>();
        var cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.92f, 0.7f); cb.pressedColor = new Color(0.8f, 0.7f, 0.4f); cb.selectedColor = new Color(1f, 0.92f, 0.7f);
        b.colors = cb;
        var t = Txt("Teks", r, label, 36, FontStyles.Bold, primary ? new Color(0.1f, 0.07f, 0.03f) : Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }

    static RectTransform Box(string n, Transform root, string title)
    {
        var r = Rect(n, root);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(110, -90); r.sizeDelta = new Vector2(1400, 880);
        r.gameObject.AddComponent<Image>().color = Panel;
        var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.78f, 0.2f, 0.6f); o.effectDistance = new Vector2(3, -3);
        Txt("Judul", r, title, 64, FontStyles.Bold, Gold, new Vector2(60, -40), new Vector2(1200, 80), TextAlignmentOptions.Left);
        return r;
    }

    static Sprite GradientSprite()
    {
        string path = "Assets/GixBoy/UI/Gradient_Kiri.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var tex = new Texture2D(256, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 256; x++)
        {
            float a = Mathf.Clamp01(1f - (x / 255f) * 1.35f);
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
