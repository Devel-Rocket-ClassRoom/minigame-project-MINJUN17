using System;
using System.Collections;
using UnityEngine;

public class TimeSystem : MonoBehaviour
{
    public event Action OnHourChanged;

    [SerializeField] private float _nightHourInterval = 0.5f;
    [SerializeField] private float _hourInterval = 15f;
    [SerializeField] private int _openHour = 8;
    [SerializeField] private int _closeHour = 24;

    private float _timer;
    private int _hour = 8;
    private int _month = 1;
    private int _year = 0;
    private bool _ticking = true;
    private Coroutine _rollRoutine;

    public int Hour => _hour;
    public int Month => _month;
    public int Year => _year;
    public bool IsOpen => _ticking;

    public event Action OnCloseHourReached;
    public event Action OnDayStarted;
    public event Action OnYearEnded;

    private void Update()
    {
        if (!_ticking) return;

        _timer += Time.deltaTime;
        if (_timer < _hourInterval) return;

        _timer = 0f;
        _hour++;
        OnHourChanged?.Invoke();

        if (_hour >= _closeHour)
        {
            _ticking = false;
            OnCloseHourReached?.Invoke();
        }
    }

    public void BeginDay()
    {
        if (_rollRoutine != null) StopCoroutine(_rollRoutine);
        _rollRoutine = StartCoroutine(RollToOpenHour());
    }
    private IEnumerator RollToOpenHour()
    {
        while (_hour != _openHour)
        {
            yield return new WaitForSeconds(_nightHourInterval);
            _hour++;
            if (_hour >= 24)
            {
                _hour = 0;
                _month++;
                if (_month > 12) { _month = 1; _year++; OnYearEnded?.Invoke(); }
            }
            OnHourChanged?.Invoke();
        }
        _timer = 0f;
        _ticking = true;
        _rollRoutine = null;
        OnDayStarted?.Invoke();
    }

    // ─── Save / Load ───
    public TimeData ToData() => new TimeData
    {
        hour    = _hour,
        month   = _month,
        year    = _year,
        ticking = _ticking,
        timer   = _timer,
    };

    public void FromData(TimeData data)
    {
        if (data == null) return;

        // 진행 중일 수 있는 RollToOpenHour 코루틴 정리
        StopAllCoroutines();
        _rollRoutine = null;

        _hour    = data.hour;
        _month   = data.month;
        _year    = data.year;
        _ticking = data.ticking;
        _timer   = data.timer;

        OnHourChanged?.Invoke();   // HUD 즉시 갱신

        // 영업 중이 아니면 다음 영업일 코루틴 재시작
        if (!_ticking) BeginDay();
    }
}