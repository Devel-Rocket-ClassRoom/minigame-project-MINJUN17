using UnityEngine;

/// <summary>
/// 드라이브 쓰루 픽업 창구. 직원이 음식 가져다 놓고, 차가 와서 받아감.
/// Seat의 FoodDropOff와 PassWindow의 PlaceFood 패턴을 합친 형태.
/// </summary>
public class DTPickupWindow : MonoBehaviour
{
    [Tooltip("차가 멈춰서 음식 받는 위치")]
    [SerializeField] private Transform carSlot;
    [Tooltip("ServerStaff가 음식 놓으러 오는 위치 (그리드 안쪽)")]
    [SerializeField] private Transform staffPos;
    [Tooltip("음식이 실제로 놓일 Transform. 비워두면 자기 자신 사용")]
    [SerializeField] private Transform foodDropOff;

    public Transform CarSlot => carSlot;
    public Transform StaffPos => staffPos;
    public Transform FoodDropOff => foodDropOff != null ? foodDropOff : transform;

    private DTCustomer _waitingCar;
    public DTCustomer WaitingCar => _waitingCar;
    public bool HasWaitingCar => _waitingCar != null;

    private Food _placedFood;
    public Food PlacedFood => _placedFood;
    public bool HasReadyFood => _placedFood != null;

    private void Awake()
    {
        if (DTWindowManager.Instance != null)
            DTWindowManager.Instance.RegisterPickup(this);
    }

    private void OnDestroy()
    {
        if (DTWindowManager.Instance != null)
            DTWindowManager.Instance.UnregisterPickup(this);
    }

    public void OnCarArrived(DTCustomer car)
    {
        _waitingCar = car;
    }

    public void OnCarLeft()
    {
        _waitingCar = null;
    }

    // ServerStaff가 PassWindow에서 가져온 음식을 여기 놓을 때 호출
    public void PlaceFood(Food food)
    {
        if (food == null) return;
        _placedFood = food;
        food.transform.SetParent(FoodDropOff, false);
        food.transform.localPosition = Vector3.zero;
    }

    // DTCustomer가 음식 가져갈 때 호출 (LEAVE 진입 시점)
    public Food TakeFood()
    {
        var f = _placedFood;
        _placedFood = null;
        return f;
    }
}
