using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TimeOfDay { Pagi, Siang, Malam }

// Mengatur suasana pagi / siang / malam: arah & warna matahari, ambient, langit,
// dan lampu jalan + lampu kanopi pom bensin yang menyala di malam hari.
public class DayNightController : MonoBehaviour
{
    public static DayNightController Instance { get; private set; }

    public Light sun;
    public TimeOfDay current = TimeOfDay.Pagi;
    public float transitionTime = 2.5f;

    [System.Serializable]
    public class Preset
    {
        public Vector2 sunAngles;      // x = ketinggian, y = arah
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public Color ambientSky, ambientEquator, ambientGround;
        public float skyExposure = 1f;
        public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
        public Color fogColor = Color.gray;
    }

    public Preset pagi, siang, malam;

    readonly List<Light> nightLights = new List<Light>();
    Material skyMat;
    Coroutine running;

    void Reset() { SetDefaults(); }

    public void SetDefaults()
    {
        pagi = new Preset { sunAngles = new Vector2(22f, 100f), sunColor = new Color(1f, 0.82f, 0.62f), sunIntensity = 1.0f,
            ambientSky = Hex("#4B4E5E"), ambientEquator = Hex("#4A4036"), ambientGround = Hex("#221E19"), skyExposure = 1.0f, skyTint = Hex("#C7A27F"), fogColor = Hex("#E9C9A6") };
        siang = new Preset { sunAngles = new Vector2(58f, 330f), sunColor = new Color(1f, 0.98f, 0.94f), sunIntensity = 1.1f,
            ambientSky = Hex("#3A4252"), ambientEquator = Hex("#34312B"), ambientGround = Hex("#1E1C18"), skyExposure = 1.3f, skyTint = Hex("#808080"), fogColor = Hex("#C8DDF0") };
        malam = new Preset { sunAngles = new Vector2(35f, 210f), sunColor = new Color(0.45f, 0.55f, 0.9f), sunIntensity = 0.22f,
            ambientSky = Hex("#151B30"), ambientEquator = Hex("#10131E"), ambientGround = Hex("#08090C"), skyExposure = 0.12f, skyTint = Hex("#2A3560"), fogColor = Hex("#0E1424") };
    }

    static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    void Awake()
    {
        Instance = this;
        if (pagi == null || pagi.sunIntensity == 0f) SetDefaults();
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (RenderSettings.skybox != null) { skyMat = new Material(RenderSettings.skybox); RenderSettings.skybox = skyMat; }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        BuildNightLights();
        ApplyInstant(current);
    }

    void BuildNightLights()
    {
        var warm = new Color(1f, 0.78f, 0.45f);
        foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            string n = mf.sharedMesh.name.ToLower();
            var r = mf.GetComponent<Renderer>(); if (r == null) continue;
            if (n.Contains("lamp") && !n.Contains("park"))
            {
                var b = r.bounds;
                AddLight(new Vector3(b.center.x, b.max.y - 0.8f, b.center.z), 24f, 9f, warm);
            }
            else if (mf.name == "Canopy_Underside")
            {
                AddLight(r.bounds.center + Vector3.down * 0.8f, 26f, 12f, new Color(0.95f, 0.97f, 1f));
            }
        }
    }

    void AddLight(Vector3 pos, float range, float intensity, Color c)
    {
        var go = new GameObject("LampuMalam");
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c;
        l.shadows = LightShadows.None;
        l.enabled = false;
        nightLights.Add(l);
    }

    Preset Get(TimeOfDay t) { return t == TimeOfDay.Pagi ? pagi : t == TimeOfDay.Siang ? siang : malam; }

    public void Apply(TimeOfDay t, bool instant = false)
    {
        current = t;
        if (instant || !Application.isPlaying) { ApplyInstant(t); return; }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Blend(t));
    }

    void ApplyInstant(TimeOfDay t)
    {
        if (Application.isPlaying) AudioManager.Ambience(t == TimeOfDay.Malam ? "amb_night" : "amb_day");
        var p = Get(t);
        Set(p, p, 1f);
        foreach (var l in nightLights) if (l != null) l.enabled = t == TimeOfDay.Malam;
    }

    IEnumerator Blend(TimeOfDay t)
    {
        var from = Capture();
        var to = Get(t);
        float k = 0f;
        if (t == TimeOfDay.Malam) foreach (var l in nightLights) if (l != null) l.enabled = true;
        AudioManager.Ambience(t == TimeOfDay.Malam ? "amb_night" : "amb_day");
        while (k < 1f)
        {
            k += Time.deltaTime / transitionTime;
            Set(from, to, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        Set(to, to, 1f);
        foreach (var l in nightLights) if (l != null) l.enabled = t == TimeOfDay.Malam;
        running = null;
    }

    Preset Capture()
    {
        var p = new Preset();
        if (sun != null)
        {
            var e = sun.transform.eulerAngles; p.sunAngles = new Vector2(e.x, e.y);
            p.sunColor = sun.color; p.sunIntensity = sun.intensity;
        }
        p.ambientSky = RenderSettings.ambientSkyColor; p.ambientEquator = RenderSettings.ambientEquatorColor; p.ambientGround = RenderSettings.ambientGroundColor;
        p.skyExposure = skyMat != null && skyMat.HasProperty("_Exposure") ? skyMat.GetFloat("_Exposure") : 1f;
        p.skyTint = skyMat != null && skyMat.HasProperty("_SkyTint") ? skyMat.GetColor("_SkyTint") : Color.gray;
        p.fogColor = RenderSettings.fogColor;
        return p;
    }

    void Set(Preset a, Preset b, float k)
    {
        if (sun != null)
        {
            float x = Mathf.LerpAngle(a.sunAngles.x, b.sunAngles.x, k);
            float y = Mathf.LerpAngle(a.sunAngles.y, b.sunAngles.y, k);
            sun.transform.rotation = Quaternion.Euler(x, y, 0f);
            sun.color = Color.Lerp(a.sunColor, b.sunColor, k);
            sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, k);
            Shader.SetGlobalVector("_GixSunDir", -sun.transform.forward);
        }
        float exposure = Mathf.Lerp(a.skyExposure, b.skyExposure, k);
        Shader.SetGlobalFloat("_GixNight", 1f - Mathf.Clamp01((exposure - 0.12f) / 0.6f));
        RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, k);
        RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, k);
        RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, k);
        RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, k);
        if (skyMat != null)
        {
            if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", Mathf.Lerp(a.skyExposure, b.skyExposure, k));
            if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", Color.Lerp(a.skyTint, b.skyTint, k));
        }
        var cam = Camera.main;
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
            cam.backgroundColor = Color.Lerp(a.fogColor, b.fogColor, k);
    }
}
