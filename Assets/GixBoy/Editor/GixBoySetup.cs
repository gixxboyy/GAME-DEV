#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menu: GixBoy > Setup Misi & HUD  — memasang sistem misi, HUD, polisi, dan siang/malam ke scene.
public static class GixBoySetup
{
    static DialogueLine N(string t) { return new DialogueLine { gixBoySpeaks = false, text = t }; }
    static DialogueLine G(string t) { return new DialogueLine { gixBoySpeaks = true, text = t }; }

    [MenuItem("GixBoy/Setup Misi & HUD")]
    public static void SetupMissions()
    {
        var city = GameObject.Find("GixBoy_City");
        if (city == null) { Debug.LogError("GixBoy_City tidak ditemukan"); return; }
        var sys = city.transform.Find("Sistem");
        if (sys == null) { sys = new GameObject("Sistem").transform; sys.SetParent(city.transform); }

        // ---- Player ----
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player.GetComponent<GixBoyStats>() == null) player.AddComponent<GixBoyStats>();

        // ---- spawn ----
        var spawn = sys.Find("SpawnPoint_Kos");
        if (spawn == null) { spawn = new GameObject("SpawnPoint_Kos").transform; spawn.SetParent(sys); }
        spawn.position = new Vector3(-61f, 0.2f, 141.5f);
        spawn.rotation = Quaternion.Euler(0f, 180f, 0f);

        // ---- Day/Night ----
        var dn = Get<DayNightController>(sys, "DayNight");
        dn.SetDefaults();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { dn.sun = l; break; }

        // ---- HUD ----
        Get<GameHUD>(sys, "GameHUD");

        // ---- Marker ----
        var marker = Get<MissionMarker>(sys, "PenandaMisi");

        // ---- Police ----
        var ucup = GameObject.Find("GixBoy_City/NPC/NPC_BripkaUcup");
        PolicePatrol police = null;
        if (ucup != null)
        {
            var old = ucup.GetComponent("NPCAvatarController");
            if (old != null) Object.DestroyImmediate(old);
            police = ucup.GetComponent<PolicePatrol>(); if (police == null) police = ucup.AddComponent<PolicePatrol>();
            var pts = new[] {
                new Vector3(75f,0.2f,-17f), new Vector3(20f,0.2f,-17f), new Vector3(-30f,0.2f,-17f), new Vector3(-38f,0.2f,40f),
                new Vector3(-38f,0.2f,73f), new Vector3(40f,0.2f,73f), new Vector3(110f,0.2f,73f), new Vector3(128f,0.2f,30f), new Vector3(128f,0.2f,-12f) };
            var route = new List<Vector3>();
            foreach (var p in pts) { NavMeshHit h; if (NavMesh.SamplePosition(p, out h, 6f, NavMesh.AllAreas)) route.Add(h.position); }
            police.route = route.ToArray();
            EditorUtility.SetDirty(police);
        }

        // ---- Missions ----
        var mm = Get<MissionManager>(sys, "MissionManager");
        mm.marker = marker;
        mm.police = police;
        mm.spawnPoint = spawn;
        mm.omaAni = Npc("NPC_OmaAni");
        mm.lockedLines = new List<DialogueLine> { N("Belum waktunya, Nyong. Nanti kita cari ngana.") };
        mm.missions = new List<Mission>
        {
            new Mission { title = "Bos Lili (Pagi)", boss = Npc("NPC_BosLili"), station = Station("Pom1_PasarBersehati"), stationName = "Pom Pertaminix Pasar Bersehati",
                time = TimeOfDay.Pagi, cansRequired = 3, reward = 100000,
                reminderLines = new List<DialogueLine>{ N("Mana jeriken? Pigi ka Pom Pertaminix dekat pasar, ambe di balakang kios."), G("Iyo, Bos. Kita pigi skarang.") },
                thanksLines = new List<DialogueLine>{ N("So dapa? Bagus, Nyong!"), G("Tiga jeriken, Bos Lili."), N("Taru di balakang karung beras. Ini ngana pe doi."), N("Masih pagi, Bos Luis di Tikala juga cari ngana.") },
                doneLines = new List<DialogueLine>{ N("Makase, Nyong. Pigi ka Bos Luis di Tikala.") } },
            new Mission { title = "Bos Luis (Pagi)", boss = Npc("NPC_BosLuis"), station = Station("Pom2_Tikala"), stationName = "Pom Pertaminix Tikala",
                time = TimeOfDay.Pagi, cansRequired = 3, reward = 125000,
                reminderLines = new List<DialogueLine>{ N("Bensin dari Pom Tikala mana? Orang-orang so antre!"), G("Sabar, Bos.") },
                thanksLines = new List<DialogueLine>{ N("Akhirnya! Ini cukup for tiga hari eceran."), G("...Dorang mo beli tiga kali lipat, Bos?"), N("Bukan ngana pe urusan. Ini doi."), N("Siang nanti Bos Bruno di pelabuhan cari ngana.") },
                doneLines = new List<DialogueLine>{ N("Pigi ka pelabuhan. Bos Bruno so tunggu.") } },
            new Mission { title = "Bos Bruno (Siang)", boss = Npc("NPC_BosBruno"), station = Station("Pom3_Pelabuhan"), stationName = "Pom Pertaminix Pelabuhan",
                time = TimeOfDay.Siang, cansRequired = 4, reward = 175000,
                reminderLines = new List<DialogueLine>{ N("Kapal so mo berangkat! Jeriken dari Pom dekat pelabuhan mana?") },
                thanksLines = new List<DialogueLine>{ N("Bagus. Kapal bisa berangkat skarang."), G("Para nelayan di dermaga so lama nda melaut, Bos..."), N("Jang pikir dorang. Ini ngana pe doi."), N("Malam nanti Ci Ribka di Boulevard cari ngana.") },
                doneLines = new List<DialogueLine>{ N("Malam ini pigi ka Boulevard.") } },
            new Mission { title = "Ci Ribka (Malam)", boss = Npc("NPC_CiRibka"), station = Station("Pom4_Boulevard"), stationName = "Pom Pertaminix Boulevard",
                time = TimeOfDay.Malam, cansRequired = 4, reward = 200000,
                reminderLines = new List<DialogueLine>{ N("Nyong, bensin dari Pom Boulevard? Malam nda lama.") },
                thanksLines = new List<DialogueLine>{ N("Rapi skali karja ngana."), G("Ci, sebenarnya bensin ini ka mana samua?"), N("Ka gudang Opa Hendrik di Malalayang."), N("Pigi ketemu dia. Dia mo kase ngana tantangan terakhir.") },
                doneLines = new List<DialogueLine>{ N("Opa Hendrik so tunggu di Malalayang.") } },
            new Mission { title = "Opa Hendrik (Malam)", boss = Npc("NPC_OpaHendrik"), station = Station("Pom5_Malalayang"), stationName = "Pom Pertaminix Malalayang",
                time = TimeOfDay.Malam, cansRequired = 5, reward = 500000,
                reminderLines = new List<DialogueLine>{ N("Kumpul bensin dari Pom Malalayang, Nyong. Samua.") },
                thanksLines = new List<DialogueLine>{ N("Samua so lengkap..."), N("Ngana so kerja bagus, Nyong. Mulai hari ini, ngana bukan lagi cuma orang suruhan."), N("Ngana so jadi bagian dari torang."), G("..."), G("(Biar susah, jang ambe yang bukan ngana pe hak...)") },
                doneLines = new List<DialogueLine>{ N("Torang pe keluarga, Nyong.") } },
        };
        EditorUtility.SetDirty(mm);

        AddSceneToBuild();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[GixBoy] Setup misi selesai: " + mm.missions.Count + " misi.");
    }

    static T Get<T>(Transform parent, string name) where T : Component
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent); }
        var c = t.GetComponent<T>();
        if (c == null) c = t.gameObject.AddComponent<T>();
        return c;
    }

    static DialogueCutscene Npc(string n)
    {
        var g = GameObject.Find("GixBoy_City/NPC/" + n);
        return g != null ? g.GetComponent<DialogueCutscene>() : null;
    }

    static Transform Station(string n)
    {
        var g = GameObject.Find("GixBoy_City/PomBensin/" + n);
        return g != null ? g.transform : null;
    }

    static void AddSceneToBuild()
    {
        string path = EditorSceneManager.GetActiveScene().path;
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == path))
        {
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
