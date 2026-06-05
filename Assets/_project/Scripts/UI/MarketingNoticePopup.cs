using System.Collections;
using UnityEngine;

/// <summary>
/// 마케팅 적용(구매) 시 "다음달부터 더 많은 손님이 옵니다" 안내 창을 일정 시간 띄운다.
/// 항상 활성인 오브젝트(예: Canvas/UI 매니저)에 붙이고, noticeRoot에 안내 창을 연결.
/// (이 컴포넌트를 안내 창 자체에 붙이면 SetActive(false) 시 코루틴이 끊기므로 분리할 것)
/// </summary>
public class MarketingNoticePopup : MonoBehaviour
{
    [Tooltip("띄울 안내 창 루트 (기본 비활성 상태로 둘 것)")]
    [SerializeField] private GameObject noticeRoot;
    [Tooltip("표시 시간(초)")]
    [SerializeField] private float showSeconds = 3f;

    private MarketingManager _manager;
    private Coroutine _routine;

    private void Start()
    {
        _manager = MarketingManager.Instance;
        if (_manager != null) _manager.OnMarketingPurchased += HandlePurchased;
        if (noticeRoot != null) noticeRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.OnMarketingPurchased -= HandlePurchased;
    }

    private void HandlePurchased(MarketingData _)
    {
        if (noticeRoot == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        noticeRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(showSeconds);   // 정산/일시정지 timeScale 영향 안 받게 realtime
        noticeRoot.SetActive(false);
        _routine = null;
    }
}
