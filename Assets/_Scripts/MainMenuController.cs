using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

[System.Serializable]
public struct TrackData
{
    public Sprite    trackSprite;
    public string    trackName;
    public AudioClip audioClip;
}

/// <summary>Одна запись в панели Титров — имя трека/артиста + ссылка.</summary>
[System.Serializable]
public struct CreditsEntry
{
    [Tooltip("Например: return — Øneheart")]
    public string displayName;
    [Tooltip("Ссылка на Spotify ")]
    public string url;
}

[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(AudioSource))]
public class MainMenuController : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────

    [Header("Track Player (Canvas)")]
    [SerializeField] private GameObject           trackPlayerCanvas;
    [SerializeField] private RectTransform        playerPanelRect;
    [SerializeField] private UnityEngine.UI.Image trackImage;
    [SerializeField] private TextMeshProUGUI      trackNameText;

    // ВАЖНО: promptText — прямой потомок trackPlayerCanvas, НЕ внутри playerPanelRect
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private AudioSource     audioSource;
    [SerializeField] private TrackData[]     tracks;

    [Header("Animation & Timer")]
    [SerializeField] private float hiddenYOffset   = 300f;
    [SerializeField] private float animDuration    = 0.5f;
    [SerializeField] private float moveOutDuration = 0.4f;
    [SerializeField] private float hideDelay       = 10f;

    [Header("Credits")]
    [SerializeField] private CreditsEntry[] creditsEntries;

    // ─── Приватные поля ───────────────────────────────────────────

    private VisualElement _root;
    private bool          _settingsOpen;
    private bool          _creditsOpen;

    private int   _currentTrackIndex;
    private bool  _isPlayerActive;
    private bool  _isTrackPlaying;
    private bool  _isPanelVisible;
    private float _hideTimer;
    private float _initialPanelPosY;

    // Флаг «меню сейчас скрыто»
    private bool _menuIdle;

    // Кэш устройств — не дёргаем .current каждый кадр
    private Keyboard _keyboard;
    private Mouse    _mouse;

    // UI Toolkit элементы
    private Button        _btnStart;
    private Button        _btnSettings;
    private Button        _btnSettingsClose;
    private Button        _btnCredits;
    private Button        _btnCreditsClose;
    private Button        _btnQuit;

    private VisualElement _settingsPanel;
    private VisualElement _creditsPanel;
    private VisualElement _creditsItemsContainer;
    private VisualElement _menuContainer;

    private Slider        _sliderMusic;
    private Slider        _sliderSfx;
    private Label         _lblMusicVal;
    private Label         _lblSfxVal;
    private DropdownField _dropdownResolution;

    // ─── Unity Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        if (playerPanelRect != null)
            _initialPanelPosY = playerPanelRect.anchoredPosition.y;

        _keyboard = Keyboard.current;
        _mouse    = Mouse.current;
    }

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;

        BindUIElements();
        RegisterCallbacks();
        BuildCreditsItems();

        CloseSettings();
        CloseCredits();

        if (trackPlayerCanvas != null)
            trackPlayerCanvas.SetActive(false);
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void OnDestroy()
    {
        playerPanelRect?.DOKill();
    }

    private void Update()
    {
        if (!_isPlayerActive) return;

        HandlePromptBlink();
        HandlePlayerInput();
        HandleAutoHideTimer();
    }

    // ─── UI Binding ───────────────────────────────────────────────

    private void BindUIElements()
    {
        _menuContainer         = Q<VisualElement>("menu-container");
        _btnStart              = Q<Button>("btn-start");
        _btnSettings           = Q<Button>("btn-settings");
        _btnSettingsClose      = Q<Button>("btn-settings-close");
        _btnCredits            = Q<Button>("btn-credits");
        _btnCreditsClose       = Q<Button>("btn-credits-close");
        _btnQuit               = Q<Button>("btn-quit");
        _settingsPanel         = Q<VisualElement>("settings-panel");
        _creditsPanel          = Q<VisualElement>("credits-panel");
        _creditsItemsContainer = Q<VisualElement>("credits-items-container");
        _sliderMusic           = Q<Slider>("slider-music");
        _sliderSfx             = Q<Slider>("slider-sfx");
        _lblMusicVal           = Q<Label>("lbl-music-val");
        _lblSfxVal             = Q<Label>("lbl-sfx-val");
        _dropdownResolution    = Q<DropdownField>("dropdown-resolution");
    }

    private void RegisterCallbacks()
    {
        if (_btnStart        != null) _btnStart.clicked        += OnStartClicked;
        if (_btnSettings     != null) _btnSettings.clicked     += OnSettingsClicked;
        if (_btnSettingsClose != null) _btnSettingsClose.clicked += CloseSettings;
        if (_btnCredits      != null) _btnCredits.clicked      += OnCreditsClicked;
        if (_btnCreditsClose != null) _btnCreditsClose.clicked += CloseCredits;
        if (_btnQuit         != null) _btnQuit.clicked         += OnQuitClicked;

        if (_sliderMusic != null)
        {
            _sliderMusic.RegisterValueChangedCallback(OnMusicVolumeChanged);
            SyncVolume(_sliderMusic.value);
        }
        if (_sliderSfx != null)
            _sliderSfx.RegisterValueChangedCallback(OnSfxVolumeChanged);
        if (_dropdownResolution != null)
            _dropdownResolution.RegisterValueChangedCallback(e => ApplyResolution(e.newValue));

        // Любое движение мыши по UI сбрасывает таймер бездействия
        _root?.RegisterCallback<MouseMoveEvent>(OnRootMouseMove);

        // Клики по кнопкам меню тоже сбрасывают таймер
        _root?.RegisterCallback<ClickEvent>(OnRootClick);
    }

    private void UnregisterCallbacks()
    {
        if (_btnStart        != null) _btnStart.clicked        -= OnStartClicked;
        if (_btnSettings     != null) _btnSettings.clicked     -= OnSettingsClicked;
        if (_btnSettingsClose != null) _btnSettingsClose.clicked -= CloseSettings;
        if (_btnCredits      != null) _btnCredits.clicked      -= OnCreditsClicked;
        if (_btnCreditsClose != null) _btnCreditsClose.clicked -= CloseCredits;
        if (_btnQuit         != null) _btnQuit.clicked         -= OnQuitClicked;

        _sliderMusic?.UnregisterValueChangedCallback(OnMusicVolumeChanged);
        _sliderSfx?.UnregisterValueChangedCallback(OnSfxVolumeChanged);

        _root?.UnregisterCallback<MouseMoveEvent>(OnRootMouseMove);
        _root?.UnregisterCallback<ClickEvent>(OnRootClick);
    }

    // ─── Credits: динамическое построение ссылок ─────────────────

    /// <summary>
    /// Генерирует Label-ссылки из Inspector-массива creditsEntries.
    /// Каждый Label: при наведении меняет цвет (через USS .credits-link:hover),
    /// при клике открывает браузер.
    /// </summary>
    private void BuildCreditsItems()
    {
        if (_creditsItemsContainer == null || creditsEntries == null) return;

        _creditsItemsContainer.Clear();

        for (int i = 0; i < creditsEntries.Length; i++)
        {
            var entry = creditsEntries[i];
            var label = new Label($"{i + 1}.  {entry.displayName}");
            label.AddToClassList("credits-link");

            // Захватываем url локально для корректного замыкания
            string url = entry.url;
            label.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));

            _creditsItemsContainer.Add(label);
        }
    }

    // ─── Update: разбит на читаемые методы ───────────────────────

    private void HandlePromptBlink()
    {
        if (_isTrackPlaying || promptText == null) return;

        float alpha = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
        var c = promptText.color;
        promptText.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void HandlePlayerInput()
    {
        if (_keyboard == null) return;

        // Enter → запуск трека
        if (!_isTrackPlaying && _keyboard.enterKey.wasPressedThisFrame)
        {
            StartTrackPlayback();
            return;
        }

        if (!_isTrackPlaying) return;

        // Автопереключение по окончанию клипа
        if (!audioSource.isPlaying && audioSource.clip != null && audioSource.time == 0f)
        {
            NextTrack();
            return;
        }

        // Ручное переключение
        if (_keyboard.nKey.wasPressedThisFrame)
        {
            NextTrack();
            return;
        }

        // Любой ввод → восстановить всё
        bool anyInput = _keyboard.anyKey.wasPressedThisFrame ||
                        (_mouse != null && _mouse.leftButton.wasPressedThisFrame) ||
                        (_mouse != null && _mouse.delta.ReadValue() != Vector2.zero);

        if (anyInput)
        {
            ResetHideTimer();
            if (!_isPanelVisible) AnimatePanelIn();
            if (_menuIdle) ShowMenuUI();
        }
    }

    private void HandleAutoHideTimer()
    {
        if (!_isTrackPlaying || !_isPanelVisible) return;

        _hideTimer -= Time.unscaledDeltaTime;
        if (_hideTimer <= 0f)
            HideAll(); // скрываем и канвас-панель, и меню
    }

    // ─── UI Event Callbacks ───────────────────────────────────────

    private void OnRootMouseMove(MouseMoveEvent _)
    {
        if (!_isTrackPlaying) return;
        ResetHideTimer();
        if (_menuIdle) ShowMenuUI();
    }

    private void OnRootClick(ClickEvent _)
    {
        if (!_isTrackPlaying) return;
        ResetHideTimer();
        if (_menuIdle) ShowMenuUI();
    }

    // ─── Start / Playback ────────────────────────────────────────

    private void OnStartClicked()
    {
        if (tracks == null || tracks.Length == 0)
        {
            Debug.LogWarning("[MainMenu] Нет треков в массиве tracks!");
            return;
        }

        trackPlayerCanvas.SetActive(true);

        _isPlayerActive    = true;
        _isTrackPlaying    = false;
        _isPanelVisible    = false;
        _currentTrackIndex = 0;

        // FIX: сначала выключаем playerPanelRect, потом включаем promptText.
        // promptText ОБЯЗАН быть прямым потомком trackPlayerCanvas, НЕ внутри playerPanelRect.
        if (playerPanelRect != null)
            playerPanelRect.gameObject.SetActive(false);

        if (promptText != null)
            promptText.gameObject.SetActive(true);

        UpdateTrackUI(_currentTrackIndex);
        ResetHideTimer();
    }

    private void StartTrackPlayback()
    {
        _isTrackPlaying = true;

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (playerPanelRect != null)
        {
            playerPanelRect.anchoredPosition = new Vector2(
                playerPanelRect.anchoredPosition.x,
                _initialPanelPosY + hiddenYOffset
            );
            playerPanelRect.gameObject.SetActive(true);
        }

        PlayTrack(_currentTrackIndex);
        AnimatePanelIn();
    }

    private void PlayTrack(int index)
    {
        if (audioSource == null || tracks.Length == 0) return;

        _currentTrackIndex = index;
        UpdateTrackUI(index);
        audioSource.clip = tracks[index].audioClip;
        audioSource.Play();
    }

    private void NextTrack()
    {
        int next = (_currentTrackIndex + 1) % tracks.Length;
        PlayTrack(next);

        ResetHideTimer();
        if (!_isPanelVisible) AnimatePanelIn();
        if (_menuIdle) ShowMenuUI();
    }

    private void UpdateTrackUI(int index)
    {
        if (trackImage    != null) trackImage.sprite  = tracks[index].trackSprite;
        if (trackNameText != null) trackNameText.text = tracks[index].trackName;
    }

    // ─── DOTween ─────────────────────────────────────────────────

    private void AnimatePanelIn()
    {
        if (playerPanelRect == null) return;

        _isPanelVisible = true;
        ResetHideTimer();

        playerPanelRect.DOKill();
        playerPanelRect
            .DOAnchorPosY(_initialPanelPosY, animDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private void AnimatePanelOut()
    {
        if (playerPanelRect == null) return;

        _isPanelVisible = false;

        playerPanelRect.DOKill();
        playerPanelRect
            .DOAnchorPosY(_initialPanelPosY + hiddenYOffset, moveOutDuration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true);
    }

    /// <summary>Скрывает и Canvas-панель трека, и меню одновременно.</summary>
    private void HideAll()
    {
        AnimatePanelOut();
        HideMenuUI();
    }

    // ─── Menu idle visibility ────────────────────────────────────

    private void ShowMenuUI()
    {
        if (_menuContainer == null) return;

        _menuIdle = false;
        _menuContainer.RemoveFromClassList("menu-idle");
        _menuContainer.pickingMode = PickingMode.Position;
    }

    private void HideMenuUI()
    {
        if (_menuContainer == null) return;

        _menuIdle = true;
        _menuContainer.AddToClassList("menu-idle");

        // Отключаем hit-testing, пока меню прозрачно
        _menuContainer.pickingMode = PickingMode.Ignore;
    }

    private void ResetHideTimer() => _hideTimer = hideDelay;

    // ─── Settings ────────────────────────────────────────────────

    private void OnSettingsClicked()
    {
        if (_settingsOpen) CloseSettings();
        else               OpenSettings();
    }

    private void OpenSettings()
    {
        _settingsOpen = true;
        if (_creditsOpen) CloseCredits(); // взаимоисключение с Титрами
        _settingsPanel?.RemoveFromClassList("hidden");
        _btnSettings?.AddToClassList("open");
    }

    private void CloseSettings()
    {
        _settingsOpen = false;
        _settingsPanel?.AddToClassList("hidden");
        _btnSettings?.RemoveFromClassList("open");
    }

    // ─── Credits ─────────────────────────────────────────────────

    private void OnCreditsClicked()
    {
        if (_creditsOpen) CloseCredits();
        else              OpenCredits();
    }

    private void OpenCredits()
    {
        _creditsOpen = true;
        if (_settingsOpen) CloseSettings(); // взаимоисключение с Настройками
        _creditsPanel?.RemoveFromClassList("hidden");
        _btnCredits?.AddToClassList("open");
    }

    private void CloseCredits()
    {
        _creditsOpen = false;
        _creditsPanel?.AddToClassList("hidden");
        _btnCredits?.RemoveFromClassList("open");
    }

    // ─── Volume ──────────────────────────────────────────────────

    private void OnMusicVolumeChanged(ChangeEvent<float> e)
    {
        if (_lblMusicVal != null)
            _lblMusicVal.text = Mathf.RoundToInt(e.newValue).ToString();
        SyncVolume(e.newValue);
    }

    private void OnSfxVolumeChanged(ChangeEvent<float> e)
    {
        if (_lblSfxVal != null)
            _lblSfxVal.text = Mathf.RoundToInt(e.newValue).ToString();
    }

    private void SyncVolume(float sliderValue)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(sliderValue / 100f);
    }

    // ─── Resolution ──────────────────────────────────────────────

    private void ApplyResolution(string value)
    {
        char[] sep = { '×', 'x', 'X' };
        string[] parts = value.Split(sep, System.StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2) return;

        if (int.TryParse(parts[0].Trim(), out int w) &&
            int.TryParse(parts[1].Trim(), out int h))
        {
            Screen.SetResolution(w, h, Screen.fullScreen);
        }
        else
        {
            Debug.LogWarning($"[MainMenu] Не удалось распарсить разрешение: '{value}'");
        }
    }

    // ─── Other ───────────────────────────────────────────────────

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Типобезопасный Q&lt;T&gt; с предупреждением при отсутствии элемента.</summary>
    private T Q<T>(string name) where T : VisualElement
    {
        var el = _root.Q<T>(name);
        if (el == null)
            Debug.LogWarning($"[MainMenu] UI-элемент '{name}' не найден в UXML.");
        return el;
    }
}