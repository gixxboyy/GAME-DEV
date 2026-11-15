using UnityEngine;

// Pengaturan game yang disimpan di PlayerPrefs (volume, grafik, layar penuh, FPS).
public static class GameSettings
{
    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.7f;
    public static float SfxVolume { get; private set; } = 0.9f;
    public static int Quality { get; private set; }
    public static bool Fullscreen { get; private set; } = true;
    public static bool ShowFps { get; private set; }

    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat("gix_master", 1f);
        MusicVolume = PlayerPrefs.GetFloat("gix_music", 0.7f);
        SfxVolume = PlayerPrefs.GetFloat("gix_sfx", 0.9f);
        Quality = PlayerPrefs.GetInt("gix_quality", QualitySettings.GetQualityLevel());
        Fullscreen = PlayerPrefs.GetInt("gix_fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        ShowFps = PlayerPrefs.GetInt("gix_fps", 0) == 1;
        Apply();
    }

    public static void SetMaster(float v) { MasterVolume = Mathf.Clamp01(v); Save(); }
    public static void SetMusic(float v) { MusicVolume = Mathf.Clamp01(v); Save(); }
    public static void SetSfx(float v) { SfxVolume = Mathf.Clamp01(v); Save(); }
    public static void SetQuality(int q) { Quality = Mathf.Clamp(q, 0, QualitySettings.names.Length - 1); Save(); }
    public static void SetFullscreen(bool f) { Fullscreen = f; Save(); }
    public static void SetShowFps(bool f) { ShowFps = f; Save(); }

    public static string QualityName
    {
        get
        {
            var n = QualitySettings.names;
            return n.Length == 0 ? "-" : n[Mathf.Clamp(Quality, 0, n.Length - 1)];
        }
    }

    static void Save()
    {
        PlayerPrefs.SetFloat("gix_master", MasterVolume);
        PlayerPrefs.SetFloat("gix_music", MusicVolume);
        PlayerPrefs.SetFloat("gix_sfx", SfxVolume);
        PlayerPrefs.SetInt("gix_quality", Quality);
        PlayerPrefs.SetInt("gix_fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("gix_fps", ShowFps ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    static void Apply()
    {
        AudioListener.volume = MasterVolume;
        if (QualitySettings.names.Length > 0 && QualitySettings.GetQualityLevel() != Quality)
            QualitySettings.SetQualityLevel(Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
        if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        if (Changed != null) Changed();
    }
}
