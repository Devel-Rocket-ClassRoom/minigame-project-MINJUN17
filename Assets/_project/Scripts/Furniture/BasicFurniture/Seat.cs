using UnityEngine;

public class Seat : MonoBehaviour
{
    private bool _isOccupied;
    public bool IsOccupied => _isOccupied;
    public void Occupy() => _isOccupied = true;
    public void Release() => _isOccupied = false;

    [Tooltip("음식이 놓일 테이블 위 위치. 비워두면 Awake에서 부모 자식 중 'FoodDropOff' 이름을 자동 탐색")]
    [SerializeField] private Transform foodDropOff;
    public Transform FoodDropOff => foodDropOff != null ? foodDropOff : transform;

    [Tooltip("손님이 실제로 앉을 위치. 비워두면 좌석이 속한 셀 중앙으로 이동(기존 동작). " +
             "2인용 등 한 가구에 좌석이 여러 개일 때 각 자리를 정확히 지정하려면 여기에 위치를 연결")]
    [SerializeField] private Transform sitPoint;
    /// <summary>지정된 착석 위치. null이면 호출측에서 셀 중앙 폴백.</summary>
    public Transform SitPoint => sitPoint;

    private void Awake()
    {
        // 1순위: 인스펙터에서 명시적으로 드래그한 dropOff 사용
        // 2순위: 부모 GameObject 자식 중 "FoodDropOff" 이름 자동 탐색
        // 3순위: 자기 자신 (단독 의자 폴백)
        if (foodDropOff == null && transform.parent != null)
            foodDropOff = transform.parent.Find("FoodDropOff");

        SeatManager.Instance.RegisterSeat(this);
    }

    private void OnDestroy()
    {
        if (SeatManager.Instance != null)
            SeatManager.Instance.UnregisterSeat(this);
    }
}
