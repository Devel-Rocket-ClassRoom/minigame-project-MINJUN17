using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 공통 설정 패널. StartScene / 인게임 양쪽에서 같은 프리팹을 쓰되,
/// 맨 밑 버튼 동작만 인스펙터에서 다르게 지정한다.
/// - GoHome  : "홈으로"  → StartScene 로드 (인게임에서 사용)
/// - QuitGame: "게임종료" → 앱 종료      (StartScene에서 사용)
///
/// 소리(BGM/SFX 음소거)는 자식의 SoundToggleControl이 각자 처리.
/// 언어는 Unity Localization 로케일 전환.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public enum BottomAction { GoHome, QuitGame }

    [Header("맨 밑 버튼")]
    [SerializeField] private BottomAction bottomAction = BottomAction.GoHome;
    [SerializeField] private Button bottomButton;
    [SerializeField] private TextMeshProUGUI bottomButtonLabel;
    [SerializeField] private string goHomeText = "홈으로";
    [SerializeField] private string quitText   = "게임종료";
    [Tooltip("GoHome 시 로드할 씬 이름")]
    [SerializeField] private string homeSceneName = "StartScene";

    [Header("언어 드롭다운")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    [Tooltip("AvailableLocales로 옵션을 자동 채움(네이티브 이름). 끄면 수동 옵션 사용.")]
    [SerializeField] private bool autoPopulateLanguages = true;

    [Header("열기/닫기 (선택 — 없으면 외부에서 제어)")]
    [SerializeField] private GameObject panelRoot;   // 패널 본체 (없으면 this.gameObject)
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    private List<Locale> _locales;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        // 맨 밑 버튼: 모드에 따라 텍스트 + 동작
        if (bottomButtonLabel != null)
            bottomButtonLabel.text = bottomAction == BottomAction.GoHome ? goHomeText : quitText;
        if (bottomButton != null)
            bottomButton.onClick.AddListener(OnBottomClicked);

        if (openButton  != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        SetupLanguageDropdown();
    }

    private void OnDestroy()
    {
        if (bottomButton != null) bottomButton.onClick.RemoveListener(OnBottomClicked);
        if (openButton   != null) openButton.onClick.RemoveListener(Open);
        if (closeButton  != null) closeButton.onClick.RemoveListener(Close);
        if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
    }

    // ─────────────────────────────────────── 맨 밑 버튼
    private void OnBottomClicked()
    {
        if (bottomAction == BottomAction.GoHome)
        {
            Time.timeScale = 1f;   // 인게임이 일시정지 상태일 수 있으니 복구
            SceneManager.LoadScene(homeSceneName);
        }
        else
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // 에디터에선 플레이 종료
#endif
        }
    }

    // ─────────────────────────────────────── 언어
    private void SetupLanguageDropdown()
    {
        if (languageDropdown == null) return;

        _locales = LocalizationSettings.AvailableLocales?.Locales;
        if (_locales == null || _locales.Count == 0) return;

        if (autoPopulateLanguages)
        {
            var options = new List<string>(_locales.Count);
            foreach (var loc in _locales)
            {
                // 네이티브 이름 우선("한국어"), 없으면 LocaleName 폴백
                string native = loc.Identifier.CultureInfo?.NativeName;
                options.Add(string.IsNullOrEmpty(native) ? loc.LocaleName : native);
            }
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(options);
        }

        // 현재 선택된 로케일로 드롭다운 값 맞춤(이벤트 발생 없이)
        int current = Mathf.Max(0, _locales.IndexOf(LocalizationSettings.SelectedLocale));
        languageDropdown.SetValueWithoutNotify(current);

        languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(int index)
    {
        if (_locales != null && index >= 0 && index < _locales.Count)
            LocalizationSettings.SelectedLocale = _locales[index];
    }

    // ─────────────────────────────────────── 열기/닫기
    // 패널이 꺼진 채 시작하면 Awake가 안 돌아 panelRoot가 null일 수 있으므로
    // 여기서도 gameObject로 폴백한다. (패널 자체가 루트인 경우 대응)
    private GameObject ResolveRoot() => panelRoot != null ? panelRoot : gameObject;

    public void Open()  => ResolveRoot().SetActive(true);
    public void Close() => ResolveRoot().SetActive(false);
}
