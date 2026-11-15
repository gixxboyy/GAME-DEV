using System.Collections.Generic;
using UnityEngine;

// Data GixBoy: health, uang, jeriken yang sedang dibawa, total bensin terkumpul.
public class GixBoyStats : MonoBehaviour
{
    public static GixBoyStats Instance { get; private set; }

    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;

    [Header("Ekonomi & Bensin")]
    public int money = 0;
    public int carried = 0;          // jeriken yang sedang dibawa
    public int deliveredCans = 0;    // total jeriken yang sudah diserahkan
    public int litersPerCan = 20;

    readonly List<GameObject> backCans = new List<GameObject>();
    Transform carryAnchor;

    public int DeliveredLiters => deliveredCans * litersPerCan;
    public int CarriedLiters => carried * litersPerCan;

    void Awake()
    {
        Instance = this;
        health = maxHealth;
    }

    void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim != null && anim.isHuman) carryAnchor = anim.GetBoneTransform(HumanBodyBones.Chest);
        if (carryAnchor == null) carryAnchor = transform;
        RefreshBack();
    }

    public void AddCans(int n) { carried += n; RefreshBack(); }

    public int TakeCans(int n)
    {
        int t = Mathf.Min(n, carried);
        carried -= t;
        RefreshBack();
        return t;
    }

    public void ClearCans() { carried = 0; RefreshBack(); }

    public void Damage(float d)
    {
        if (health <= 0f) return;
        health = Mathf.Max(0f, health - d);
        if (health <= 0f && MissionManager.Instance != null) MissionManager.Instance.OnPlayerCaught();
    }

    public void Heal(float d) { health = Mathf.Min(maxHealth, health + d); }

    // jeriken kecil menempel di punggung GixBoy (maks 3 yang terlihat)
    void RefreshBack()
    {
        if (carryAnchor == null) return;
        int show = Mathf.Min(carried, 3);
        while (backCans.Count < show)
        {
            int i = backCans.Count;
            var g = JerrycanVisual.Create(carryAnchor, 0.4f);
            g.name = "Jeriken_Punggung_" + i;
            g.transform.localPosition = new Vector3((i - 1) * 0.3f, -0.12f, -0.27f);
            g.transform.localRotation = Quaternion.identity;
            backCans.Add(g);
        }
        while (backCans.Count > show)
        {
            var g = backCans[backCans.Count - 1];
            backCans.RemoveAt(backCans.Count - 1);
            if (g != null) Destroy(g);
        }
    }
}

// Membuat model jeriken sederhana dari primitive (tanpa collider).
public static class JerrycanVisual
{
    static Material red, dark;

    static Material Mat(ref Material m, Color c)
    {
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh != null ? sh : Shader.Find("Standard"));
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", 0.45f);
        }
        return m;
    }

    public static GameObject Create(Transform parent, float scale)
    {
        var root = new GameObject("Jeriken");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * scale;
        var r = Mat(ref red, new Color(0.82f, 0.12f, 0.1f));
        var d = Mat(ref dark, new Color(0.12f, 0.12f, 0.13f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 1.0f, 0.38f), r);
        Part(root.transform, PrimitiveType.Cube, new Vector3(-0.12f, 1.14f, 0f), new Vector3(0.42f, 0.1f, 0.12f), r);
        Part(root.transform, PrimitiveType.Cube, new Vector3(-0.3f, 1.08f, 0f), new Vector3(0.08f, 0.12f, 0.12f), r);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0.06f, 1.08f, 0f), new Vector3(0.08f, 0.12f, 0.12f), r);
        var spout = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.28f, 1.15f, 0f), new Vector3(0.12f, 0.12f, 0.12f), d);
        spout.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
        var x1 = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.2f), new Vector3(0.9f, 0.06f, 0.02f), d);
        x1.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var x2 = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.2f), new Vector3(0.9f, 0.06f, 0.02f), d);
        x2.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        return root;
    }

    static GameObject Part(Transform p, PrimitiveType t, Vector3 pos, Vector3 s, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }
}
