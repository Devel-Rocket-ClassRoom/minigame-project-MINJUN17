using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시상식 사회자 등장 연출. 화면 밖/옆에서 무대 중앙으로 걸어 들어온 뒤 멈춘다.
/// - 걷는 동안: HostAnimator.PlayWalk()
/// - 도착하면: HostAnimator.PlayIdle()
///
/// 배치: 에디터에서 사회자를 "도착 지점(무대 중앙)"에 두면,
/// 코드가 entranceOffset만큼 뒤로 뺀 곳을 시작 위치로 자동 계산한다.
///
/// CeremonyDirector가 WalkIn()을 호출하고 반환 Tween을 시퀀스에 엮는다.
/// (도착 시 onArrived 콜백으로 핀조명 켜기 등 연결)
/// </summary>
public class HostEntrance : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("움직일 사회자 RectTransform. 비우면 자기 자신")]
    [SerializeField] private RectTransform host;
    [Tooltip("사회자 애니 상태 전환기. 비우면 이동만(애니 전환 없음)")]
    [SerializeField] private HostAnimator animator;

    [Header("이동")]
    [Tooltip("도착 지점(에디터 배치 위치) 기준, 시작 위치까지의 오프셋(px). 예: (-700,0) = 왼쪽에서 들어옴")]
    [SerializeField] private Vector2 entranceOffset = new(-700f, 0f);
    [SerializeField] private float walkDuration = 1.4f;
    [SerializeField] private Ease walkEase = Ease.Linear;

    private Vector2 _arrivePos;   // 에디터 배치 위치 = 도착 지점
    private bool _captured;

    private void Awake() => CaptureArrivePos();

    private void CaptureArrivePos()
    {
        if (host == null) host = transform as RectTransform;
        if (host != null && !_captured)
        {
            _arrivePos = host.anchoredPosition;
            _captured = true;
        }
    }

    /// <summary>시작 위치(화면 밖)로 즉시 이동 + 숨김 준비.</summary>
    public void ResetToStart()
    {
        CaptureArrivePos();
        if (host != null) host.anchoredPosition = _arrivePos + entranceOffset;
    }

    /// <summary>걸어 들어오기. 도착하면 onArrived 호출. 반환 Tween을 시퀀스에 엮어도 됨.</summary>
    public Tween WalkIn(System.Action onArrived = null)
    {
        CaptureArrivePos();
        if (host == null) { Debug.LogWarning("[HostEntrance] host(RectTransform) 가 null"); return null; }

        host.anchoredPosition = _arrivePos + entranceOffset;
        if (animator != null) animator.PlayWalk();

        return host.DOAnchorPos(_arrivePos, walkDuration)
            .SetEase(walkEase)
            .SetUpdate(true)
            .SetLink(host.gameObject)
            .OnComplete(() =>
            {
                if (animator != null) animator.PlayIdle();
                onArrived?.Invoke();
            });
    }

    [ContextMenu("▶ Walk In (걸어오기)")]
    private void WalkInContext() => WalkIn();

    [ContextMenu("■ Reset To Start (시작 위치로)")]
    private void ResetContext() => ResetToStart();
}
