using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시상식 연출 총괄(지휘). RankingSystem.OnYearRanked 를 구독해
/// 조명·사회자·봉투 연출과 기존 RankingPanel 결과 공개를 하나로 엮는다.
///
/// 흐름:
///   12월 정산 확인
///   → 무대 켜짐 + 불 켜짐 + 패널 먼저 뜸(타이틀) + 사회자 왼쪽에서 걸어옴
///   → 도착: 핀조명 ON + idle + 드럼소리, 연매출·평판 카운트업
///   → (다 뜨면) 사회자 편지 꺼냄(8프레임) + 편지 UI 애니(4프레임) 동시 재생
///   → 두 애니 모두 끝나면 → 순위 공개 + 발표 효과음
///   → 확인 닫힘 → 불 켜고 무대 정리 + 시간 재개
///
/// ※ RankingPanel의 autoSubscribe는 꺼두고 이 Director가 흐름을 제어
///   (드럼롤/발표 효과음은 Director가 직접 재생). 관중은 각자 UIImageAnimator로 들썩임.
/// </summary>
public class CeremonyDirector : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private RankingSystem rankingSystem;
    [Tooltip("연출 동안만 켜질 무대 루트(조명·사회자·관중·배경 묶음). 비우면 항상 켜진 상태로 간주")]
    [SerializeField] private GameObject stageRoot;
    [SerializeField] private CeremonyLighting lighting;
    [SerializeField] private HostEntrance host;
    [SerializeField] private HostAnimator hostAnimator;
    [SerializeField] private RankingPanel rankingPanel;

    [Header("편지 UI 애니 (사회자 편지와 동시 재생, 4프레임 등)")]
    [Tooltip("사회자가 편지 꺼낼 때 같이 뜨는 별도 편지 UI의 UIImageAnimator. 단발(loop off) 권장")]
    [SerializeField] private UIImageAnimator envelopeUiAnim;

    private RankingSystem.YearResult _pending;

    private void OnEnable()
    {
        if (rankingSystem != null) rankingSystem.OnYearRanked += Begin;
        if (rankingPanel != null)
        {
            rankingPanel.OnResultsReady += OnResultsReady;
            rankingPanel.OnClosed += Teardown;
        }
    }

    private void OnDisable()
    {
        if (rankingSystem != null) rankingSystem.OnYearRanked -= Begin;
        if (rankingPanel != null)
        {
            rankingPanel.OnResultsReady -= OnResultsReady;
            rankingPanel.OnClosed -= Teardown;
        }
    }

    /// <summary>12월 정산 직후: 무대 세팅 + 패널 먼저 + 사회자 걸어오기.</summary>
    private void Begin(RankingSystem.YearResult result)
    {
        _pending = result;

        Time.timeScale = 0f;   // 시상식 동안 게임 시간 정지 (연출 트윈은 SetUpdate(true)/ignoreTimeScale)

        if (stageRoot != null) stageRoot.SetActive(true);
        if (lighting != null) lighting.LightsOn(true);     // 처음엔 환하게(빔 꺼짐)
        if (hostAnimator != null) hostAnimator.PlayIdle();
        if (envelopeUiAnim != null) envelopeUiAnim.gameObject.SetActive(false);   // 편지 UI는 발표 직전까지 숨김

        // 패널 먼저 띄우기(타이틀만) → 사회자가 걷는 동안 패널 프레임 노출
        if (rankingPanel != null) rankingPanel.ShowEmpty(_pending);

        if (host != null)
        {
            host.ResetToStart();
            host.WalkIn(OnArrived);
        }
        else OnArrived();
    }

    /// <summary>도착: 핀조명 ON + idle + 드럼소리. 패널이 연매출·평판 카운트업 시작.</summary>
    private void OnArrived()
    {
        if (lighting != null) lighting.SpotlightOn();
        if (hostAnimator != null) hostAnimator.PlayIdle();
        SoundManager.Get()?.PlaySfx(SfxId.Drumroll);

        if (rankingPanel != null) rankingPanel.ShowResults(_pending);
        else OnResultsReady();
    }

    /// <summary>연매출·평판 다 뜸 → 사회자 편지 + 편지 UI 동시 재생 → 둘 다 끝나면 발표.</summary>
    private void OnResultsReady()
    {
        float wait = 0f;

        if (hostAnimator != null)
        {
            hostAnimator.PlayEnvelope();
            wait = Mathf.Max(wait, hostAnimator.EnvelopeDuration);
        }

        if (envelopeUiAnim != null)
        {
            envelopeUiAnim.gameObject.SetActive(true);
            envelopeUiAnim.Play();
            wait = Mathf.Max(wait, envelopeUiAnim.Duration);
        }

        if (wait <= 0f) Reveal();
        else DOVirtual.DelayedCall(wait, Reveal, true).SetLink(gameObject);
    }

    /// <summary>편지 애니 다 끝남 → 순위 공개 + 발표 효과음.</summary>
    private void Reveal()
    {
        SoundManager.Get()?.PlaySfx(SfxId.RankingReveal);
        if (rankingPanel != null) rankingPanel.RevealRank();
    }

    /// <summary>확인 닫힘: 불 켜고 무대 정리 + 시간 재개.</summary>
    private void Teardown()
    {
        if (lighting != null) lighting.LightsOn();
        if (envelopeUiAnim != null) envelopeUiAnim.gameObject.SetActive(false);
        if (stageRoot != null) stageRoot.SetActive(false);
        Time.timeScale = 1f;
    }
}
