using UnityEngine;

// Penanda tujuan misi: berlian melayang + sorot cahaya tinggi supaya terlihat dari jauh.
public class MissionMarker : MonoBehaviour
{
    public Color color = new Color(1f, 0.78f, 0.15f);
    public float height = 4.5f;
    Transform diamond, beam;
    Vector3 target;
    bool hasTarget;

    void Start()
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
        m.SetColor("_BaseColor", color); m.color = color;

        var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(d.GetComponent<Collider>());
        d.name = "Berlian"; d.transform.SetParent(transform, false);
        d.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        d.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        d.GetComponent<Renderer>().sharedMaterial = m;
        diamond = d.transform;

        var mb = new Material(m);
        var c2 = color; c2.a = 0.25f;
        mb.SetColor("_BaseColor", c2);
        // transparan
        mb.SetFloat("_Surface", 1f); mb.SetFloat("_Blend", 0f);
        mb.SetOverrideTag("RenderType", "Transparent");
        mb.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mb.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mb.SetInt("_ZWrite", 0);
        mb.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mb.renderQueue = 3000;
        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(b.GetComponent<Collider>());
        b.name = "Sorot"; b.transform.SetParent(transform, false);
        b.transform.localScale = new Vector3(0.6f, 25f, 0.6f);
        b.transform.localPosition = new Vector3(0f, 25f, 0f);
        b.GetComponent<Renderer>().sharedMaterial = mb;
        b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam = b.transform;
        gameObject.SetActive(hasTarget);
    }

    public void SetTarget(Vector3 pos)
    {
        target = pos; hasTarget = true;
        transform.position = pos;
        gameObject.SetActive(true);
    }

    public void Hide() { hasTarget = false; gameObject.SetActive(false); }

    void Update()
    {
        if (diamond == null) return;
        diamond.localPosition = new Vector3(0f, height + Mathf.Sin(Time.time * 2f) * 0.4f, 0f);
        diamond.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        bool hide = DialogueManager.IsPlaying || GixBoyCutscene.IsPlaying;
        diamond.gameObject.SetActive(!hide);
        beam.gameObject.SetActive(!hide);
    }
}
