using UnityEngine;

public class RiderStaff : Staff
{
    private RiderState _state;
    private Food _carryingFood;
    private Food _claimedFood;   // 픽업대에 시각적으로 남아있으나 클레임만 한 음식
    private float _deliverDurationCache;

    // 사이드워크 ↔ 가게 안 횡단 시 도어 셀 (0,1) 경유 — 손님과 같은 문 공유
    private bool _passedDoor;
    private static readonly Vector3 DoorWorld = new Vector3(0.5f, 1.5f, 0f);

    // 라이더 대기 위치 — 화면 밖 한 점에 전부 겹쳐 대기 (겹쳐도 안 보이므로 무관)
    private static readonly Vector3 StandbyPos = new Vector3(-6.5f, 1.5f, 0f);

    public bool IsIdle => _state == RiderState.IDLE_OUTSIDE;
    public float EffectiveDeliveryDuration
    {
        get
        {
            // hireVariance(+)면 더 빠름, growthMultiplier(>1)면 더 빠름 → 둘 다 분모로
            float divisor = Mathf.Max(0.01f, (1f + _hireVariance) * _growthMultiplier);
            float adjusted = _data.deliveryTime / divisor;
            return Mathf.Max(5f, adjusted);
        }
    }

    public void Init(StaffData data, int id, string nameKey, float hireVariance = 0f)
    {
        InitBase(data, id, nameKey, hireVariance);
        ChangeState(RiderState.IDLE_OUTSIDE);
    }

    protected override PathRole GetPathRole() => PathRole.Rider;

    private void Update()
    {
        if (TickCommute()) { _stateTimer += Time.deltaTime; return; }

        switch (_state)
        {
            case RiderState.IDLE_OUTSIDE:       IdleOutsideState(); break;
            case RiderState.WALK_TO_PASSWINDOW: WalkToPassWindowState(); break;
            case RiderState.WALK_TO_EXIT:       WalkToExitState(); break;
            case RiderState.DELIVER:            DeliverState(); break;
        }
        _stateTimer += Time.deltaTime;
    }

    // 모든 라이더가 같은 대기 지점에 겹쳐 대기
    private Vector3 OutsideWaitPos() => StandbyPos;

    private void ChangeStateWithDoor(RiderState next)
    {
        ChangeState(next);
        _passedDoor = false;   // 도어 다시 통과해야 함
    }

    protected override Vector3 GetWorkPosition() => OutsideWaitPos();

    protected override void OnArrivedAtWork()
    {
        SetVisible(true);   // 배달 중 숨겨졌던 경우 대비
        ChangeState(RiderState.IDLE_OUTSIDE);
    }

    private void ChangeState(RiderState next)
    {
        _state = next;
        _stateTimer = 0f;
    }

    private void IdleOutsideState()
    {
        // 배달 음식 있으면 클레임 (실제 픽업은 픽업대 도착 시점에)
        if (PassWindowManager.Instance.HasReadyDeliveryFood())
        {
            var pwTransform = PassWindowManager.Instance.GetFirstPassWindowTransform();
            if (pwTransform != null && IsClosestIdleRiderTo(pwTransform.position))
            {
                _claimedFood = PassWindowManager.Instance.ClaimDeliveryFood(this);
                if (_claimedFood != null)
                {
                    ChangeStateWithDoor(RiderState.WALK_TO_PASSWINDOW);
                    return;
                }
            }
        }

        // 일감 없으면 밖에서 대기
        MoveTo(OutsideWaitPos());
        _dirAnim?.FaceDirection(DirectionalCharacterAnimator.DIR_DOWN);  // 휴식 시 정면 (이동 중엔 LateUpdate가 덮어씀)
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
        // 사이드워크에서 출발 — 1단계: 도어 (0,1) 경유, 2단계: 픽업대로 그리드 길찾기
        if (!_passedDoor)
        {
            MoveTo(DoorWorld);
            if (HasArrived()) _passedDoor = true;
            return;
        }

        Vector3 target = PassWindowManager.Instance.GetApproachPosition(PathRole.Rider, transform.position);
        if (target == Vector3.zero)
        {
            // 픽업대 접근 불가: 클레임만 해제하고 음식은 픽업대에 그대로 둠
            if (_claimedFood != null) _claimedFood.claimedBy = null;
            _claimedFood = null;
            ChangeStateWithDoor(RiderState.WALK_TO_EXIT);
            return;
        }
        MoveTo(target);
        if (HasArrived())
        {
            // 픽업대 도착: 클레임한 음식 실제로 들기
            if (_claimedFood != null)
            {
                _carryingFood = PassWindowManager.Instance.TakeFood(_claimedFood);
                _claimedFood = null;
                if (_carryingFood != null) AttachFood(_carryingFood);
            }
            ChangeStateWithDoor(RiderState.WALK_TO_EXIT);
        }
    }

    private void WalkToExitState()
    {
        // 가게 안에서 출발(또는 픽업대에서) — 1단계: 도어로, 2단계: 출구로
        if (!_passedDoor)
        {
            MoveTo(DoorWorld);
            if (HasArrived()) _passedDoor = true;
            return;
        }

        Vector3 exit = CustomerManager.Instance != null
            ? CustomerManager.Instance.ExitPosition
            : transform.position;
        MoveTo(exit);
        if (HasArrived())
        {
            // 음식 들고 나가서 배달 (화면 밖) — 클레임 실패로 빈손인 경우 방어
            if (_carryingFood != null) Destroy(_carryingFood.gameObject);
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

        // 배달 끝 → 밖(입구)으로 복귀 후 다시 보이게
        transform.position = OutsideWaitPos();
        SetVisible(true);
        ChangeState(RiderState.IDLE_OUTSIDE);
    }

    private void SetVisible(bool visible)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = visible;
    }
}
