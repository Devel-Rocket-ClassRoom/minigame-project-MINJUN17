using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 월별 매출 막대 그래프. 최근 N개월을 막대로 표시.
/// X축 = 월(막대 아래 라벨), Y축 = 보이는 구간 최대 매출(maxValueLabel에 표기).
/// 막대 높이 = 그달매출 / 최대매출 × maxBarHeight.
///
/// 구조(에디터에서 준비):
///   barContainer : Horizontal Layout Group (막대 칸들 가로 배치)
///   barPrefab    : Vertical Layout Group(하단 정렬) 안에
///                    - Bar  : Image + LayoutElement (코드가 preferredHeight 설정)
///                    - Label: TextMeshProUGUI (월 표시)
/// </summary>
public class RevenueGraph : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private RectTransform barContainer;     // 막대들이 들어갈 영역 (HorizontalLayoutGroup)
    [SerializeField] private GameObject barPrefab;           // 막대 1칸 프리팹
    [SerializeField] private TextMeshProUGUI maxValueLabel;  // Y축 최대값 표기 (옵션)

    [Header("설정")]
    [SerializeField] private int monthsToShow = 12;
    [Tooltip("막대 최대 높이를 '컨테이너 실제 높이'의 몇 %로 잡을지 (반응형). 라벨 공간 고려해 0.85 권장)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float heightFraction = 0.85f;
    [Tooltip("컨테이너 높이를 못 읽을 때만 쓰는 폴백 px")]
    [SerializeField] private float fallbackBarHeight = 200f;
    [Tooltip("매출 0이어도 살짝 보이게 하는 최소 높이(px)")]
    [SerializeField] private float minBarHeight = 2f;

    [Header("표시 형식")]
    [SerializeField] private string maxValueFormat   = "{0:N0}원";
    [SerializeField] private string monthLabelFormat = "{0}월";
    [SerializeField] private string emptyMaxText     = "기록 없음";

    // 에디터에서 컴포넌트 우클릭 → 실행. ⚠️ Play 모드에서 눌러야 정상 동작(Destroy/Instantiate 때문).
    [ContextMenu("테스트 데이터로 그려보기 (Play 모드)")]
    private void RenderTestData()
    {
        var fake = new List<MonthlyRevenueEntry>();
        long[] vals = { 800000, 1200000, 950000, 1900000, 1500000, 1700000,
                        600000, 1100000, 1850000, 1300000, 1000000, 1600000 };
        for (int i = 0; i < vals.Length; i++)
            fake.Add(new MonthlyRevenueEntry { year = 0, month = i + 1, revenue = vals[i] });
        Render(fake);
    }

    /// <summary>월별 매출 기록으로 그래프를 다시 그린다.</summary>
    public void Render(IReadOnlyList<MonthlyRevenueEntry> history)
    {
        if (barContainer == null || barPrefab == null) return;

        // 기존 막대 제거
        for (int i = barContainer.childCount - 1; i >= 0; i--)
            Destroy(barContainer.GetChild(i).gameObject);

        int count = history?.Count ?? 0;
        int start = Mathf.Max(0, count - monthsToShow);   // 최근 N개월

        // 막대 최대높이 = 컨테이너 실제 높이 × 비율 (반응형). 못 읽으면 폴백 px.
        float containerH = barContainer.rect.height;
        float maxBarHeight = (containerH > 1f) ? containerH * heightFraction : fallbackBarHeight;

        // 보이는 구간 최대 매출 (0 방지)
        long max = 1;
        for (int i = start; i < count; i++)
            if (history[i].revenue > max) max = history[i].revenue;

        if (maxValueLabel != null)
            maxValueLabel.text = (count > start)
                ? string.Format(maxValueFormat, max)
                : emptyMaxText;

        // 항상 monthsToShow(12)칸 생성 → 균등 분할로 폭이 늘 일정.
        // 데이터는 왼쪽부터 쌓이고, 남는 칸은 오른쪽에 빈 칸으로.
        // 12개월 다 차면 last-12 윈도우라 새 달이 오른쪽에 붙고 가장 오래된 달이 왼쪽으로 밀려 사라짐.
        int dataCount = count - start;
        int emptySlots = monthsToShow - dataCount;

        // 1) 실제 데이터 (왼쪽부터)
        for (int i = start; i < count; i++)
        {
            float ratio = Mathf.Clamp01((float)history[i].revenue / max);
            float h = Mathf.Max(minBarHeight, ratio * maxBarHeight);
            CreateColumn(h, string.Format(monthLabelFormat, history[i].month));
        }

        // 2) 남은 빈 칸 (오른쪽)
        for (int e = 0; e < emptySlots; e++)
            CreateColumn(0f, "");
    }

    private void CreateColumn(float heightPx, string label)
    {
        var go = Instantiate(barPrefab, barContainer);
        go.SetActive(true);

        var rb = go.GetComponent<RevenueBar>();
        if (rb != null)
        {
            if (rb.bar != null)        rb.bar.preferredHeight = heightPx;
            if (rb.monthLabel != null) rb.monthLabel.text = label;
        }
    }
}
