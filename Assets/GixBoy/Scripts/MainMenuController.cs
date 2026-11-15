using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu: Mulai Main, Cara Bermain, Kredit, Keluar + layar memuat.
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Nama scene kota yang dimuat saat tombol Mulai Main ditekan")]
    public string gameScene = "GixBoy_Manado";

    public GameObject mainPanel;
    public GameObject howToPanel;
    public GameObject creditsPanel;
    public GameObject loadingPanel;
    public Image loadingFill;
    public RectTransform background;
    public float backgroundZoom = 0.06f;
    public float backgroundSpeed = 0.05f;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ShowMain();
        AudioManager.PlayMusic("music_menu");
        AudioManager.Ambience(null);
        AudioManager.Siren(false);
    }

    void Update()
    {
        // gerakan pelan background (efek "Ken Burns")
        if (background != null)
        {
            float t = Time.unscaledTime * backgroundSpeed;
            float s = 1.04f + Mathf.Sin(t) * backgroundZoom * 0.5f + backgroundZoom * 0.5f;
            background.localScale = new Vector3(s, s, 1f);
            background.anchoredPosition = new Vector2(Mathf.Sin(t * 0.7f) * 25f, Mathf.Cos(t * 0.5f) * 12f);
        }
        if (Input.GetKeyDown(KeyCode.Escape) && (howToPanel.activeSelf || creditsPanel.activeSelf)) ShowMain();
        if (settings != null && settings.gameObject.activeSelf) mainPanel.SetActive(false);
    }

    public void ShowMain() { Set(mainPanel); }
    public void ShowHowTo() { Set(howToPanel); }
    public void ShowCredits() { Set(creditsPanel); }

    SettingsPanel settings;
    public void ShowSettings()
    {
        Set(null);
        if (settings == null) settings = SettingsPanel.Create(mainPanel.transform.parent, ShowMain);
        settings.gameObject.SetActive(true);
        settings.transform.SetAsLastSibling();
    }

    void Set(GameObject active)
    {
        mainPanel.SetActive(active == mainPanel);
        howToPanel.SetActive(active == howToPanel);
        creditsPanel.SetActive(active == creditsPanel);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (active != null && settings != null) settings.gameObject.SetActive(false);
    }

    public void PlayGame() { StartCoroutine(Load()); }

    IEnumerator Load()
    {
        mainPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);
        var op = SceneManager.LoadSceneAsync(gameScene);
        if (op == null) yield break;
        while (!op.isDone)
        {
            if (loadingFill != null) loadingFill.fillAmount = Mathf.Clamp01(op.progress / 0.9f);
            yield return null;
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
