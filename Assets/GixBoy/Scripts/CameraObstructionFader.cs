using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Membuat apa pun yang menghalangi pandangan kamera ke GixBoy (gedung, atap, pohon, tiang, dll.)
// menjadi transparan, lalu kembali normal setelah tidak menghalangi.
// Bekerja tanpa collider: memeriksa bentuk mesh (OBB) setiap renderer terhadap garis kamera -> GixBoy.
[DefaultExecutionOrder(200)]
public class CameraObstructionFader : MonoBehaviour
{
    public Transform target;
    [Range(0f, 1f)] public float fadedAlpha = 0.22f;
    public float fadeSpeed = 5f;
    [Tooltip("Titik pada tubuh GixBoy yang harus terlihat (tinggi dari kaki)")]
    public float[] targetHeights = { 0.4f, 1.1f, 1.9f };
    public float rayPadding = 0.35f;
    [Tooltip("Abaikan objek sangat besar (laut, tanah)")]
    public float maxObjectSize = 150f;
    public float checkInterval = 0.05f;

    class Entry
    {
        public Renderer r;
        public Material[] original;
        public Material[] faded;
        public Color[] baseColors;
        public float alpha = 1f;
        public bool blocking;
    }

    readonly List<Renderer> candidates = new List<Renderer>();
    // renderer lain dengan parent yang sama (untuk ikut transparan bila menempel)
    readonly Dictionary<Renderer, List<Renderer>> groups = new Dictionary<Renderer, List<Renderer>>();
    readonly Dictionary<Renderer, Entry> active = new Dictionary<Renderer, Entry>();
    readonly List<Renderer> toRemove = new List<Renderer>();
    static readonly Dictionary<Material, Material> fadeCache = new Dictionary<Material, Material>();
    MaterialPropertyBlock mpb;
    float timer, rescanTimer;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { fadeCache.Clear(); }

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (target == null) { var p = GameObject.Find("Player"); if (p != null) target = p.transform; }
        Rescan();
    }

    // daftar renderer lingkungan yang boleh dibuat transparan
    void Rescan()
    {
        candidates.Clear();
        groups.Clear();
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.gameObject.layer == 5) continue; // UI
            if (target != null && r.transform.IsChildOf(target)) continue;
            if (r.GetComponentInParent<Animator>() != null) continue;            // karakter
            if (r.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue; // NPC / polisi
            if (r.GetComponentInParent<MissionMarker>() != null) continue;
            if (r.GetComponentInParent<JerrycanPickup>() != null) continue;
            if (r.GetComponentInParent<PickupTruck>() != null) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue; // teks papan nama
            var sm = r.sharedMaterial;
            if (sm == null || !sm.HasProperty("_Surface")) continue;
            var s = r.bounds.size;
            if (s.x > maxObjectSize || s.z > maxObjectSize) continue;
            if (r.GetComponent<MeshFilter>() == null || r.GetComponent<MeshFilter>().sharedMesh == null) continue;
            candidates.Add(r);
        }
        var byParent = new Dictionary<Transform, List<Renderer>>();
        foreach (var r in candidates)
        {
            var par = r.transform.parent; if (par == null) continue;
            List<Renderer> l; if (!byParent.TryGetValue(par, out l)) { l = new List<Renderer>(); byParent[par] = l; }
            l.Add(r);
        }
        foreach (var r in candidates) { List<Renderer> l; if (r.transform.parent != null && byParent.TryGetValue(r.transform.parent, out l) && l.Count > 1) groups[r] = l; }
    }

    void Mark(Renderer r)
    {
        Entry e;
        if (!active.TryGetValue(r, out e)) { e = Begin(r); if (e == null) return; active[r] = e; }
        e.blocking = true;
    }

    void LateUpdate()
    {
        if (target == null) return;
        rescanTimer += Time.unscaledDeltaTime;
        if (rescanTimer > 5f) { rescanTimer = 0f; Rescan(); }

        timer += Time.unscaledDeltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            foreach (var kv in active) kv.Value.blocking = false;
            Vector3 cam = transform.position;
            float minY = target.position.y + 0.3f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var r = candidates[i];
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                if (b.max.y < minY) continue; // tanah, jalan, trotoar
                if (b.size.y < 0.6f && b.max.y < minY + 1.2f) continue; // ubin/lantai tipis
                if (Blocks(r, b, cam))
                {
                    Mark(r);
                    // bagian kecil yang menempel pada objek ini (lampu, logo, papan) ikut transparan
                    List<Renderer> g;
                    if (groups.TryGetValue(r, out g))
                    {
                        var be = b; be.Expand(1f); float big = b.size.magnitude;
                        foreach (var gr in g)
                            if (gr != null && gr != r && gr.enabled && be.Contains(gr.bounds.center) && gr.bounds.size.magnitude <= big * 1.3f) Mark(gr);
                    }
                }
            }
        }

        float step = fadeSpeed * Time.unscaledDeltaTime;
        toRemove.Clear();
        foreach (var kv in active)
        {
            var e = kv.Value;
            if (e.r == null) { toRemove.Add(kv.Key); continue; }
            e.alpha = Mathf.MoveTowards(e.alpha, e.blocking ? fadedAlpha : 1f, step);
            if (!e.blocking && e.alpha >= 1f) { End(e); toRemove.Add(kv.Key); continue; }
            Apply(e);
        }
        foreach (var r in toRemove) active.Remove(r);
    }

    bool Blocks(Renderer r, Bounds worldBounds, Vector3 cam)
    {
        var mesh = r.GetComponent<MeshFilter>().sharedMesh;
        var tr = r.transform;
        Bounds lb = mesh.bounds;
        for (int h = 0; h < targetHeights.Length; h++)
        {
            Vector3 tp = target.position + Vector3.up * targetHeights[h];
            Vector3 d = tp - cam;
            float len = d.magnitude;
            if (len < 0.01f) continue;
            // cek cepat dengan AABB dunia
            var wb = worldBounds; wb.Expand(rayPadding * 2f);
            float dist;
            if (!wb.IntersectRay(new Ray(cam, d / len), out dist) || dist > len - 0.6f)
            {
                if (!wb.Contains(cam)) continue;
            }
            // cek teliti di ruang lokal mesh (OBB)
            Vector3 lc = tr.InverseTransformPoint(cam);
            Vector3 lt = tr.InverseTransformPoint(tp);
            Vector3 ld = lt - lc;
            float llen = ld.magnitude;
            if (llen < 0.0001f) continue;
            Vector3 sc = tr.lossyScale;
            float pad = rayPadding / Mathf.Max(0.01f, Mathf.Min(Mathf.Abs(sc.x), Mathf.Min(Mathf.Abs(sc.y), Mathf.Abs(sc.z))));
            var lbb = lb; lbb.Expand(pad * 2f);
            // GixBoy berada di dalam kotak objek (mis. di bawah atap): menghalangi kalau ada bagian di atas kepalanya
            if (lbb.Contains(lt)) { if (worldBounds.max.y > target.position.y + 2.6f && !lbb.Contains(lc)) return true; continue; }
            if (lbb.Contains(lc)) return true;
            float ld2;
            if (lbb.IntersectRay(new Ray(lc, ld / llen), out ld2) && ld2 < llen) return true;
        }
        return false;
    }

    Entry Begin(Renderer r)
    {
        var e = new Entry();
        e.r = r;
        e.original = r.sharedMaterials;
        e.faded = new Material[e.original.Length];
        e.baseColors = new Color[e.original.Length];
        for (int i = 0; i < e.original.Length; i++)
        {
            var m = e.original[i];
            if (m == null) return null;
            e.faded[i] = GetFaded(m);
            e.baseColors[i] = m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : (m.HasProperty("_Color") ? m.color : Color.white);
        }
        r.sharedMaterials = e.faded;
        return e;
    }

    void Apply(Entry e)
    {
        for (int i = 0; i < e.faded.Length; i++)
        {
            e.r.GetPropertyBlock(mpb, i);
            var c = e.baseColors[i]; c.a *= e.alpha;
            mpb.SetColor(BaseColor, c);
            e.r.SetPropertyBlock(mpb, i);
        }
    }

    void End(Entry e)
    {
        for (int i = 0; i < e.faded.Length; i++) e.r.SetPropertyBlock(null, i);
        e.r.sharedMaterials = e.original;
    }

    void OnDisable()
    {
        foreach (var kv in active) if (kv.Value.r != null) End(kv.Value);
        active.Clear();
    }

    static Material GetFaded(Material src)
    {
        Material f;
        if (fadeCache.TryGetValue(src, out f) && f != null) return f;
        if (src.HasProperty("_Surface"))
        {
            f = new Material(src);
        }
        else
        {
            // shader lain -> pakai URP Simple Lit dengan tekstur & warna yang sama
            f = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            if (src.HasProperty("_MainTex") && src.mainTexture != null) f.SetTexture("_BaseMap", src.mainTexture);
            if (src.HasProperty("_Color")) f.SetColor(BaseColor, src.color);
        }
        f.name = src.name + " (Fade)";
        f.SetFloat("_Surface", 1f);
        f.SetFloat("_Blend", 0f);
        f.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        f.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (f.HasProperty("_SrcBlendAlpha")) f.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (f.HasProperty("_DstBlendAlpha")) f.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        f.SetFloat("_ZWrite", 0f);
        f.SetFloat("_AlphaClip", 0f);
        f.SetOverrideTag("RenderType", "Transparent");
        f.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        f.DisableKeyword("_ALPHATEST_ON");
        f.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        f.renderQueue = (int)RenderQueue.Transparent;
        f.SetShaderPassEnabled("DepthOnly", false);
        f.SetShaderPassEnabled("ShadowCaster", true);
        fadeCache[src] = f;
        return f;
    }
}
