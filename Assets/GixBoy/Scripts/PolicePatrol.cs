using UnityEngine;
using UnityEngine.AI;

// Bripka Ucup: patroli keliling kota. Kalau melihat GixBoy membawa jeriken, dia mengejar.
// Kalau GixBoy tertangkap (dekat), health berkurang. Kalau GixBoy lolos, Ucup kembali patroli.
[RequireComponent(typeof(NavMeshAgent))]
public class PolicePatrol : MonoBehaviour
{
    public Vector3[] route;
    public float patrolSpeed = 3.2f;
    public float chaseSpeed = 4.6f;
    public float detectRadius = 16f;
    public float loseRadius = 30f;
    public float hitRadius = 2.0f;
    public float damagePerSecond = 14f;
    public float waitAtPoint = 1.5f;

    public string officerName = "Bripka Ucup";
    [Tooltip("Kalau false, polisi hanya berpatroli dan tidak mengejar (misi 1)")]
    public bool canChase = true;

    public bool IsChasing { get; private set; }
    public static readonly System.Collections.Generic.List<PolicePatrol> All = new System.Collections.Generic.List<PolicePatrol>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // dipanggil MissionManager untuk mengatur tingkat kesulitan & rute
    public void Configure(Vector3[] newRoute, bool chase, float detect, float chaseSpd, float dmg)
    {
        canChase = chase; detectRadius = detect; chaseSpeed = chaseSpd; damagePerSecond = dmg;
        if (newRoute != null && newRoute.Length > 0) { route = newRoute; idx = 0; }
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (IsChasing) StopChase(false);
        if (agent != null && agent.isOnNavMesh && route != null && route.Length > 0) { agent.Warp(route[0]); GoTo(route[route.Length > 1 ? 1 : 0]); idx = route.Length > 1 ? 1 : 0; }
    }

    static bool AnyChasing() { foreach (var p in All) if (p != null && p.IsChasing) return true; return false; }

    NavMeshAgent agent;
    Transform player;
    GixBoyStats stats;
    int idx;
    float lostTimer, waitTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; stats = p.GetComponent<GixBoyStats>(); }
        if (route != null && route.Length > 0)
        {
            float best = float.MaxValue;
            for (int i = 0; i < route.Length; i++)
            {
                float d = (route[i] - transform.position).sqrMagnitude;
                if (d < best) { best = d; idx = i; }
            }
            GoTo(route[idx]);
        }
    }

    void GoTo(Vector3 p) { if (agent.isOnNavMesh) agent.SetDestination(p); }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        bool paused = DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying;
        agent.isStopped = paused;
        if (paused || player == null || stats == null) return;
        if (InteriorPortal.InsideRoom) { if (IsChasing) StopChase(true); return; }

        float d = Vector3.Distance(transform.position, player.position);
        bool filling = JerrycanPickup.PlayerFilling;
        bool suspicious = canChase && (stats.carried > 0 || filling) && stats.health > 0f;
        float detect = detectRadius * (filling ? 1.35f : 1f); // mengisi jeriken itu berisik

        if (!IsChasing && suspicious && d < detect && CanSee())
        {
            bool first = !AnyChasing();
            IsChasing = true;
            agent.speed = chaseSpeed;
            AudioManager.Siren(true);
            if (first) GameHUD.Toast(officerName + (filling ? " lihat ngana ambe bensin! LARI!" : " lihat ngana bawa jeriken! LARI!"), new Color(1f, 0.35f, 0.3f));
        }

        if (IsChasing)
        {
            agent.SetDestination(player.position);
            bool inFastTruck = PickupTruck.PlayerInside && PickupTruck.Instance != null && Mathf.Abs(PickupTruck.Instance.Speed) > 3f;
            if (d < hitRadius + (PickupTruck.PlayerInside ? 1.2f : 0f) && !inFastTruck) stats.Damage(damagePerSecond * Time.deltaTime);
            if (d > loseRadius || !suspicious)
            {
                lostTimer += Time.deltaTime;
                if (lostTimer > 2f) StopChase(suspicious);
            }
            else lostTimer = 0f;
        }
        else if (route != null && route.Length > 0 && !agent.pathPending && agent.remainingDistance < 1.5f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtPoint)
            {
                waitTimer = 0f;
                idx = (idx + 1) % route.Length;
                GoTo(route[idx]);
            }
        }
    }

    public void StopChase(bool escaped)
    {
        if (!IsChasing) return;
        IsChasing = false; lostTimer = 0f;
        if (!AnyChasing()) AudioManager.Siren(false);
        agent.speed = patrolSpeed;
        if (route != null && route.Length > 0) GoTo(route[idx]);
        if (escaped && !AnyChasing()) GameHUD.Toast("Ngana lolos dari polisi.", new Color(0.6f, 1f, 0.6f));
    }

    public void ResetToRoute()
    {
        AudioManager.Siren(false);
        IsChasing = false; lostTimer = 0f;
        agent.speed = patrolSpeed;
        if (route != null && route.Length > 0 && agent.isOnNavMesh) { agent.Warp(route[idx]); GoTo(route[(idx + 1) % route.Length]); }
    }

    bool CanSee()
    {
        Vector3 a = transform.position + Vector3.up * 1.6f, b = player.position + Vector3.up * 1.5f;
        foreach (var h in Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(transform) || h.transform.IsChildOf(player)) continue;
            if (h.point.y < 0.8f) continue;
            return false;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, detectRadius);
        if (route == null) return;
        Gizmos.color = Color.blue;
        for (int i = 0; i < route.Length; i++) Gizmos.DrawLine(route[i], route[(i + 1) % route.Length]);
    }
}
