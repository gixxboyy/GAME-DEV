using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

// Mengatur cutscene dialog: bar sinematik, kotak dialog, dan kamera Cinemachine
// yang bergantian menyorot lawan bicara dan GixBoy (over-the-shoulder).
public class DialogueManager : MonoBehaviour
{
    static DialogueManager inst;
    public static bool HasInstance { get { return inst != null; } }
    public static DialogueManager Instance
    {
        get
        {
            if (inst == null) inst = FindAnyObjectByType<DialogueManager>();
            if (inst == null) inst = new GameObject("DialogueManager").AddComponent<DialogueManager>();
            return inst;
        }
    }
    public static bool IsPlaying { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; IsPlaying = false; DialogueFinished = null; }
    public static event System.Action<DialogueCutscene> DialogueFinished;

    [Header("Kamera")]
    public float shotDistance = 2.3f;     // jarak kamera di belakang bahu pendengar
    public float shotSide = 0.85f;         // geser ke samping (over-the-shoulder)
    public float shotHeight = 2.15f;       // tinggi kamera
    public float shotFov = 40f;
    public float wideDistance = 6f;       // jarak bidikan pembuka (dua karakter)
    public float blendTime = 0.6f;

    [Header("Teks")]
    public float charsPerSecond = 40f;
    public Color gixBoyColor = new Color(1f, 0.78f, 0.2f);
    public Color npcColor = new Color(0.55f, 0.85f, 1f);
    public string gixBoyName = "GixBoy";

    Canvas canvas;
    RectTransform topBar, bottomBar, panel;
    TextMeshProUGUI nameText, bodyText, hintText, promptText;
    CinemachineCamera camA, camB;
    bool useA;
    DialogueCutscene promptOwner;

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        BuildUI();
        camA = MakeCam("DialogueCam_A");
        camB = MakeCam("DialogueCam_B");
    }

    // ---------------- Prompt ----------------
    public void SetPrompt(DialogueCutscene owner, bool show)
    {
        if (show) { promptOwner = owner; promptText.text = "Tekan  <b>[E]</b>  untuk bicara dengan <b>" + owner.npcDisplayName + "</b>"; promptGO.SetActive(true); }
        else if (promptOwner == owner) { promptOwner = null; promptGO.SetActive(false); }
    }

    // ---------------- Play ----------------
    public void Play(DialogueCutscene npc, Transform player)
    {
        if (IsPlaying) return;
        StartCoroutine(Run(npc, player));
    }

    IEnumerator Run(DialogueCutscene npc, Transform player)
    {
        IsPlaying = true;
        promptOwner = null; promptGO.SetActive(false);

        // --- bekukan player & matikan kamera lama ---
        var cam = Camera.main;
        var chase = cam != null ? cam.GetComponent("ChaseCamera") as Behaviour : null;
        var fade = cam != null ? cam.GetComponent("FadeCameraObstructions") as Behaviour : null;
        var brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        var pac = player.GetComponent("PlayerAvatarController") as Behaviour;
        var agent = player.GetComponent<NavMeshAgent>();
        var pAnim = player.GetComponent<Animator>();
        var npcAgent = npc.GetComponent<NavMeshAgent>();
        var npcAnim = npc.GetComponent<Animator>();
        bool chaseWas = chase != null && chase.enabled;
        bool fadeWas = fade != null && fade.enabled;
        bool pacWas = pac != null && pac.enabled;

        if (pac != null) pac.enabled = false;
        InputManager.bCanPlayerMove = false;
        if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; agent.isStopped = true; }
        if (npcAgent != null && npcAgent.isOnNavMesh) { npcAgent.ResetPath(); npcAgent.velocity = Vector3.zero; npcAgent.isStopped = true; }
        if (pAnim != null) pAnim.SetFloat("Speed_f", 0f);
        if (npcAnim != null) npcAnim.SetFloat("Speed_f", 0f);

        // saling berhadapan
        FaceEachOther(player, npc.transform);

        // --- kamera: mulai dari posisi kamera sekarang supaya transisinya halus ---
        CinemachineBlendDefinition oldBlend = brain != null ? brain.DefaultBlend : default(CinemachineBlendDefinition);
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        if (chase != null) chase.enabled = false;
        if (fade != null) fade.enabled = false;
        if (cam != null) { camA.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation); SetLens(camA, cam.fieldOfView); }
        Activate(camA); useA = true;
        yield return null;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.0f);

        // bidikan pembuka: kedua karakter dari samping
        var next = useA ? camB : camA; useA = !useA;
        FrameWide(next, player, npc.transform, npc.headHeight);
        Activate(next);
        if (npcAnim != null) npcAnim.SetTrigger("Wave");
        yield return StartCoroutine(Bars(true, 0.5f));
        yield return new WaitForSeconds(0.6f);
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);

        panel.gameObject.SetActive(true);
        bool lastSpeakerG = false; bool first = true;
        foreach (var line in npc.lines)
        {
            Transform speaker = line.gixBoySpeaks ? player : npc.transform;
            Transform listener = line.gixBoySpeaks ? npc.transform : player;
            float sh = line.gixBoySpeaks ? npc.headHeight : npc.headHeight;
            if (first || line.gixBoySpeaks != lastSpeakerG)
            {
                next = useA ? camB : camA; useA = !useA;
                FrameOverShoulder(next, speaker, listener, sh);
                Activate(next);
            }
            first = false; lastSpeakerG = line.gixBoySpeaks;

            nameText.text = line.gixBoySpeaks ? gixBoyName : npc.npcDisplayName; AudioManager.Sfx("blip", 0.6f);
            nameText.color = line.gixBoySpeaks ? gixBoyColor : npcColor;
            yield return StartCoroutine(TypeLine(line.text));
            hintText.gameObject.SetActive(true);
            yield return StartCoroutine(WaitAdvance());
            hintText.gameObject.SetActive(false);
        }

        // --- selesai: kembalikan semuanya ---
        panel.gameObject.SetActive(false);
        yield return StartCoroutine(Bars(false, 0.4f));
        camA.enabled = false; camB.enabled = false;
        if (brain != null) brain.DefaultBlend = oldBlend;
        if (chase != null && chaseWas) chase.enabled = true;
        if (fade != null && fadeWas) fade.enabled = true;
        if (agent != null && agent.isOnNavMesh) { agent.isStopped = false; agent.ResetPath(); }
        if (npcAgent != null && npcAgent.isOnNavMesh) npcAgent.isStopped = false;
        if (pac != null && pacWas) pac.enabled = true;
        InputManager.bCanPlayerMove = true;
        IsPlaying = false;

        if (npc.onDialogueFinished != null) npc.onDialogueFinished.Invoke();
        if (DialogueFinished != null) DialogueFinished(npc);
    }

    // ---------------- Kamera ----------------
    CinemachineCamera MakeCam(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform);
        var c = go.AddComponent<CinemachineCamera>();
        c.Priority = 500;
        SetLens(c, shotFov);
        c.enabled = false;
        return c;
    }

    void SetLens(CinemachineCamera c, float fov)
    {
        var l = c.Lens; l.FieldOfView = fov; l.NearClipPlane = 0.1f; l.FarClipPlane = 1000f; c.Lens = l;
    }

    void Activate(CinemachineCamera c)
    {
        var other = c == camA ? camB : camA;
        c.enabled = true; c.Priority = 600;
        other.Priority = 500;
        // matikan kamera yang lain setelah blend selesai
        StartCoroutine(DisableLater(other, blendTime + 0.1f, c));
    }

    IEnumerator DisableLater(CinemachineCamera c, float t, CinemachineCamera keep)
    {
        yield return new WaitForSeconds(t);
        if (c != keep && c.Priority < 600) c.enabled = false;
    }

    void FrameOverShoulder(CinemachineCamera c, Transform speaker, Transform listener, float head)
    {
        Vector3 dir = speaker.position - listener.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = listener.forward;
        dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 look = speaker.position + Vector3.up * (head - 0.2f);
        var cands = new System.Collections.Generic.List<Vector3>();
        foreach (float lift in new[] { 0f, 1.2f, 2.4f })
        {
            Vector3 up = Vector3.up * (shotHeight + lift);
            cands.Add(listener.position - dir * shotDistance + right * (shotSide + 0.35f) + up + Vector3.up * 0.35f);   // bahu kanan pendengar
            cands.Add(listener.position - dir * shotDistance - right * (shotSide + 0.35f) + up + Vector3.up * 0.35f);   // bahu kiri pendengar
            cands.Add(speaker.position - dir * 2.3f + right * 1.0f + up);              // close-up depan-kanan
            cands.Add(speaker.position - dir * 2.3f - right * 1.0f + up);              // close-up depan-kiri
            cands.Add(speaker.position - dir * 1.7f + right * 0.45f + up);              // close-up dekat
            cands.Add(speaker.position + right * 2.5f - dir * 0.7f + up);              // samping kanan
            cands.Add(speaker.position - right * 2.5f - dir * 0.7f + up);              // samping kiri
        }
        Vector3 pos = Choose(cands, look, speaker, listener);
        c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        SetLens(c, shotFov);
    }

    void FrameWide(CinemachineCamera c, Transform a, Transform b, float head)
    {
        Vector3 mid = (a.position + b.position) * 0.5f;
        Vector3 dir = b.position - a.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = a.forward;
        dir.Normalize();
        Vector3 look = mid + Vector3.up * (head * 0.7f);
        var cands = new System.Collections.Generic.List<Vector3>();
        foreach (float lift in new[] { 0.6f, 2.2f, 4.5f })
            foreach (float r in new[] { wideDistance, wideDistance * 0.7f })
                foreach (float ang in new[] { 90f, -90f, 60f, -60f, 120f, -120f })
                    cands.Add(mid + Quaternion.Euler(0f, ang, 0f) * dir * r + Vector3.up * (head + lift));
        Vector3 pos = Choose(cands, look, a, b);
        c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        SetLens(c, 45f);
    }

    // kepala/badan karakter sendiri yang menutupi pandangan (mis. kepala GixBoy di shot bahu)
    int BodyInTheWay(Vector3 from, Vector3 look, Transform t)
    {
        if (t == null) return 0;
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0;
        Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
        int pen = 0;
        float top = bb.max.y;
        foreach (float k in new[] { 0.12f, 0.35f, 0.6f })
        {
            Vector3 p = new Vector3(t.position.x, top - k * (top - t.position.y), t.position.z);
            if ((p - look).sqrMagnitude < 0.8f * 0.8f) continue; // titik itu adalah target bidikan
            Vector3 d = look - from; float len = d.magnitude; if (len < 0.01f) continue;
            float along = Vector3.Dot(p - from, d / len);
            if (along <= 0.2f || along >= len - 0.6f) continue;
            float dist = Vector3.Distance(from + d / len * along, p);
            if (dist < 0.38f) pen += 3; else if (dist < 0.55f) pen += 1;
        }
        return pen;
    }

    // pilih posisi kamera yang paling sedikit terhalang (0 = bersih)
    Vector3 Choose(System.Collections.Generic.List<Vector3> cands, Vector3 look, Transform a, Transform b)
    {
        int best = int.MaxValue; Vector3 pos = cands[0];
        foreach (var cpos in cands)
        {
            int n = Blockers(cpos, look, a, b) + Blockers(cpos, a.position + Vector3.up * 1.4f, a, b) / 2 + Blockers(cpos, b.position + Vector3.up * 1.4f, a, b) / 2;
            n += BodyInTheWay(cpos, look, a) + BodyInTheWay(cpos, look, b);
            if (n < best) { best = n; pos = cpos; if (n == 0) break; }
        }
        return pos;
    }

    Renderer[] sceneRenderers;
    int Blockers(Vector3 from, Vector3 to, Transform a, Transform b)
    {
        int count = 0;
        Vector3 d = to - from; float len = d.magnitude; if (len < 0.01f) return 0;
        Ray ray = new Ray(from, d / len);
        foreach (var h in Physics.RaycastAll(ray, Mathf.Max(0.01f, len - 0.3f), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(a) || h.transform.IsChildOf(b)) continue;
            if (h.point.y < 0.6f) continue;
            count += 2;
        }
        if (sceneRenderers == null) sceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        Vector3 mid = (from + to) * 0.5f;
        foreach (var r in sceneRenderers)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            var bb = r.bounds;
            if (bb.size.y < 0.4f || bb.max.y < 0.9f) continue;
            if ((bb.center - mid).sqrMagnitude > 1600f && !bb.Contains(mid)) continue;
            if (r.transform.IsChildOf(a) || r.transform.IsChildOf(b)) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            float dist;
            if (bb.Contains(from)) { count += 2; continue; }
            if (bb.IntersectRay(ray, out dist) && dist > 0.2f && dist < len - 0.4f) count++;
        }
        return count;
    }

    void FaceEachOther(Transform a, Transform b)
    {
        Vector3 d = b.position - a.position; d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        a.rotation = Quaternion.LookRotation(d);
        b.rotation = Quaternion.LookRotation(-d);
    }

    // ---------------- UI ----------------
    IEnumerator TypeLine(string text)
    {
        bodyText.text = text;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        float shown = 0f;
        yield return null;
        while (bodyText.maxVisibleCharacters < total)
        {
            if (AdvancePressed()) { bodyText.maxVisibleCharacters = total; yield return null; break; }
            shown += Time.deltaTime * charsPerSecond;
            bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }
        bodyText.maxVisibleCharacters = 99999;
    }

    IEnumerator WaitAdvance()
    {
        yield return null;
        while (!AdvancePressed()) yield return null;
    }

    bool AdvancePressed()
    {
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
    }

    IEnumerator Bars(bool show, float time)
    {
        float from = show ? 0f : 130f, to = show ? 130f : 0f, t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float h = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
            topBar.sizeDelta = new Vector2(0f, h); bottomBar.sizeDelta = new Vector2(0f, h);
            yield return null;
        }
        topBar.sizeDelta = new Vector2(0f, to); bottomBar.sizeDelta = new Vector2(0f, to);
    }

    void BuildUI()
    {
        var cgo = new GameObject("DialogueCanvas");
        cgo.transform.SetParent(transform);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        topBar = Bar("BarAtas", true);
        bottomBar = Bar("BarBawah", false);

        // panel dialog
        var p = new GameObject("PanelDialog", typeof(RectTransform), typeof(Image));
        p.transform.SetParent(cgo.transform, false);
        panel = p.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.12f, 0f); panel.anchorMax = new Vector2(0.88f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        panel.sizeDelta = new Vector2(0f, 230f);
        p.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
        var outline = p.AddComponent<Outline>(); outline.effectColor = new Color(1f, 0.78f, 0.2f, 0.6f); outline.effectDistance = new Vector2(3, -3);

        nameText = Txt("Nama", panel, new Vector2(30, -18), new Vector2(-60, 50), 38, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        bodyText = Txt("Isi", panel, new Vector2(30, -74), new Vector2(-60, 130), 34, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyText.color = Color.white;
        hintText = Txt("Lanjut", panel, new Vector2(30, -188), new Vector2(-60, 32), 24, FontStyles.Italic, TextAlignmentOptions.BottomRight);
        hintText.text = "Klik / E / Spasi untuk lanjut  ▼";
        hintText.color = new Color(1f, 1f, 1f, 0.7f);
        hintText.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);

        // prompt bicara
        var pr = new GameObject("PromptBicara", typeof(RectTransform));
        pr.transform.SetParent(cgo.transform, false);
        var prt = pr.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 150f); prt.sizeDelta = new Vector2(1000f, 70f);
        var bg = pr.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.6f);
        promptText = Txt("Teks", prt, new Vector2(0, 0), new Vector2(0, 0), 32, FontStyles.Normal, TextAlignmentOptions.Center);
        var ptr = promptText.rectTransform; ptr.anchorMin = Vector2.zero; ptr.anchorMax = Vector2.one; ptr.offsetMin = Vector2.zero; ptr.offsetMax = Vector2.zero;
        promptText.color = Color.white;
        pr.SetActive(false);
        promptText = pr.GetComponentInChildren<TextMeshProUGUI>(true);
        promptGO = pr;
    }
    GameObject promptGO;

    RectTransform Bar(string n, bool top)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.GetComponent<Image>().color = Color.black;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot = new Vector2(0.5f, top ? 1f : 0f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0f, 0f);
        return r;
    }

    TextMeshProUGUI Txt(string n, RectTransform parent, Vector2 pos, Vector2 size, float fs, FontStyles style, TextAlignmentOptions al)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        t.fontSize = fs; t.fontStyle = style; t.alignment = al; t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        return t;
    }
}
