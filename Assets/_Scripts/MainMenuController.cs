using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Контроллер главного меню.
/// Прикрепить на GameObject с UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    // ─── Ссылки на сцену (опционально) ───────────────────────────
    [Header("Scene References")]
    [SerializeField] private string firstSceneName = "Game";

    // ─── Внутренние переменные ────────────────────────────────────
    private VisualElement _root;
    private bool          _settingsOpen = false;

    // кнопки
    private Button _btnStart;
    private Button _btnSettings;
    private Button _btnSettingsClose;
    private Button _btnCredits;
    private Button _btnQuit;

    // панель настроек
    private VisualElement _settingsPanel;

    // слайдеры + подписи
    private Slider _sliderMusic;
    private Slider _sliderSfx;
    private Label  _lblMusicVal;
    private Label  _lblSfxVal;

    // dropdown
    private DropdownField _dropdownResolution;

    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;

        // Находим элементы
        _btnStart          = _root.Q<Button>("btn-start");
        _btnSettings       = _root.Q<Button>("btn-settings");
        _btnSettingsClose  = _root.Q<Button>("btn-settings-close");
        _btnCredits        = _root.Q<Button>("btn-credits");
        _btnQuit           = _root.Q<Button>("btn-quit");

        _settingsPanel     = _root.Q<VisualElement>("settings-panel");

        _sliderMusic       = _root.Q<Slider>("slider-music");
        _sliderSfx         = _root.Q<Slider>("slider-sfx");
        _lblMusicVal       = _root.Q<Label>("lbl-music-val");
        _lblSfxVal         = _root.Q<Label>("lbl-sfx-val");

        _dropdownResolution = _root.Q<DropdownField>("dropdown-resolution");

        // Назначаем колбэки
        _btnStart.clicked         += OnStartClicked;
        _btnSettings.clicked      += OnSettingsClicked;
        _btnSettingsClose.clicked += CloseSettings;
        _btnCredits.clicked       += OnCreditsClicked;
        _btnQuit.clicked          += OnQuitClicked;

        // Слайдеры — обновляем подпись в реальном времени
        _sliderMusic.RegisterValueChangedCallback(e =>
            _lblMusicVal.text = Mathf.RoundToInt(e.newValue).ToString());

        _sliderSfx.RegisterValueChangedCallback(e =>
            _lblSfxVal.text = Mathf.RoundToInt(e.newValue).ToString());

        // Dropdown — применяем разрешение
        _dropdownResolution.RegisterValueChangedCallback(e =>
            ApplyResolution(e.newValue));

        // Убедимся что панель скрыта при старте
        CloseSettings();
    }

    private void OnDisable()
    {
        _btnStart.clicked         -= OnStartClicked;
        _btnSettings.clicked      -= OnSettingsClicked;
        _btnSettingsClose.clicked -= CloseSettings;
        _btnCredits.clicked       -= OnCreditsClicked;
        _btnQuit.clicked          -= OnQuitClicked;
    }

    // ─── Кнопки ───────────────────────────────────────────────────

    private void OnStartClicked()
    {
        // Плавное начало игры — добавь свою логику переходов
        UnityEngine.SceneManagement.SceneManager.LoadScene(firstSceneName);
    }

    private void OnSettingsClicked()
    {
        if (_settingsOpen) CloseSettings();
        else               OpenSettings();
    }

    private void OpenSettings()
    {
        _settingsOpen = true;
        _settingsPanel.RemoveFromClassList("hidden");
        _btnSettings.AddToClassList("open");     // меняет стиль кнопки (см. USS .open)
    }

    private void CloseSettings()
    {
        _settingsOpen = false;
        _settingsPanel.AddToClassList("hidden");
        _btnSettings.RemoveFromClassList("open");
    }

    private void OnCreditsClicked()
    {
        Debug.Log("[MainMenu] Открыть титры");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Credits");
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Разрешение ──────────────────────────────────────────────

    private void ApplyResolution(string value)
    {
        // Парсим строку вида "1920×1080"
        // Символ × (U+00D7) — именно такой используется в UXML
        string[] parts = value.Split('×');
        if (parts.Length != 2) return;

        if (int.TryParse(parts[0].Trim(), out int w) &&
            int.TryParse(parts[1].Trim(), out int h))
        {
            Screen.SetResolution(w, h, Screen.fullScreen);
            Debug.Log($"[MainMenu] Разрешение: {w}×{h}");
        }
    }
}
