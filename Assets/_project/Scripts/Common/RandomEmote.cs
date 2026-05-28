using UnityEngine;

/// <summary>
/// 랜덤 간격으로 잠깐씩 떠올랐다 사라지는 머리 위 emote.
/// 자식 SpriteRenderer에 부착. 부모(Customer 등)에서 Begin/End로 활성 토글.
///
/// 동작:
///   Begin() → 랜덤 interval(min~max) 뒤에 잠깐(showDuration) 보였다 사라짐을 반복
///   End()   → 즉시 숨김 + 사이클 중단
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class RandomEmote : MonoBehaviour
{
    [Tooltip("emote가 안 보이는 평상시 간격 (초)")]
    [SerializeField] private float minInterval = 3f;
    [SerializeField] private float maxInterval = 7f;

    [Tooltip("한 번 보일 때 유지 시간 (초)")]
    [SerializeField] private float showDuration = 1.5f;

    private SpriteRenderer _sr;
    private bool _active;
    private float _timer;
    private bool _showing;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.enabled = false;
    }

    public void Begin()
    {
        if (_active) return;
        _active = true;
        _showing = false;
        _sr.enabled = false;
        _timer = Random.Range(minInterval, maxInterval);   // 시작하자마자 띄우지 말고 한 사이클 기다림
    }

    public void End()
    {
        _active = false;
        _showing = false;
        if (_sr != null) _sr.enabled = false;
    }

    private void Update()
    {
        if (!_active) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        if (_showing)
        {
            // 노출 끝 → 숨기고 다음 등장까지 대기
            _showing = false;
            _sr.enabled = false;
            _timer = Random.Range(minInterval, maxInterval);
        }
        else
        {
            // 대기 끝 → 노출 시작
            _showing = true;
            _sr.enabled = true;
            _timer = showDuration;
        }
    }
}
