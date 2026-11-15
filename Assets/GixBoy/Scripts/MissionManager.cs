using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class Mission
{
    public string title = "Misi";
    public DialogueCutscene boss;
    public Transform station;
    public string stationName = "Pom";
    public TimeOfDay time = TimeOfDay.Pagi;
    public int cansRequired = 3;
    public int reward = 100000;
    [Tooltip("Dialog saat misi sudah diterima tapi jeriken belum lengkap")]
    public List<DialogueLine> reminderLines = new List<DialogueLine>();
    [Tooltip("Dialog saat GixBoy menyerahkan jeriken")]
    public List<DialogueLine> thanksLines = new List<DialogueLine>();
    [Tooltip("Dialog setelah misi selesai")]
    public List<DialogueLine> doneLines = new List<DialogueLine>();
    [HideInInspector] public List<DialogueLine> briefLines;
}

public enum MissionState { Briefing, Collecting, Delivering, Done }

// Alur misi: temui bos -> ambil jeriken di pom bensin -> serahkan ke bos -> misi berikutnya.
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    public List<Mission> missions = new List<Mission>();
    public DialogueCutscene omaAni;
    public PolicePatrol police;
    public MissionMarker marker;
    public Transform spawnPoint;
    public List<DialogueLine> lockedLines = new List<DialogueLine>();
    [Tooltip("Posisi lokal jeriken di setiap pom (di belakang kios)")]
    public Vector3[] pickupOffsets = {
        new Vector3(-3f, 0f, -10.5f), new Vector3(0f, 0f, -10.5f), new Vector3(3f, 0f, -10.5f),
        new Vector3(-6f, 0f, -10.5f), new Vector3(6f, 0f, -10.5f), new Vector3(-9f, 0f, -10.5f) };

    [Header("Tingkat kesulitan per misi (indeks 0 = misi 1)")]
    public float[] fillTimes = { 0.8f, 1.4f, 2.0f, 2.6f, 3.2f };      // detik tahan E per jeriken
    public float[] pickupSpread = { 1.0f, 1.3f, 1.7f, 2.1f, 2.5f };   // seberapa menyebar jeriken
    public int[] guardCount = { 0, 1, 2, 3, 4 };                       // jumlah polisi penjaga pom
    public float[] guardDetect = { 0f, 12f, 14f, 16f, 18f };
    public float[] guardChaseSpeed = { 0f, 4.4f, 4.8f, 5.2f, 5.6f };
    public float[] guardDamage = { 0f, 10f, 13f, 16f, 20f };
    public float guardRingRadius = 15f;
    public GameObject[] guardPrefabs;
    readonly List<PolicePatrol> extraGuards = new List<PolicePatrol>();
    Vector3[] ucupCityRoute;

    public int CurrentIndex { get; private set; }
    public MissionState State { get; private set; }
    public bool Finished { get; private set; }

    int collected;
    bool started;
    readonly List<JerrycanPickup> livePickups = new List<JerrycanPickup>();
    Transform pickupRoot;
    GixBoyStats stats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        Instance = this;
        foreach (var m in missions)
            if (m.boss != null) m.briefLines = new List<DialogueLine>(m.boss.lines);
        pickupRoot = new GameObject("JerikenMisi").transform;
        pickupRoot.SetParent(transform);
    }

    void OnEnable() { DialogueManager.DialogueFinished += OnDialogueFinished; }
    void OnDisable() { DialogueManager.DialogueFinished -= OnDialogueFinished; }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) stats = p.GetComponent<GixBoyStats>();
        CurrentIndex = 0; State = MissionState.Briefing;
        if (police != null) ucupCityRoute = police.route;
        SetupDifficulty(0);
        RefreshBossLines();
        if (missions.Count > 0 && DayNightController.Instance != null) DayNightController.Instance.Apply(missions[0].time, true);
        if (GameHUD.Instance != null && missions.Count > 0) GameHUD.Instance.SetTime(missions[0].time);
        UpdateObjective(false);
    }

    void Update()
    {
        if (!started && Time.timeSinceLevelLoad > 1.0f && !GixBoyCutscene.IsPlaying)
        {
            started = true;
            UpdateObjective(true);
        }
    }

    Mission Cur { get { return CurrentIndex < missions.Count ? missions[CurrentIndex] : null; } }

    // --------------- event ---------------
    void OnDialogueFinished(DialogueCutscene npc)
    {
        if (npc == omaAni && stats != null)
        {
            if (stats.health < stats.maxHealth) GameHUD.Toast("Health pulih! Makase nasi kuning, Oma Ani.", new Color(0.6f, 1f, 0.6f));
            stats.Heal(stats.maxHealth);
            return;
        }
        var m = Cur;
        if (m == null || npc != m.boss) return;

        if (State == MissionState.Briefing)
        {
            State = MissionState.Collecting;
            collected = 0;
            SpawnPickups(m);
            RefreshBossLines();
            UpdateObjective(true);
            if (PickupTruck.Instance != null) PickupTruck.Instance.GiveNear(InteriorPortal.Resolve(m.boss.transform.position), m.boss.npcDisplayName);
        }
        else if (State == MissionState.Delivering && stats != null && stats.carried >= m.cansRequired)
        {
            stats.TakeCans(m.cansRequired);
            stats.deliveredCans += m.cansRequired;
            AudioManager.Sfx("complete");
        stats.money += m.reward;
            State = MissionState.Done;
            GameHUD.Toast("Misi selesai! +Rp " + m.reward.ToString("N0").Replace(',', '.'), new Color(1f, 0.85f, 0.3f));
            StartCoroutine(NextMission());
        }
    }

    IEnumerator NextMission()
    {
        RefreshBossLines();
        yield return new WaitForSeconds(1.2f);
        CurrentIndex++;
        if (CurrentIndex >= missions.Count)
        {
            Finished = true;
            if (marker != null) marker.Hide();
            if (GameHUD.Instance != null)
            {
                GameHUD.Instance.SetMission("SEMUA MISI SELESAI", "", null);
                yield return new WaitForSeconds(1.5f);
                GameHUD.Instance.ShowEnd("Semua bensin terkumpul dan seluruh misi para bos mafia selesai.\n\n" +
                    "Bensin terkumpul: <b>" + (stats != null ? stats.DeliveredLiters : 0) + " L</b>\n" +
                    "Uang: <b>Rp " + (stats != null ? stats.money.ToString("N0").Replace(',', '.') : "0") + "</b>");
            }
            yield break;
        }
        State = MissionState.Briefing;
        var m = Cur;
        SetupDifficulty(CurrentIndex);
        if (DayNightController.Instance != null && DayNightController.Instance.current != m.time)
        {
            DayNightController.Instance.Apply(m.time);
            GameHUD.Toast(m.time == TimeOfDay.Pagi ? "Pagi hari di Manado..." : m.time == TimeOfDay.Siang ? "Siang hari, matahari terik di Manado..." : "Malam tiba. Lampu-lampu Boulevard menyala...", new Color(0.8f, 0.9f, 1f));
            yield return new WaitForSeconds(3.5f);
        }
        if (GameHUD.Instance != null) GameHUD.Instance.SetTime(m.time);
        RefreshBossLines();
        UpdateObjective(true);
    }

    public void OnPickup(JerrycanPickup p)
    {
        livePickups.Remove(p);
        var m = Cur;
        if (m == null || State != MissionState.Collecting || p.missionIndex != CurrentIndex) return;
        collected++;
        if (stats != null) stats.AddCans(1);
        if (collected >= m.cansRequired)
        {
            State = MissionState.Delivering;
            RefreshBossLines();
            UpdateObjective(true);
        }
        else
        {
            GameHUD.Toast("Jeriken diambil (" + collected + "/" + m.cansRequired + ")", new Color(1f, 0.7f, 0.35f));
            UpdateObjective(false);
        }
    }

    public void OnPlayerCaught()
    {
        StartCoroutine(Caught());
    }

    IEnumerator Caught()
    {
        AudioManager.Sfx("caught"); if (GameHUD.Instance != null) GameHUD.Instance.ShowCaught(true);
        if (PickupTruck.Instance != null) PickupTruck.Instance.ForceExit();
        var m = Cur;
        int lostMoney = stats != null ? Mathf.RoundToInt(stats.money * 0.1f) : 0;
        yield return new WaitForSeconds(2.2f);
        if (stats != null)
        {
            stats.ClearCans();
            stats.money -= lostMoney;
            stats.health = stats.maxHealth;
            var agent = stats.GetComponent<NavMeshAgent>();
            if (spawnPoint != null)
            {
                if (agent != null && agent.isOnNavMesh) agent.Warp(spawnPoint.position);
                else stats.transform.position = spawnPoint.position;
                stats.transform.rotation = spawnPoint.rotation;
            }
        }
        foreach (var pol in PolicePatrol.All.ToArray()) if (pol != null) pol.ResetToRoute();
        if (m != null && (State == MissionState.Collecting || State == MissionState.Delivering))
        {
            State = MissionState.Collecting;
            collected = 0;
            SpawnPickups(m);
            RefreshBossLines();
        }
        if (GameHUD.Instance != null) GameHUD.Instance.ShowCaught(false);
        GameHUD.Toast("Ngana ditangkap Bripka Ucup! Jeriken disita" + (lostMoney > 0 ? ", denda Rp " + lostMoney.ToString("N0").Replace(',', '.') : "") + ".", new Color(1f, 0.4f, 0.35f));
        UpdateObjective(false);
    }

    // --------------- helper ---------------
    void SpawnPickups(Mission m)
    {
        foreach (var p in livePickups) if (p != null) Destroy(p.gameObject);
        livePickups.Clear();
        if (m.station == null) return;
        for (int i = 0; i < m.cansRequired; i++)
        {
            var off = pickupOffsets[i % pickupOffsets.Length];
            float sp = Pick(pickupSpread, CurrentIndex, 1f);
            off = new Vector3(off.x * sp + Random.Range(-1.5f, 1.5f) * (sp - 1f), off.y, off.z * Mathf.Lerp(1f, sp, 0.6f) + Random.Range(-2f, 2f) * (sp - 1f));
            Vector3 pos = m.station.TransformPoint(off);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pos, out hit, 4f, NavMesh.AllAreas)) pos = hit.position;
            livePickups.Add(JerrycanPickup.Spawn(pos, CurrentIndex, pickupRoot));
        }
    }

    string GuardWarning()
    {
        int g = guardCount != null && guardCount.Length > 0 ? guardCount[Mathf.Clamp(CurrentIndex, 0, guardCount.Length - 1)] : 0;
        if (g <= 0) return "";
        return g == 1 ? " Awas, ada polisi jaga!" : " Awas, " + g + " polisi jaga!";
    }

    static float Pick(float[] a, int i, float def) { return a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : def; }

    // atur waktu isi jeriken + polisi penjaga sesuai misi
    void SetupDifficulty(int i)
    {
        var m = i < missions.Count ? missions[i] : null;
        JerrycanPickup.FillTime = Pick(fillTimes, i, 1f);
        int guards = guardCount != null && guardCount.Length > 0 ? guardCount[Mathf.Clamp(i, 0, guardCount.Length - 1)] : 0;
        float det = Pick(guardDetect, i, 14f), spd = Pick(guardChaseSpeed, i, 4.6f), dmg = Pick(guardDamage, i, 14f);

        foreach (var g in extraGuards) if (g != null) Destroy(g.gameObject);
        extraGuards.Clear();

        if (police != null)
        {
            if (guards <= 0 || m == null || m.station == null)
                police.Configure(ucupCityRoute, false, 16f, 4.6f, 14f);          // misi 1: Ucup hanya patroli kota
            else if (i >= 3 && ucupCityRoute != null)
                police.Configure(ucupCityRoute, true, det, spd, dmg);            // misi 4-5: Ucup razia di jalan kota
            else
                police.Configure(RingRoute(m.station.position, 0), true, det, spd, dmg);
        }
        if (m == null || m.station == null) return;
        int extra = (i >= 3) ? guards : guards - 1;
        for (int k = 0; k < extra; k++)
        {
            var g = SpawnGuard(m.station.position, k + 1);
            if (g == null) continue;
            g.Configure(RingRoute(m.station.position, (k + 1) * 2), true, det, spd, dmg);
            extraGuards.Add(g);
        }
    }

    Vector3[] RingRoute(Vector3 center, int phase)
    {
        var pts = new List<Vector3>();
        for (int j = 0; j < 6; j++)
        {
            float a = ((j + phase) % 6) * 60f * Mathf.Deg2Rad;
            float r = guardRingRadius * (j % 2 == 0 ? 1f : 0.75f);
            Vector3 c = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            NavMeshHit h;
            if (NavMesh.SamplePosition(c, out h, 5f, NavMesh.AllAreas)) pts.Add(h.position);
        }
        return pts.ToArray();
    }

    PolicePatrol SpawnGuard(Vector3 near, int n)
    {
        if (police == null) return null;
        GameObject model = guardPrefabs != null && guardPrefabs.Length > 0 ? guardPrefabs[(n - 1) % guardPrefabs.Length] : null;
        GameObject go;
        if (model != null)
        {
            go = Instantiate(model);
            go.transform.localScale = police.transform.localScale;
            var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>();
            var src = police.GetComponent<Animator>();
            if (src != null) { anim.runtimeAnimatorController = src.runtimeAnimatorController; if (anim.avatar == null) anim.avatar = src.avatar; }
            anim.applyRootMotion = false;
            var srcAgent = police.GetComponent<NavMeshAgent>();
            var ag = go.AddComponent<NavMeshAgent>();
            ag.radius = srcAgent.radius; ag.height = srcAgent.height; ag.acceleration = srcAgent.acceleration; ag.angularSpeed = srcAgent.angularSpeed; ag.stoppingDistance = srcAgent.stoppingDistance; ag.baseOffset = srcAgent.baseOffset;
            var rb = go.AddComponent<Rigidbody>(); var srb = police.GetComponent<Rigidbody>(); if (srb != null) { rb.isKinematic = srb.isKinematic; rb.useGravity = srb.useGravity; }
            var cap = go.AddComponent<CapsuleCollider>(); var sc = police.GetComponent<CapsuleCollider>(); if (sc != null) { cap.center = sc.center; cap.radius = sc.radius; cap.height = sc.height; }
            go.AddComponent<MovableObjectMotor>();
            go.AddComponent<PolicePatrol>();
        }
        else
        {
            go = Instantiate(police.gameObject);
            var dc = go.GetComponent<DialogueCutscene>(); if (dc != null) Destroy(dc);
        }
        go.name = "Polisi_Patroli_" + n;
        var pp = go.GetComponent<PolicePatrol>();
        pp.officerName = "Polisi patroli";
        pp.patrolSpeed = police.patrolSpeed; pp.loseRadius = police.loseRadius; pp.hitRadius = police.hitRadius;
        NavMeshHit h;
        Vector3 start = near + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
        if (NavMesh.SamplePosition(start, out h, 8f, NavMesh.AllAreas)) start = h.position;
        var a2 = go.GetComponent<NavMeshAgent>(); a2.enabled = false; go.transform.position = start; a2.enabled = true;
        return pp;
    }

    void RefreshBossLines()
    {
        for (int i = 0; i < missions.Count; i++)
        {
            var m = missions[i];
            if (m.boss == null) continue;
            List<DialogueLine> l;
            if (i < CurrentIndex || (i == CurrentIndex && State == MissionState.Done)) l = m.doneLines;
            else if (i > CurrentIndex) l = lockedLines;
            else if (State == MissionState.Briefing) l = m.briefLines;
            else if (State == MissionState.Collecting) l = m.reminderLines;
            else l = m.thanksLines;
            if (l != null && l.Count > 0) m.boss.lines = l;
        }
    }

    public void RefreshMarker() { UpdateObjective(false); }

    void UpdateObjective(bool announce)
    {
        var m = Cur;
        if (m == null || GameHUD.Instance == null) return;
        string head = "MISI " + (CurrentIndex + 1) + "/" + missions.Count + "  -  " + m.title;
        string obj; Vector3 tpos; Transform tt;
        switch (State)
        {
            case MissionState.Briefing:
                obj = "Temui <b>" + m.boss.npcDisplayName + "</b>, tekan <b>E</b> untuk bicara.";
                tt = m.boss.transform; break;
            case MissionState.Collecting:
                obj = "Ambil jeriken di <b>" + m.stationName + "</b> (" + collected + "/" + m.cansRequired + "). Tahan E di dekat jeriken." + GuardWarning();
                tt = m.station; break;
            default:
                obj = "Bawa " + m.cansRequired + " jeriken ke <b>" + m.boss.npcDisplayName + "</b>.";
                tt = m.boss.transform; break;
        }
        GameHUD.Instance.SetMission(head, obj, tt);
        tpos = tt.position;
        if (marker != null)
        {
            if (State == MissionState.Collecting && livePickups.Count > 0)
            {
                Vector3 c = Vector3.zero; int n = 0;
                foreach (var p in livePickups) if (p != null) { c += p.transform.position; n++; }
                if (n > 0) tpos = c / n;
            }
            marker.SetTarget(InteriorPortal.Resolve(tpos));
        }
        if (announce && started) GameHUD.Toast(obj.Replace("<b>", "").Replace("</b>", ""), new Color(1f, 0.85f, 0.4f));
    }

    void LateUpdate()
    {
        // penanda mengikuti bos yang bisa bergerak sedikit
        var m = Cur;
        if (marker == null || m == null || m.boss == null) return;
        if (State == MissionState.Briefing || State == MissionState.Delivering)
            marker.transform.position = InteriorPortal.Resolve(m.boss.transform.position);
    }
}
