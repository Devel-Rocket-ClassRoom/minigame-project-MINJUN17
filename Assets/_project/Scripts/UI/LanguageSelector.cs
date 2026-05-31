using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// 가짜 드롭다운 언어 선택기 (언어 2개용).
/// - 메인 버튼: 현재 언어 표시. 누르면 아래 한 칸이 펼쳐짐.
/// - 옵션 칸: "다른 언어" 하나만 표시. 누르면 그 언어로 전환 후 접힘.
/// 언어가 3개 이상이면 옵션 칸은 '다음 언어'를 순환 표시한다.
/// </summary>
public class LanguageSelector : MonoBehaviour
{
    [Header("메인 (현재 언어)")]
    [SerializeField] private Button mainButton;
    [SerializeField] private TextMeshProUGUI mainLabel;

    [Header("펼쳐지는 옵션 한 칸 (기본 비활성)")]
    [SerializeField] private GameObject optionRoot;   // 펼침/접힘 토글 대상
    [SerializeField] private Button optionButton;
    [SerializeField] private TextMeshProUGUI optionLabel;

    private List<Locale> _locales;

    private void Awake()
    {
        if (mainButton   != null) mainButton.onClick.AddListener(ToggleExpand);
        if (optionButton != null) optionButton.onClick.AddListener(OnOptionClicked);
    }

    private void OnDestroy()
    {
        if (mainButton   != null) mainButton.onClick.RemoveListener(ToggleExpand);
        if (optionButton != null) optionButton.onClick.RemoveListener(OnOptionClicked);
    }

    private void OnEnable()
    {
        _locales = LocalizationSettings.AvailableLocales?.Locales;
        Collapse();        // 열 때마다 접힌 상태로 시작
        RefreshLabels();   // 현재 언어로 라벨 동기화
    }

    private int CurrentIndex
    {
        get
        {
            if (_locales == null || _locales.Count == 0) return -1;
            int i = _locales.IndexOf(LocalizationSettings.SelectedLocale);
            return i < 0 ? 0 : i;
        }
    }

    // 옵션 칸에 보여줄(=다음에 전환될) 언어 인덱스
    private int NextIndex
    {
        get
        {
            int cur = CurrentIndex;
            if (cur < 0) return -1;
            return (cur + 1) % _locales.Count;
        }
    }

    private void RefreshLabels()
    {
        if (mainLabel != null && CurrentIndex >= 0)
            mainLabel.text = NativeName(_locales[CurrentIndex]);

        if (optionLabel != null && NextIndex >= 0)
            optionLabel.text = NativeName(_locales[NextIndex]);
    }

    private void ToggleExpand()
    {
        if (optionRoot == null) return;
        bool show = !optionRoot.activeSelf;
        if (show) RefreshLabels();   // 펼치기 직전 옵션 라벨 최신화
        optionRoot.SetActive(show);
    }

    private void Collapse()
    {
        if (optionRoot != null) optionRoot.SetActive(false);
    }

    private void OnOptionClicked()
    {
        int next = NextIndex;
        if (next >= 0) LocalizationSettings.SelectedLocale = _locales[next];

        RefreshLabels();
        Collapse();
    }

    // 네이티브 이름("한국어"/"English") 우선, 없으면 LocaleName 폴백
    private static string NativeName(Locale loc)
    {
        if (loc == null) return "";
        string native = loc.Identifier.CultureInfo?.NativeName;
        return string.IsNullOrEmpty(native) ? loc.LocaleName : native;
    }
}
