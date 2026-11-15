using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Mobil pickup yang dipinjamkan bos. GixBoy bisa naik/turun (E) dan menyetir (W/S gas-mundur, A/D belok, Spasi rem).
// Jeriken yang dibawa GixBoy tampil di bak belakang saat menyetir. Gerak mobil dibatasi NavMesh supaya tidak menembus gedung.
public class PickupTruck : MonoBehaviour
{
    public static PickupTruck Instance { get; private set; }
    public static bool PlayerInside { get; private set; }

    [Header("Setir")]
    public float maxSpeed = 15f;
    public float reverseSpeed = 6f;
    public float acceleration = 9f;
    public float brakeForce = 22f;
    public float drag = 3f;
    public float steerSpeed = 110f;
    public float enterDistance = 4.2f;
    public float halfWidth = 0.95f;
    public float halfLength = 1.9f;

    [Header("Bagian")]
    public Transform model;
    public Transform[] frontWheels;
    public Transform[] allWheels;
    public Transform bed;

    [HideInInspector] public float debugThrottle, debugSteer;

    public float Speed { get { return speed; } }
    public bool Available { get; private set; }

    Transform player;
    PlayerAvatarController controller;
    NavMeshAgent agent;
    PlayerSprint sprint;
    GixBoyStats stats;
    CinemachineCamera cmCam;
    CinemachineFollow cmFollow;
    GameplayCameraZoom zoom;
    float savedDistance, savedPitch;

    float speed, steerVisual, wheelSpin, enterCooldown, crashToastTime;
    readonly List<GameObject> bedCans = new List<GameObject>();
    readonly List<Collider> playerColliders = new List<Collider>();
    readonly List<Renderer> playerRenderers = new List<Renderer>();
    TextMeshProUGUI prompt;
    GameObject promptRoot;
    AudioSource engine;

    static readonly Vector3[] BedSlots = {
        new Vector3(-0.46f, 0f, -2.05f), new Vector3(0f, 0f, -2.05f), new Vector3(0.46f, 0f, -2.05f),
        new Vector3(-0.46f, 0f, -1.58f), new Vector3(0f, 0f, -1.58f), new Vector3(0.46f, 0f, -1.58f),
        new Vector3(-0.46f, 0f, -1.1f), new Vector3(0f, 0f, -1.1f), new Vector3(0.46f, 0f, -1.1f),
        new Vector3(-0.24f, 0.55f, -1.8f), new Vector3(0.24f, 0.55f, -1.8f), new Vector3(0f, 0.55f, -1.34f) };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { PlayerInside = false; Instance = null; }

    void Awake() { Instance = this; PlayerInside = false; }
    void OnDestroy() { if (Instance == this) { Instance = null; PlayerInside = false; } }

    void Start()
    {
        var p = GameObject.Find("Player");
        if (p != null)
        {
            player = p.transform;
            controller = p.GetComponent<PlayerAvatarController>();
            agent = p.GetComponent<NavMeshAgent>();
            sprint = p.GetComponent<PlayerSprint>();
            stats = p.GetComponent<GixBoyStats>();
        }
        var cm = GameObject.Find("CM_Gameplay");
        if (cm != null) { cmCam = cm.GetComponent<CinemachineCamera>(); cmFollow = cm.GetComponent<CinemachineFollow>(); zoom = cm.GetComponent<GameplayCameraZoom>(); }
        BuildPrompt();
        BuildEngine();
        SetAvailable(false);
    }

    // ---------------- dipanggil MissionManager ----------------
    public void GiveNear(Vector3 bossPos, string bossName)
    {
        if (PlayerInside) return;
        // cari titik jalan terdekat yang lapang di sekitar bos
        Vector3 best = bossPos; float bestScore = -1f;
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f * Mathf.Deg2Rad;
            for (int r = 6; r <= 14; r += 4)
            {
                Vector3 c = bossPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                NavMeshHit h;
                if (!NavMesh.SamplePosition(c, out h, 1.5f, NavMesh.AllAreas)) continue;
                NavMeshHit edge;
                float clear = NavMesh.FindClosestEdge(h.position, out edge, NavMesh.AllAreas) ? edge.distance : 0f;
                float score = clear - r * 0.05f;
                if (score > bestScore) { bestScore = score; best = h.position; }
            }
        }
        transform.position = best;
        Vector3 toBoss = bossPos - best; toBoss.y = 0f;
        transform.rotation = toBoss.sqrMagnitude > 0.01f ? Quaternion.LookRotation(Vector3.Cross(Vector3.up, toBoss.normalized)) : transform.rotation;
        speed = 0f;
        SetAvailable(true);
        GameHUD.Toast(bossName + " kase pinjam mobil pickup! Dekati mobil lalu tekan E untuk naik.", new Color(1f, 0.85f, 0.35f));
    }

    public void SetAvailable(bool on)
    {
        Available = on;
        if (model != null) model.gameObject.SetActive(on);
        if (!on && PlayerInside) Exit(true);
    }

    public void ForceExit() { if (PlayerInside) Exit(true); }

    // ---------------- update ----------------
    void Update()
    {
        if (player == null) return;
        enterCooldown -= Time.deltaTime;
        bool blocked = GameHUD.IsPaused || DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying;

        if (!PlayerInside)
        {
            bool near = Available && !blocked && controller != null && controller.enabled
                        && Vector3.Distance(player.position, transform.position) < enterDistance;
            ShowPrompt(near ? "Tekan  <b>[E]</b>  untuk naik pickup" : null);
            if (near && enterCooldown <= 0f && Input.GetKeyDown(KeyCode.E)) Enter();
            speed = Mathf.MoveTowards(speed, 0f, brakeForce * Time.deltaTime);
            if (engine.isPlaying) engine.Stop();
            return;
        }

        if (blocked) { ShowPrompt(null); return; }

        Drive();
        UpdateBed();
        HidePlayer();
        int kmh = Mathf.RoundToInt(Mathf.Abs(speed) * 3.6f);
        ShowPrompt("<b>" + kmh + " km/j</b>    W/S gas-mundur  ·  A/D belok  ·  Spasi rem  ·  <b>[E]</b> turun");
        if (enterCooldown <= 0f && Input.GetKeyDown(KeyCode.E) && Mathf.Abs(speed) < 4f) Exit(false);

        if (!engine.isPlaying) engine.Play();
        engine.pitch = 0.75f + Mathf.Abs(speed) / maxSpeed * 1.1f;
        engine.volume = 0.32f * GameSettings.SfxVolume;
    }

    void Drive()
    {
        float v = Mathf.Clamp(Input.GetAxisRaw("Vertical") + debugThrottle, -1f, 1f);
        float h = Mathf.Clamp(Input.GetAxisRaw("Horizontal") + debugSteer, -1f, 1f);
        bool hand = Input.GetKey(KeyCode.Space);
        float dt = Time.deltaTime;

        if (v > 0.1f) speed = speed < 0f ? Mathf.MoveTowards(speed, 0f, brakeForce * dt) : Mathf.MoveTowards(speed, maxSpeed, acceleration * dt);
        else if (v < -0.1f) speed = speed > 0f ? Mathf.MoveTowards(speed, 0f, brakeForce * dt) : Mathf.MoveTowards(speed, -reverseSpeed, acceleration * 0.7f * dt);
        else speed = Mathf.MoveTowards(speed, 0f, drag * dt);
        if (hand) speed = Mathf.MoveTowards(speed, 0f, brakeForce * 1.3f * dt);

        float steerFactor = Mathf.Clamp01(Mathf.Abs(speed) / 3f) * Mathf.Sign(speed);
        float yaw = h * steerSpeed * steerFactor * (1f - Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed) * 0.35f) * dt;
        steerVisual = Mathf.Lerp(steerVisual, h * 28f, dt * 8f);

        Quaternion newRot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = newRot * Vector3.forward;
        Vector3 move = fwd * speed * dt;
        Vector3 pos = transform.position;
        Vector3 target = pos + move;

        // cek tabrakan dengan batas NavMesh di ujung depan/belakang kiri-kanan
        if (move.sqrMagnitude > 1e-6f)
        {
            float dir = Mathf.Sign(speed);
            Vector3 right = newRot * Vector3.right;
            Vector3 tip = fwd * halfLength * dir;
            bool hit = false;
            foreach (float side in new[] { -halfWidth, 0f, halfWidth })
            {
                Vector3 a = pos + tip * 0.9f + right * side;
                NavMeshHit nh;
                if (NavMesh.Raycast(a, a + move + fwd * 0.15f * dir, out nh, NavMesh.AllAreas)) { hit = true; break; }
            }
            if (hit)
            {
                if (Mathf.Abs(speed) > 6f && Time.time > crashToastTime) { crashToastTime = Time.time + 2f; GameHUD.Toast("Brak! Hati-hati menyetir.", new Color(1f, 0.6f, 0.4f)); AudioManager.Sfx("caught", 0.4f); }
                speed = -speed * 0.2f;
                target = pos;
                yaw *= 0.3f;
                newRot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            }
        }
        NavMeshHit g;
        if (NavMesh.SamplePosition(target, out g, 2f, NavMesh.AllAreas)) target.y = Mathf.Lerp(pos.y, g.position.y, dt * 10f);
        transform.SetPositionAndRotation(target, newRot);

        // roda
        wheelSpin += speed * dt / 0.26f * Mathf.Rad2Deg;
        if (allWheels != null) foreach (var w in allWheels) if (w != null) w.localRotation = Quaternion.Euler(wheelSpin, IsFront(w) ? steerVisual : 0f, 0f);
        // sedikit miring saat belok
        if (model != null) model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(-Mathf.Clamp(speed * 0.05f, -1f, 1f), 0f, -h * Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed) * 3f), dt * 5f);
    }

    bool IsFront(Transform w) { if (frontWheels == null) return false; foreach (var f in frontWheels) if (f == w) return true; return false; }

    // ---------------- naik / turun ----------------
    void Enter()
    {
        PlayerInside = true; enterCooldown = 0.4f;
        if (agent != null) { if (agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
        if (controller != null) controller.enabled = false;
        if (sprint != null) sprint.enabled = false;
        var anim = player.GetComponentInChildren<Animator>(); if (anim != null) anim.SetFloat("Speed_f", 0f);
        playerColliders.Clear();
        foreach (var c in player.GetComponentsInChildren<Collider>()) if (c.enabled) { c.enabled = false; playerColliders.Add(c); }
        playerRenderers.Clear();
        foreach (var r in player.GetComponentsInChildren<Renderer>(true)) if (r.enabled) playerRenderers.Add(r);
        player.SetParent(transform, true);
        player.localPosition = new Vector3(-0.5f, 0.25f, 0.3f);
        player.localRotation = Quaternion.identity;
        HidePlayer();
        if (cmCam != null) { cmCam.Follow = transform; cmCam.LookAt = transform; }
        if (cmFollow != null)
        {
            var ts = cmFollow.TrackerSettings;
            ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
            ts.RotationDamping = new Vector3(0f, 1.2f, 0f);
            cmFollow.TrackerSettings = ts;
        }
        if (zoom != null) { savedDistance = zoom.distance; savedPitch = zoom.pitch; zoom.distance = Mathf.Max(zoom.distance, 15f); zoom.pitch = 30f; }
        AudioManager.Sfx("click");
    }

    void Exit(bool forced)
    {
        PlayerInside = false; enterCooldown = 0.4f; speed = forced ? 0f : speed;
        player.SetParent(null, true);
        Vector3 exitPos = transform.TransformPoint(new Vector3(-2.1f, 0f, 0.3f));
        NavMeshHit h;
        if (!NavMesh.SamplePosition(exitPos, out h, 3f, NavMesh.AllAreas)) NavMesh.SamplePosition(transform.TransformPoint(new Vector3(2.1f, 0f, 0.3f)), out h, 4f, NavMesh.AllAreas);
        player.position = h.hit ? h.position : exitPos;
        player.rotation = Quaternion.LookRotation(transform.forward);
        foreach (var c in playerColliders) if (c != null) c.enabled = true;
        playerColliders.Clear();
        foreach (var r in playerRenderers) if (r != null) r.enabled = true;
        playerRenderers.Clear();
        if (agent != null) { agent.enabled = true; if (agent.isOnNavMesh) agent.Warp(player.position); }
        if (controller != null) controller.enabled = true;
        if (sprint != null) sprint.enabled = true;
        if (cmCam != null) { cmCam.Follow = player; cmCam.LookAt = player; }
        if (cmFollow != null)
        {
            var ts = cmFollow.TrackerSettings;
            ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            cmFollow.TrackerSettings = ts;
        }
        if (zoom != null && savedDistance > 0f) { zoom.distance = savedDistance; zoom.pitch = savedPitch; }
        UpdateBed();
        if (engine != null) engine.Stop();
        ShowPrompt(null);
    }

    void HidePlayer() { foreach (var r in player.GetComponentsInChildren<Renderer>(true)) r.enabled = false; }

    // jeriken yang dibawa tampil di bak saat GixBoy menyetir
    void UpdateBed()
    {
        int want = PlayerInside && stats != null ? Mathf.Min(stats.carried, BedSlots.Length) : 0;
        while (bedCans.Count < want)
        {
            int i = bedCans.Count;
            var can = JerrycanVisual.Create(bed, 0.52f);
            can.transform.localPosition = BedSlots[i];
            can.transform.localRotation = Quaternion.Euler(0f, 90f + (i * 37 % 20) - 10f, 0f);
            bedCans.Add(can);
        }
        while (bedCans.Count > want) { Destroy(bedCans[bedCans.Count - 1]); bedCans.RemoveAt(bedCans.Count - 1); }
    }

    // ---------------- UI & suara ----------------
    void BuildPrompt()
    {
        var canvas = GameObject.Find("HUDCanvas");
        if (canvas == null) return;
        promptRoot = new GameObject("PromptPickup", typeof(RectTransform));
        promptRoot.transform.SetParent(canvas.transform, false);
        var rt = promptRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 70f); rt.sizeDelta = new Vector2(1100f, 64f);
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

    void BuildEngine()
    {
        engine = gameObject.AddComponent<AudioSource>();
        engine.loop = true; engine.playOnAwake = false; engine.spatialBlend = 0f;
        int rate = 44100, n = rate; // 1 detik
        var data = new float[n];
        float seed = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = 46f;
            float saw = 2f * (t * f - Mathf.Floor(t * f + 0.5f));
            float s = saw * 0.45f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * f * 0.5f * t) * 0.3f;
            seed = seed * 0.92f + (Random.value * 2f - 1f) * 0.08f;
            s += seed * 0.5f;
            s *= 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 9f * t); // getaran mesin
            data[i] = s * 0.5f;
        }
        var clip = AudioClip.Create("engine", n, 1, rate, false);
        clip.SetData(data, 0);
        engine.clip = clip;
    }
}
