using UnityEngine;
using UnityEngine.AI;
using Unity.Cinemachine;

// Fitur LARI: tahan Shift kiri untuk berlari. Lari menguras stamina,
// stamina pulih saat berjalan/diam. Jeriken yang dibawa memperlambat lari.
[DefaultExecutionOrder(50)]
public class PlayerSprint : MonoBehaviour
{
    public static PlayerSprint Instance { get; private set; }

    [Header("Kecepatan (m/detik)")]
    public float walkSpeed = 4.2f;
    public float runSpeed = 7.5f;
    [Tooltip("Pengurangan kecepatan lari per jeriken yang dibawa")]
    public float slowPerCan = 0.3f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 17f;
    public float regenPerSecond = 12f;
    public float regenDelay = 1f;
    [Tooltip("Setelah stamina habis, harus pulih sampai nilai ini sebelum bisa lari lagi")]
    public float recoverThreshold = 30f;

    [Header("Kamera")]
    public float runFovBoost = 6f;

    [HideInInspector] public bool debugForceRun;

    public float Stamina { get; private set; }
    public bool IsRunning { get; private set; }
    public bool Exhausted { get; private set; }
    public float Stamina01 { get { return Stamina / maxStamina; } }

    PlayerAvatarController controller;
    NavMeshAgent agent;
    GixBoyStats stats;
    CinemachineCamera cmCam;
    float baseFov = -1f, regenTimer, stepTimer;

    void Awake() { Instance = this; Stamina = maxStamina; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        controller = GetComponent<PlayerAvatarController>();
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<GixBoyStats>();
        var cm = GameObject.Find("CM_Gameplay");
        if (cm != null) { cmCam = cm.GetComponent<CinemachineCamera>(); if (cmCam != null) baseFov = cmCam.Lens.FieldOfView; }
    }

    // Kecepatan saat ini dipakai oleh PlayerAvatarController
    public float CurrentSpeed
    {
        get
        {
            if (!IsRunning) return walkSpeed;
            int cans = stats != null ? stats.carried : 0;
            return Mathf.Max(walkSpeed + 0.5f, runSpeed - cans * slowPerCan);
        }
    }

    // Nilai Speed_f untuk Animator: 0.3 = Walking, 1 = Running
    public float AnimSpeed { get { return IsRunning ? 1f : 0.3f; } }

    void Update()
    {
        bool canControl = controller != null && controller.enabled && !GameHUD.IsPaused;
        bool wasd = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        bool clickMoving = agent != null && agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.1f;
        bool moving = canControl && (wasd || clickMoving);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || debugForceRun;

        if (Exhausted && Stamina >= recoverThreshold) Exhausted = false;
        IsRunning = moving && shift && !Exhausted && Stamina > 0f;

        if (IsRunning)
        {
            Stamina = Mathf.Max(0f, Stamina - drainPerSecond * Time.deltaTime);
            regenTimer = regenDelay;
            if (Stamina <= 0f) { Exhausted = true; IsRunning = false; }
        }
        else
        {
            regenTimer -= Time.deltaTime;
            if (regenTimer <= 0f) Stamina = Mathf.Min(maxStamina, Stamina + regenPerSecond * (moving ? 0.6f : 1f) * Time.deltaTime);
        }

        // langkah kaki
        if (moving) { stepTimer -= Time.deltaTime; if (stepTimer <= 0f) { stepTimer = IsRunning ? 0.29f : 0.46f; AudioManager.Sfx("step", IsRunning ? 0.35f : 0.22f); } }
        else stepTimer = 0.1f;

        // kecepatan untuk klik-untuk-jalan (NavMeshAgent)
        if (agent != null) agent.speed = CurrentSpeed;

        // sedikit efek kamera saat lari
        if (cmCam != null && baseFov > 0f)
        {
            var lens = cmCam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, baseFov + (IsRunning ? runFovBoost : 0f), Time.deltaTime * 4f);
            cmCam.Lens = lens;
        }
    }

    public void RestoreFull() { Stamina = maxStamina; Exhausted = false; }
}
