using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// 직원 선택 패널.
/// 좌/우 화살표로 전체 직원을 순회하면서 한 명씩 상세를 보여주고,
/// 승급/해고 버튼으로 즉시 조작한다.
/// StaffManager.OnRosterChanged 구독으로 채용/해고/승급/월틱마다 자동 갱신.
/// </summary>
public class StaffSelectionPanel : MonoBehaviour
{
    [Header("좌우 이동")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI indexText;     // "직원 선택 1/5"

    [Header("정보 표시")]
    [SerializeField] private TextMeshProUGUI nameText;      // "이름 : 김성은"
    [SerializeField] private TextMeshProUGUI salaryText;    // "월급 : 200,000원"
    [SerializeField] private TextMeshProUGUI gradeText;     // "초보 요리사"
    [SerializeField] private TextMeshProUGUI tenureText;    // "근속 : 3개월"
    [SerializeField] private Image portrait;                // 직급×직종 9종 — StaffData.sprite 직접 사용

    [Header("액션")]
    [SerializeField] private Button promoteButton;
    [SerializeField] private Button fireButton;

    [Header("Localization")]
    [Tooltip("직급/직종 라벨이 들어있는 String Table 이름")]
    [SerializeField] private string labelTableName = "StringTable";

    private readonly List<Staff> _roster = new();
    private int _index;

    private void OnEnable()
    {
        if (StaffManager.Instance != null)
            StaffManager.Instance.OnRosterChanged += Refresh;

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        if (prevButton    != null) prevButton.onClick.AddListener(OnPrev);
        if (nextButton    != null) nextButton.onClick.AddListener(OnNext);
        if (promoteButton != null) promoteButton.onClick.AddListener(OnPromote);
        if (fireButton    != null) fireButton.onClick.AddListener(OnFire);

        StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        Refresh();
    }

    private void OnDisable()
    {
        if (StaffManager.Instance != null)
            StaffManager.Instance.OnRosterChanged -= Refresh;

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        if (prevButton    != null) prevButton.onClick.RemoveListener(OnPrev);
        if (nextButton    != null) nextButton.onClick.RemoveListener(OnNext);
        if (promoteButton != null) promoteButton.onClick.RemoveListener(OnPromote);
        if (fireButton    != null) fireButton.onClick.RemoveListener(OnFire);
    }

    private void OnLocaleChanged(Locale _) => Refresh();

    private void Refresh()
    {
        _roster.Clear();
        if (StaffManager.Instance != null)
            foreach (var s in StaffManager.Instance.GetAllStaffs())
                _roster.Add(s);

        if (_roster.Count == 0)
        {
            if (indexText   != null) indexText.text   = "직원 없음";
            if (nameText    != null) nameText.text    = "-";
            if (salaryText  != null) salaryText.text  = "-";
            if (gradeText   != null) gradeText.text   = "-";
            if (tenureText  != null) tenureText.text  = "-";
            if (portrait    != null) portrait.enabled = false;
            if (promoteButton != null) promoteButton.interactable = false;
            if (fireButton    != null) fireButton.interactable    = false;
            return;
        }

        _index = Mathf.Clamp(_index, 0, _roster.Count - 1);
        var st = _roster[_index];
        var d  = st.Data;

        if (indexText  != null) indexText.text  = $"직원 선택 {_index + 1}/{_roster.Count}";
        if (nameText   != null) nameText.text   = $"이름 : {st.Name}";
        if (salaryText != null) salaryText.text = $"월급 : {st.EffectiveSalary:N0}원";
        if (gradeText  != null) gradeText.text  = d.grade == StaffType.Manager
            ? $"{RoleLabel(d.role)} {GradeLabel(d.grade)}"
            : $"{GradeLabel(d.grade)} {RoleLabel(d.role)}";
        if (tenureText != null) tenureText.text = $"근속 : {st.TenureMonths}개월";

        if (portrait != null)
        {
            portrait.enabled = d.sprite != null;
            portrait.sprite  = d.sprite;
        }

        if (promoteButton != null) promoteButton.interactable = st.CanUpgrade;
        if (fireButton    != null) fireButton.interactable    = true;
    }

    private void OnPrev()
    {
        if (_roster.Count == 0) return;
        _index = (_index - 1 + _roster.Count) % _roster.Count;
        Refresh();
    }

    private void OnNext()
    {
        if (_roster.Count == 0) return;
        _index = (_index + 1) % _roster.Count;
        Refresh();
    }

    private void OnPromote()
    {
        if (_roster.Count == 0 || StaffManager.Instance == null) return;
        StaffManager.Instance.Upgrade(_roster[_index]);
        // 성공 시 OnRosterChanged 가 Refresh 호출 / 실패 시 조용히 무시
    }

    private void OnFire()
    {
        if (_roster.Count == 0 || StaffManager.Instance == null) return;
        StaffManager.Instance.Fire(_roster[_index]);
        // 해고되면 리스트 줄어들어 Refresh 안에서 인덱스 자동 클램프
    }

    private string RoleLabel(StaffRole r)
    {
        string key = r switch
        {
            StaffRole.Cook   => "staff.role.cook",
            StaffRole.Server => "staff.role.server",
            StaffRole.Rider  => "staff.role.rider",
            _ => null
        };
        return ResolveLabel(key, r.ToString());
    }

    private string GradeLabel(StaffType g)
    {
        string key = g switch
        {
            StaffType.Junior  => "staff.grade.junior",
            StaffType.Senior  => "staff.grade.senior",
            StaffType.Manager => "staff.grade.manager",
            _ => null
        };
        return ResolveLabel(key, g.ToString());
    }

    private string ResolveLabel(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        try
        {
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(labelTableName, key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }
        catch
        {
            return fallback;
        }
    }
}
