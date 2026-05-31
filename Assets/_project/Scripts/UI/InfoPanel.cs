using TMPro;
using UnityEngine;

/// <summary>
/// 정보 탭. 인게임 누적 통계 + 월별 매출 그래프 표시.
/// - 누적 방문 손님 수
/// - 총매출
/// - 역대 최고 순위
/// - 월별 매출 그래프 (RevenueGraph)
///
/// 열기: "정보" 버튼 onClick → Open() (PopupPanel 사용) 또는 SetActive.
/// 켜질 때마다 OnEnable에서 최신 데이터로 갱신.
/// </summary>
public class InfoPanel : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PopupPanel popup;                  // 열기/닫기 애니메이션 (옵션)
    [SerializeField] private TextMeshProUGUI totalCustomersText;
    [SerializeField] private TextMeshProUGUI lifetimeRevenueText;
    [SerializeField] private TextMeshProUGUI bestRankText;
    [SerializeField] private RevenueGraph graph;

    [Header("표시 형식")]
    [SerializeField] private string customersFormat = "누적 손님: {0:N0}명";
    [SerializeField] private string revenueFormat   = "총매출: {0:N0}원";
    [SerializeField] private string bestRankFormat  = "역대 최고: {0}등";
    [SerializeField] private string noRankText      = "역대 최고: 기록 없음";

    private void OnEnable() => Refresh();

    /// <summary>"정보" 버튼 onClick에 연결.</summary>
    public void Open()
    {
        popup?.Open();
        if (popup == null) gameObject.SetActive(true);
        else Refresh();   // 이미 활성 상태에서 다시 열 때 대비
    }

    public void Close() => popup?.Close();

    public void Refresh()
    {
        var sales = SalesTracker.Instance;
        if (sales != null)
        {
            if (totalCustomersText != null)
                totalCustomersText.text = string.Format(customersFormat, sales.TotalCustomers);
            if (lifetimeRevenueText != null)
                lifetimeRevenueText.text = string.Format(revenueFormat, sales.LifetimeRevenue);
            graph?.Render(sales.MonthlyHistory);
        }

        var ranking = RankingSystem.Instance;
        if (bestRankText != null)
        {
            int best = ranking != null ? ranking.BestRank : 0;
            bestRankText.text = best > 0
                ? string.Format(bestRankFormat, best)
                : noRankText;
        }
    }
}
