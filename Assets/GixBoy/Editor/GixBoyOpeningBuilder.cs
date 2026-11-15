#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Menu: GixBoy > Buat Cutscene Pembuka (Timeline)
// Membuat Timeline: 4 bidikan Cinemachine (dengan gerakan kamera lewat blend),
// teks narasi (Activation Track), dan animasi karakter (Animation Track).
public static class GixBoyOpeningBuilder
{
    const string TimelinePath = "Assets/GixBoy/Timeline/Cutscene_Pembuka.playable";
    const float End = 24f;

    [MenuItem("GixBoy/Buat Cutscene Pembuka (Timeline)")]
    public static void Build()
    {
        var city = GameObject.Find("GixBoy_City");
        var old = city.transform.Find("Cutscene_Pembuka");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("Cutscene_Pembuka");
        root.transform.SetParent(city.transform);
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.None;

        if (!AssetDatabase.IsValidFolder("Assets/GixBoy/Timeline")) AssetDatabase.CreateFolder("Assets/GixBoy", "Timeline");
        if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null) AssetDatabase.DeleteAsset(TimelinePath);
        var tl = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(tl, TimelinePath);
        director.playableAsset = tl;

        // ---------- kamera ----------
        var camGroup = new GameObject("Kamera");
        camGroup.transform.SetParent(root.transform);
        var V = new Dictionary<string, CinemachineCamera>();
        V["1a"] = Cam(camGroup, "Shot1a_LautManado", new Vector3(260f, 70f, 0f), new Vector3(130f, 0f, 110f), 50f);
        V["1b"] = Cam(camGroup, "Shot1b_Boulevard", new Vector3(175f, 16f, 60f), new Vector3(130f, 3f, 120f), 50f);
        V["2a"] = Cam(camGroup, "Shot2a_AntreanPom", new Vector3(125f, 9f, 72f), new Vector3(104f, 2.5f, 48f), 45f);
        V["2b"] = Cam(camGroup, "Shot2b_AntreanPom", new Vector3(88f, 6f, 66f), new Vector3(104f, 2.5f, 48f), 45f);
        V["3a"] = Cam(camGroup, "Shot3a_GudangMalalayang", new Vector3(-80f, 20f, 0f), new Vector3(-118f, 4f, -38f), 45f);
        V["3b"] = Cam(camGroup, "Shot3b_OpaHendrik", new Vector3(-112.3f, 2.9f, -25.5f), new Vector3(-114f, 2.0f, -31f), 40f);
        V["4a"] = Cam(camGroup, "Shot4a_KosGixBoy", new Vector3(-52f, 7f, 124f), new Vector3(-61f, 2.2f, 143f), 45f);
        V["4b"] = Cam(camGroup, "Shot4b_GixBoy", new Vector3(-60.3f, 2.4f, 137.3f), new Vector3(-61f, 2.05f, 141.5f), 35f);
        camGroup.SetActive(false);

        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var cmTrack = tl.CreateTrack<CinemachineTrack>(null, "Kamera Cinemachine");
        director.SetGenericBinding(cmTrack, brain);
        var c1a = Shot(director, cmTrack, V["1a"], 0f, 5.5f);
        var c1b = Shot(director, cmTrack, V["1b"], 0.5f, 6.5f);
        var c2a = Shot(director, cmTrack, V["2a"], 6.5f, 11f);
        var c2b = Shot(director, cmTrack, V["2b"], 7f, 12f);
        var c3a = Shot(director, cmTrack, V["3a"], 12f, 16.5f);
        var c3b = Shot(director, cmTrack, V["3b"], 12.5f, 17.5f);
        var c4a = Shot(director, cmTrack, V["4a"], 17.5f, 21.5f);
        var c4b = Shot(director, cmTrack, V["4b"], 18f, End);
        Blend(c1a, c1b); Blend(c2a, c2b); Blend(c3a, c3b); Blend(c4a, c4b);

        Activation(tl, director, "Kamera aktif", camGroup, 0f, End);

        // ---------- UI teks ----------
        var ui = BuildUI(root.transform, out var bars, out var skip, out var texts);
        Activation(tl, director, "Bar sinematik", bars, 0f, End);
        Activation(tl, director, "Hint lewati", skip, 0f, End);
        Activation(tl, director, "Teks MANADO", texts[0], 0.8f, 4.8f);
        Activation(tl, director, "Teks krisis BBM", texts[1], 2.8f, 6.4f);
        Activation(tl, director, "Teks antrean", texts[2], 7.2f, 11.8f);
        Activation(tl, director, "Teks mafia", texts[3], 12.7f, 17.3f);
        Activation(tl, director, "Teks GIXBOY", texts[4], 18.4f, 23.8f);
        Activation(tl, director, "Teks sebatang kara", texts[5], 19.2f, 23.8f);

        // ---------- animasi karakter ----------
        string animFbx = "Assets/SimpleWorldVol1/SimpleCitizens/Models/Animations.fbx";
        var player = GameObject.FindGameObjectWithTag("Player");
        var opa = GameObject.Find("GixBoy_City/NPC/NPC_OpaHendrik");
        if (player != null)
        {
            var spawn = GameObject.Find("GixBoy_City/Sistem/SpawnPoint_Kos");
            if (spawn != null) { player.transform.position = spawn.transform.position; player.transform.rotation = spawn.transform.rotation; }
            var t = Anim(tl, director, "Animasi GixBoy", player.GetComponent<Animator>());
            AddAnim(t, animFbx, "Idle_CrossedArms", 17.5f, 20.5f);
            AddAnim(t, animFbx, "Idle_HandsOnHips", 20.5f, End);
        }
        if (opa != null)
        {
            var t = Anim(tl, director, "Animasi Opa Hendrik", opa.GetComponent<Animator>());
            AddAnim(t, animFbx, "Idle_Smoking", 12f, 17.5f);
        }

        // ---------- kontrol gameplay ----------
        var gc = root.AddComponent<GixBoyCutscene>();
        var cam = Camera.main;
        gc.chaseCamera = null; // kamera gameplay sekarang Cinemachine (CM_Gameplay)
        gc.fadeObstructions = cam.GetComponent("FadeCameraObstructions") as Behaviour;
        if (player != null)
        {
            gc.playerController = player.GetComponent("PlayerAvatarController") as Behaviour;
            gc.playerAgent = player.GetComponent<NavMeshAgent>();
        }
        gc.skipHint = skip;

        EditorUtility.SetDirty(tl);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[GixBoy] Cutscene pembuka dibuat: " + TimelinePath);
    }

    static CinemachineCamera Cam(GameObject parent, string n, Vector3 pos, Vector3 look, float fov)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent.transform);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        var c = go.AddComponent<CinemachineCamera>();
        c.Priority = 0;
        var l = c.Lens; l.FieldOfView = fov; l.NearClipPlane = 0.1f; l.FarClipPlane = 1500f; c.Lens = l;
        return c;
    }

    static TimelineClip Shot(PlayableDirector d, CinemachineTrack t, CinemachineCamera cam, float start, float end)
    {
        var clip = t.CreateClip<CinemachineShot>();
        clip.start = start; clip.duration = end - start;
        clip.displayName = cam.name;
        var shot = (CinemachineShot)clip.asset;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        d.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
        return clip;
    }

    static void Blend(TimelineClip a, TimelineClip b)
    {
        double overlap = a.end - b.start;
        if (overlap <= 0) return;
        b.blendInDuration = overlap;
        a.blendOutDuration = overlap;
    }

    static void Activation(TimelineAsset tl, PlayableDirector d, string name, GameObject go, float start, float end)
    {
        var t = tl.CreateTrack<ActivationTrack>(null, name);
        t.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
        var c = t.CreateDefaultClip();
        c.start = start; c.duration = end - start; c.displayName = name;
        d.SetGenericBinding(t, go);
        go.SetActive(false);
    }

    static AnimationTrack Anim(TimelineAsset tl, PlayableDirector d, string name, Animator a)
    {
        var t = tl.CreateTrack<AnimationTrack>(null, name);
        t.trackOffset = TrackOffset.ApplySceneOffsets;
        d.SetGenericBinding(t, a);
        return t;
    }

    static void AddAnim(AnimationTrack t, string fbx, string clipName, float start, float end)
    {
        AnimationClip src = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            if (o is AnimationClip ac && ac.name == clipName) { src = ac; break; }
        if (src == null) { Debug.LogWarning("Klip tidak ada: " + clipName); return; }
        var c = t.CreateClip(src);
        c.start = start; c.duration = end - start; c.displayName = clipName;
        var asset = (AnimationPlayableAsset)c.asset;
        asset.loop = AnimationPlayableAsset.LoopMode.On;
        asset.removeStartOffset = true;
    }

    // ---------- UI ----------
    static GameObject BuildUI(Transform root, out GameObject bars, out GameObject skip, out GameObject[] texts)
    {
        var cgo = new GameObject("CutsceneUI", typeof(RectTransform));
        cgo.transform.SetParent(root);
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
        var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;

        bars = new GameObject("BarSinematik", typeof(RectTransform));
        bars.transform.SetParent(cgo.transform, false); Stretch(bars.GetComponent<RectTransform>());
        Bar(bars.transform, true); Bar(bars.transform, false);

        skip = Text(cgo.transform, "HintLewati", "Spasi / Esc : lewati cutscene", 26, FontStyles.Italic, new Color(1f, 1f, 1f, 0.6f),
            new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(700f, 50f), TextAlignmentOptions.BottomRight).gameObject;

        texts = new GameObject[6];
        texts[0] = Text(cgo.transform, "T_Manado", "MANADO", 170, FontStyles.Bold, new Color(1f, 0.8f, 0.25f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1600f, 240f), TextAlignmentOptions.Center).gameObject;
        texts[1] = Sub(cgo.transform, "T_Krisis", "Kota pesisir yang sedang dilanda krisis BBM.");
        texts[2] = Sub(cgo.transform, "T_Antrean", "Berminggu-minggu antrean mengular di setiap pom bensin.\n\"Bensin so maso?\"");
        texts[3] = Sub(cgo.transform, "T_Mafia", "Di balik kelangkaan itu, para bos mafia bensin menguasai kota.");
        texts[4] = Text(cgo.transform, "T_GixBoy", "GIXBOY", 150, FontStyles.Bold, new Color(1f, 0.8f, 0.25f),
            new Vector2(0f, 0.5f), new Vector2(120f, 140f), new Vector2(1000f, 220f), TextAlignmentOptions.Left).gameObject;
        texts[5] = Text(cgo.transform, "T_SebatangKara", "Sebatang kara.\nOrang suruhan para bos mafia bensin.", 46, FontStyles.Normal, Color.white,
            new Vector2(0f, 0.5f), new Vector2(124f, -10f), new Vector2(1000f, 160f), TextAlignmentOptions.TopLeft).gameObject;
        return cgo;
    }

    static GameObject Sub(Transform p, string n, string s)
    {
        return Text(p, n, s, 46, FontStyles.Normal, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1600f, 140f), TextAlignmentOptions.Bottom).gameObject;
    }

    static void Bar(Transform p, bool top)
    {
        var go = new GameObject(top ? "BarAtas" : "BarBawah", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        go.GetComponent<Image>().color = Color.black;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot = new Vector2(0.5f, top ? 1f : 0f); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0f, 130f);
    }

    static TextMeshProUGUI Text(Transform p, string n, string s, float size, FontStyles st, Color c, Vector2 anchor, Vector2 pos, Vector2 dim, TextAlignmentOptions al)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        var r = t.rectTransform; r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = dim;
        t.text = s; t.fontSize = size; t.fontStyle = st; t.color = c; t.alignment = al; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.outlineWidth = 0.15f; t.outlineColor = new Color32(0, 0, 0, 200);
        return t;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
}
#endif
