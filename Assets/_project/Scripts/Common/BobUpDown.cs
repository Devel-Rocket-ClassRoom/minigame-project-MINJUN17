using DG.Tweening;
using UnityEngine;

/// <summary>
/// 오브젝트를 제자리에서 위아래로 살짝 반복 이동(까딱). 계단 안내 화살표 등에 사용.
/// 씬 오브젝트에 붙이기만 하면 자동 재생. (DOTween Yoyo 루프)
/// </summary>
public class BobUpDown : MonoBehaviour
{
    [Tooltip("위아래로 움직이는 거리")]
    [SerializeField] private float distance = 0.15f;
    [Tooltip("한 방향 이동 시간(초). 작을수록 빠르게 까딱")]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Ease ease = Ease.InOutSine;

    private Tween _tween;
    private Vector3 _startPos;

    private void OnEnable()
    {
        _startPos = transform.localPosition;
        _tween = transform
            .DOLocalMoveY(_startPos.y + distance, duration)
            .SetEase(ease)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)        // 일시정지(timeScale 0)에도 계속 까딱
            .SetLink(gameObject);   // 오브젝트 파괴 시 자동 정리
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        transform.localPosition = _startPos;   // 원위치 복구
    }
}
