using System;
using System.Collections;
using UnityEngine;

public class TimeSystem : MonoBehaviour
{
    public event Action OnHourChanged;

    [SerializeField] private float _nightHourInterval = 0.5f;
    [SerializeField] private float _hourInterval = 15f;
    [SerializeField] private int _openHour = 8;
    [SerializeField] private int _closeHour = 12;

    private float _timer;
    private int _hour = 8;
    private int _month = 0;
    private int _year = 0;
    private bool _ticking = true;

    public int Hour => _hour;
    public int Month => _month;
    public int Year => _year;
    public bool IsOpen => _ticking;

    public event Action OnCloseHourReached;

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
        StartCoroutine(RollToOpenHour());
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
                if (_month > 12) { _month = 1; _year++; }
            }
            OnHourChanged?.Invoke();
        }
        _timer = 0f;
        _ticking = true;
    }
}