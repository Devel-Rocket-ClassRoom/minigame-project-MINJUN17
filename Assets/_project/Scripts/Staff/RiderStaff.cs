using System.Collections.Generic;
using UnityEngine;

public class RiderStaff : Staff
{
    private RiderState _state;
    private Food _carryingFood;
    private float _deliverDurationCache;
    private Vector3? _currentRestTarget;
    private const float kRestBlockRadius = 0.5f;

    public bool IsIdle => _state == RiderState.IDLE_AT_RIDERPOS;
    public float EffectiveDeliveryDuration
    {
        get
        {
            // hireVariance(+)면 더 빠름, growthMultiplier(>1)면 더 빠름 → 둘 다 분모로
            float divisor = Mathf.Max(0.01f, (1f + _hireVariance) * _growthMultiplier);
            float adjusted = _data.deliveryTime / divisor;
            return RiderRoomManager.Instance != null
                ? RiderRoomManager.Instance.GetEffectiveDeliveryDuration(adjusted)
                : Mathf.Max(5f, adjusted);
        }
    }

    public void Init(StaffData data, int id, float hireVariance = 0f)
    {
        InitBase(data, id, hireVariance);
        ChangeState(RiderState.IDLE_AT_RIDERPOS);
    }

    protected override PathRole GetPathRole() => PathRole.Rider;

    private void Update()
    {
        switch (_state)
        {
            case RiderState.IDLE_AT_RIDERPOS:   IdleAtRiderPosState(); break;
            case RiderState.WALK_TO_PASSWINDOW: WalkToPassWindowState(); break;
            case RiderState.WALK_TO_EXIT:       WalkToExitState(); break;
            case RiderState.DELIVER:            DeliverState(); break;
            case RiderState.RETURN_TO_ENTRY:    ReturnToEntryState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    private void ChangeState(RiderState next)
    {
        _state = next;
        _stateTimer = 0f;
        if (next != RiderState.IDLE_AT_RIDERPOS && next != RiderState.RETURN_TO_ENTRY)
            _currentRestTarget = null;
    }

    private void IdleAtRiderPosState()
    {
        // 배달 음식 있으면 픽업 (가장 가까운 idle 라이더 우선)
        if (PassWindowManager.Instance.HasReadyDeliveryFood())
        {
            var pwTransform = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (pwTransform != null && IsClosestIdleRiderTo(pwTransform.position))
            {
                _carryingFood = PassWindowManager.Instance.PickupDeliveryFood();
                if (_carryingFood != null)
                {
                    ChangeState(RiderState.WALK_TO_PASSWINDOW);
                    return;
                }
            }
        }

        // 라이더룸 휴식 위치로 이동 (한 번 정한 목표는 다른 라이더가 차지하기 전까진 유지)
        if (RiderRoomManager.Instance != null)
        {
            if (_currentRestTarget == null
                || IsRestTargetTakenByOther(_currentRestTarget.Value))
            {
                Vector3 picked = PickRestSpot();
                _currentRestTarget = picked != Vector3.zero ? picked : (Vector3?)null;
            }
            if (_currentRestTarget.HasValue) MoveTo(_currentRestTarget.Value);
        }
    }

    private bool IsClosestIdleRiderTo(Vector3 target)
    {
        float myDist = Vector3.Distance(transform.position, target);
        foreach (var r in StaffManager.Instance.RiderStaffs)
        {
            if (r == this) continue;
            if (!r.IsIdle) continue;
            float d = Vector3.Distance(r.transform.position, target);
            if (d < myDist) return false;
            if (Mathf.Approximately(d, myDist) && r.Id < this.Id) return false;
        }
        return true;
    }

    private void WalkToPassWindowState()
    {
        // 음식 픽업은 IdleAt에서 했으니 여기선 PassWindow 인접 위치로 걸어가는 연출만
        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Rider, transform.position);
        if (target == Vector3.zero)
        {
            ChangeState(RiderState.WALK_TO_EXIT);
            return;
        }
        MoveTo(target);
        if (HasArrived())
            ChangeState(RiderState.WALK_TO_EXIT);
    }

    private void WalkToExitState()
    {
        Vector3 exit = CustomerManager.Instance != null
            ? CustomerManager.Instance.ExitPosition
            : transform.position;
        MoveTo(exit);
        if (HasArrived())
        {
            // 배달 시간 캐시 후 시각만 가림 (Update는 계속 돌아야 _stateTimer가 증가)
            _deliverDurationCache = EffectiveDeliveryDuration;
            SetVisible(false);
            ChangeState(RiderState.DELIVER);
        }
    }

    private void DeliverState()
    {
        if (_stateTimer < _deliverDurationCache) return;

        _carryingFood = null;
        if (PhoneManager.Instance != null) PhoneManager.Instance.OnDeliveryCompleted();

        // entryPoint에 재배치 후 다시 보이게
        if (CustomerManager.Instance != null)
            transform.position = CustomerManager.Instance.EntryPosition;
        SetVisible(true);

        ChangeState(RiderState.RETURN_TO_ENTRY);
    }

    private void SetVisible(bool visible)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = visible;
    }

    private void ReturnToEntryState()
    {
        if (_currentRestTarget == null
            || IsRestTargetTakenByOther(_currentRestTarget.Value))
        {
            Vector3 picked = PickRestSpot();
            _currentRestTarget = picked != Vector3.zero ? picked : (Vector3?)null;
        }
        if (!_currentRestTarget.HasValue)
        {
            ChangeState(RiderState.IDLE_AT_RIDERPOS);
            return;
        }
        MoveTo(_currentRestTarget.Value);
        if (HasArrived())
            ChangeState(RiderState.IDLE_AT_RIDERPOS);
    }

    private Vector3 PickRestSpot()
    {
        var candidates = GridManager.Instance.GetWalkableCellsInZone(CellZone.RiderRoom);
        var occupiers = new List<Vector3>();
        foreach (var r in StaffManager.Instance.RiderStaffs)
            if (r != this) occupiers.Add(r.transform.position);
        return RestSpotPicker.PickClosestFree(
            transform.position, candidates, occupiers, kRestBlockRadius);
    }

    private bool IsRestTargetTakenByOther(Vector3 target)
    {
        if (Vector3.Distance(transform.position, target) < kRestBlockRadius) return false;
        foreach (var r in StaffManager.Instance.RiderStaffs)
        {
            if (r == this) continue;
            if (Vector3.Distance(r.transform.position, target) < kRestBlockRadius)
                return true;
        }
        return false;
    }
}
