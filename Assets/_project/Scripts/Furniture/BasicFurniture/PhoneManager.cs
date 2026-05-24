using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class PhoneManager : MonoBehaviour
{
    public static PhoneManager Instance;

    [Header("카탈로그 식별")]
    [Tooltip("전화기 가구 데이터 — CatalogManager에서 이게 해금되면 IsUnlocked=true")]
    [SerializeField] private FurnitureData phoneCatalogData;

    [Header("ring 룰")]
    [SerializeField] private float ringTimeout = 10f;   // 서버가 받지 못하면 폐기

    [Header("배달 메뉴 수")]
    [SerializeField] private int minOrderCount = 1;
    [SerializeField] private int maxOrderCount = 3;

    [Header("영업시간 차단")]
    [SerializeField] private int newCallCutoffHour = 22;   // 이 시각 이후 새 콜 차단
    [SerializeField] private TimeSystem timeSystem;

    private readonly List<Phone> phones = new();
    private float _nextCallTimer;
    private int _activeDeliveryCount;   // ring + cooking + delivering 합

    public event Action OnUnlocked;            // CatalogManager 프록시 (외부 호환)
    public event Action OnInstalled;
    public event Action OnUninstalled;

    public FurnitureData PhoneCatalogData => phoneCatalogData;
    public bool IsUnlocked => CatalogManager.Instance != null
                              && CatalogManager.Instance.IsUnlocked(phoneCatalogData);
    public bool HasInstalledPhone => phones.Count > 0;
    public bool IsRiderHiringUnlocked => HasInstalledPhone;
    public int ActiveDeliveryCount => _activeDeliveryCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (CatalogManager.Instance != null)
            CatalogManager.Instance.OnFurnitureUnlocked += HandleFurnitureUnlocked;
    }

    private void OnDestroy()
    {
        if (CatalogManager.Instance != null)
            CatalogManager.Instance.OnFurnitureUnlocked -= HandleFurnitureUnlocked;
    }

    private void HandleFurnitureUnlocked(FurnitureData f)
    {
        if (f == phoneCatalogData) OnUnlocked?.Invoke();
    }

    public void Register(Phone p)
    {
        if (phones.Contains(p)) return;
        phones.Add(p);
        _nextCallTimer = RollCallInterval(p);
        if (phones.Count == 1) OnInstalled?.Invoke();
    }

    public void Unregister(Phone p)
    {
        if (!phones.Remove(p)) return;
        if (p.IsRinging) p.StopRinging();
        if (phones.Count == 0) OnUninstalled?.Invoke();
    }

    private float RollCallInterval(Phone p)
    {
        return Random.Range(p.MinCallTimer, p.MaxCallTimer);
    }

    private void Update()
    {
        if (!HasInstalledPhone) return;

        var phone = phones[0];

        // ring 타임아웃: 서버 못 받음 → 폐기 (페널티 없음)
        if (phone.IsRinging && phone.RingTimer >= ringTimeout)
        {
            phone.StopRinging();
            _activeDeliveryCount = Mathf.Max(0, _activeDeliveryCount - 1);
        }

        // 새 콜 발생 조건
        if (!phone.IsRinging && CanGenerateNewCall())
        {
            _nextCallTimer -= Time.deltaTime;
            if (_nextCallTimer <= 0f)
            {
                phone.StartRinging();
                _activeDeliveryCount++;
                _nextCallTimer = RollCallInterval(phone);
            }
        }
    }

    private bool CanGenerateNewCall()
    {
        if (StaffManager.Instance == null) return false;
        int riderCount = StaffManager.Instance.RiderCount;
        if (riderCount <= 0) return false;
        if (timeSystem != null && !timeSystem.IsOpen) return false;
        if (timeSystem != null && timeSystem.Hour >= newCallCutoffHour) return false;
        if (_activeDeliveryCount >= riderCount * 2) return false;
        return true;
    }

    // 서버가 ring을 받아서 Order로 변환 (FIFO: 단일 폰 가정이므로 첫 폰)
    public Phone GetRingingPhone()
    {
        foreach (var p in phones)
            if (p.IsRinging) return p;
        return null;
    }

    public bool HasRingingPhone() => GetRingingPhone() != null;

    public Vector3 GetPhoneApproachPosition(PathRole role, Vector3 from)
    {
        if (phones.Count == 0) return Vector3.zero;
        return GridManager.Instance.GetFurnitureApproachPosition(phones[0].transform.position, role, from);
    }

    // 서버가 도착해서 ring 받은 시점에 호출 → 배달 Order 생성하여 PassWindow에 제출
    public Order AcceptCall(Phone phone)
    {
        if (phone == null || !phone.IsRinging) return null;
        phone.StopRinging();

        int orderCount = Random.Range(minOrderCount, maxOrderCount + 1);
        var menus = new List<MenuData>();
        for (int i = 0; i < orderCount; i++)
            menus.Add(MenuManager.Instance.PickRandomByWeight());

        // ★ 배달 매출/판매 통계 기록 (전화 응대 시점)
        int totalPrice = 0;
        foreach (var menu in menus)
        {
            totalPrice += menu.price;
            SalesTracker.Instance.RecordSale(menu);
        }
        MoneySystem.Instance.Earn(totalPrice);

        var order = new Order
        {
            customer = null,
            menus = menus,
            type = OrderType.Delivery,
        };

        PassWindowManager.Instance.SubmitOrder(order);
        return order;
    }

    // 라이더가 exitPoint 도달 → 한 건 완료 처리
    public void OnDeliveryCompleted()
    {
        _activeDeliveryCount = Mathf.Max(0, _activeDeliveryCount - 1);
    }
}
