using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드라이브 쓰루 픽업 창구. 직원이 음식 가져다 놓고, 차가 와서 받아감.
/// 차마다 별도 슬롯을 가지므로 음식이 차보다 먼저 여러 개 도착해도 안 꼬임.
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

    // 차별 음식 슬롯 — 음식이 어느 차 소유인지 추적
    private readonly Dictionary<DTCustomer, Food> _placedFoods = new();
    public int PlacedFoodCount => _placedFoods.Count;
    public bool HasReadyFoodFor(DTCustomer car) => car != null && _placedFoods.ContainsKey(car);

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

    public void OnCarArrived(DTCustomer car) { _waitingCar = car; }
    public void OnCarLeft() { _waitingCar = null; }

    /// <summary>ServerStaff가 PassWindow에서 가져온 음식을 여기 놓을 때 호출.</summary>
    public void PlaceFood(Food food)
    {
        if (food == null) return;

        var car = food.order?.dtCustomer;
        if (car == null)
        {
            Debug.LogWarning("[DTPickupWindow] PlaceFood: food.order.dtCustomer가 null — 폐기");
            Destroy(food.gameObject);
            return;
        }

        // 같은 차의 음식이 이미 있다면(비정상) 기존 것 폐기
        if (_placedFoods.TryGetValue(car, out var existing) && existing != null && existing != food)
            Destroy(existing.gameObject);

        _placedFoods[car] = food;
        food.transform.SetParent(FoodDropOff, false);
        food.transform.localPosition = Vector3.zero;
    }

    /// <summary>DTCustomer가 자기 음식 가져갈 때 호출.</summary>
    public Food TakeFoodFor(DTCustomer car)
    {
        if (car == null) return null;
        if (!_placedFoods.TryGetValue(car, out var f)) return null;
        _placedFoods.Remove(car);
        return f;
    }

    /// <summary>차가 destroy될 때 남은 음식 정리(누수 방지).</summary>
    public void ClearFor(DTCustomer car)
    {
        if (car == null) return;
        if (_placedFoods.TryGetValue(car, out var f))
        {
            if (f != null) Destroy(f.gameObject);
            _placedFoods.Remove(car);
        }
    }
}
