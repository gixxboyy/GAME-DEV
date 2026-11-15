#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

// Saat menekan Play di editor, game selalu dimulai dari Main Menu (seperti game pada umumnya):
// Main Menu -> MULAI MAIN -> cutscene pembuka -> bermain.
// Bisa dimatikan lewat menu GixBoy/Play Mulai dari Main Menu.
[InitializeOnLoad]
public static class GixBoyPlayFromMenu
{
    const string MenuPath = "GixBoy/Play Mulai dari Main Menu";
    const string PrefKey = "GixBoy_PlayFromMenu";
    const string ScenePath = "Assets/GixBoy/Scenes/MainMenu.unity";

    static GixBoyPlayFromMenu()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        EditorApplication.delayCall += Apply;
    }

    static bool Enabled { get { return EditorPrefs.GetBool(PrefKey, true); } }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !Enabled);
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }
}
#endif
