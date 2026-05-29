using UnityEngine;

/// <summary>
/// 캐릭터(손님/직원) 근접 시 자동 열림/닫힘. KitchenDoor 일반화판.
/// Animator의 "Speed" float 파라미터로 열림(1) / 닫힘(-1) 제어.
///
/// Mode 별 트리거 조건:
/// - Generic         : 모든 손님/직원 근접 (단순 거리 기반)
/// - ToiletEntrance  : 화장실 이용 의도 있는 손님 (WALK_TO_TOILET / USING_TOILET / 화장실에서 LEAVE 중)
/// - Stall           : 특정 변기/세면대 칸의 사용자 (target 매칭 또는 칸 안에 위치 + USING_TOILET 아님)
/// </summary>
public class ProximityDoor : MonoBehaviour
{
    public enum DoorMode { Generic, ToiletEntrance, Stall }

    [SerializeField] private Animator animator;
    [SerializeField] private float openRadius = 1.5f;
    [SerializeField] private float closeDelay = 1.0f;

    [SerializeField] private DoorMode mode = DoorMode.Generic;
    [SerializeField] private bool triggerByCustomer = true;
    [SerializeField] private bool triggerByStaff = false;

    [Header("Stall 모드 전용 (이 칸의 변기/세면대 셀 좌표)")]
    [SerializeField] private bool useStallToilet;
    [SerializeField] private Vector2Int stallToiletCell;
    [SerializeField] private bool useStallSink;
    [SerializeField] private Vector2Int stallSinkCell;

    private Toilet _cachedToilet;
    private Sink _cachedSink;

    private bool _open;
    private float _closeTimer;

    private Toilet GetStallToilet()
    {
        if (!useStallToilet) return null;
        if (_cachedToilet != null) return _cachedToilet;
        var cell = GridManager.Instance?.GetCell(stallToiletCell);
        if (cell?.placedObject?.Instance != null)
            _cachedToilet = cell.placedObject.Instance.GetComponentInChildren<Toilet>();
        return _cachedToilet;
    }

    private Sink GetStallSink()
    {
        if (!useStallSink) return null;
        if (_cachedSink != null) return _cachedSink;
        var cell = GridManager.Instance?.GetCell(stallSinkCell);
        if (cell?.placedObject?.Instance != null)
            _cachedSink = cell.placedObject.Instance.GetComponentInChildren<Sink>();
        return _cachedSink;
    }

    private void Update()
    {
        bool near = false;

        if (triggerByCustomer && CustomerManager.Instance != null)
        {
            foreach (var c in CustomerManager.Instance.ActiveCustomers)
            {
                if (c == null || !c.gameObject.activeSelf) continue;
                if (!CustomerMatchesMode(c)) continue;
                if (Vector3.Distance(c.transform.position, transform.position) <= openRadius)
                { near = true; break; }
            }
        }
        if (!near && triggerByStaff && StaffManager.Instance != null)
        {
            foreach (var s in StaffManager.Instance.GetAllStaffs())
            {
                if (s == null || !s.gameObject.activeSelf) continue;
                if (Vector3.Distance(s.transform.position, transform.position) <= openRadius)
                { near = true; break; }
            }
        }

        if (near)
        {
            _closeTimer = closeDelay;
            if (!_open) { _open = true; SetSpeed(1f); }
        }
        else if (_open)
        {
            _closeTimer -= Time.deltaTime;
            if (_closeTimer <= 0f) { _open = false; SetSpeed(-1f); }
        }
        ClampAtEnds();
    }

    private bool CustomerMatchesMode(Customer c)
    {
        switch (mode)
        {
            case DoorMode.Generic:
                return true;

            case DoorMode.ToiletEntrance:
                if (GridManager.Instance == null) return false;
                // 들어가는 중: WALK_TO_TOILET + Stall phase (변기칸 향하는 처음 진입)
                if (c.State == CustomerState.WALK_TO_TOILET && c.Phase == Customer.ToiletPhase.Stall) return true;
                // 나가는 중: LEAVE/WALK_TO_STAIR + 화장실 zone 안
                if (c.State == CustomerState.LEAVE || c.State == CustomerState.WALK_TO_STAIR)
                {
                    var cellC = GridManager.Instance.WorldToCell(c.transform.position);
                    var cellInfo = GridManager.Instance.GetCell(cellC);
                    return cellInfo != null && cellInfo.zone == CellZone.Floor2_Toilet;
                }
                return false;

            case DoorMode.Stall:
                if (c.State == CustomerState.USING_TOILET) return false;  // 사용 중엔 무시
                var t = GetStallToilet();
                var s = GetStallSink();
                if (t != null && c.TargetToilet == t) return true;
                if (s != null && c.TargetSink == s) return true;
                // 사용 끝나고 칸 밖으로 빠져나오는 동안: 손님 현재 셀이 stall 가구 셀과 같음
                if (GridManager.Instance != null)
                {
                    var cellC = GridManager.Instance.WorldToCell(c.transform.position);
                    if (useStallToilet && cellC == stallToiletCell) return true;
                    if (useStallSink && cellC == stallSinkCell) return true;
                }
                return false;

            default:
                return false;
        }
    }

    private void ClampAtEnds()
    {
        if (animator == null) return;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        float currentSpeed = animator.GetFloat("Speed");
        if (currentSpeed < 0f && info.normalizedTime <= 0f)
        { animator.Play(info.fullPathHash, 0, 0f); SetSpeed(0f); }
        else if (currentSpeed > 0f && info.normalizedTime >= 1f)
        { animator.Play(info.fullPathHash, 0, 1f); SetSpeed(0f); }
    }

    private void SetSpeed(float v) { if (animator != null) animator.SetFloat("Speed", v); }
}
