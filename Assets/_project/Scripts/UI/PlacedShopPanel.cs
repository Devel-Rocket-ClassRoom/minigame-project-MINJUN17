using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 탭에 해당하는 상점 패널. 지정한 PlacementZone 의 해금된 가구를 슬롯으로 채운다.
/// CatalogManager.OnFurnitureUnlocked 구독해서 해금되는 즉시 슬롯 추가.
/// </summary>
public class PlacedShopPanel : MonoBehaviour
{
    [Header("이 패널이 보여줄 카테고리 (시작값)")]
    [SerializeField] private PlacementZone zone;

    [Header("슬롯 설정")]
    [SerializeField] private PlacedSlot slotPrefab;
    [SerializeField] private RectTransform content;          // 슬롯들이 들어갈 부모 (보통 ScrollView Content)
    [SerializeField] private PlacementSystem placementSystem;

    [Header("창 루트 (비우면 이 GameObject)")]
    [SerializeField] private GameObject windowRoot;

    [Header("설치 모드 진입 시 토글할 하단 UI")]
    [SerializeField] private PlacementModeUI placementModeUI;

    private readonly List<PlacedSlot> _spawned = new();
    private PlacedSlot _selectedSlot;

    public PlacementZone Zone => zone;

    /// <summary>탭 버튼 OnClick에서 PlacementZone enum 값으로 호출 (UI Button은 int까지만 지원하므로 int 오버로드도 제공).</summary>
    public void SetZone(PlacementZone newZone)
    {
        if (zone == newZone && _spawned.Count > 0) return;
        zone = newZone;
        Refresh();
    }

    /// <summary>탭 버튼 OnClick에서 int로 직접 연결할 때 (PlacementZone 값에 맞는 정수 사용).</summary>
    public void SetZone(int zoneInt) => SetZone((PlacementZone)zoneInt);

    private void OnEnable()
    {
        Refresh();
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked += OnFurnitureUnlocked;
        }
    }

    private void OnDisable()
    {
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked -= OnFurnitureUnlocked;
        }
    }

    private void OnFurnitureUnlocked(FurnitureData f)
    {
        if (f != null && f.zone == zone) Refresh();
    }

    public void Refresh()
    {
        // 기존 슬롯 정리
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
        _selectedSlot = null;

        if (CatalogManager.Instance == null || slotPrefab == null || content == null) return;

        foreach (var data in CatalogManager.Instance.UnlockedByZone(zone))
        {
            if (data.fixedSingle) continue;   // 고정 가구(카운터 등)는 추가 설치 불가 — 상점에 노출 X
            if (data.fixedPlacement && placementSystem != null && placementSystem.IsAlreadyPlaced(data)) continue; // 고정 위치 가구는 설치 후 슬롯 숨김
            if (data.singleInstance && placementSystem != null && placementSystem.IsAlreadyPlaced(data)) continue;  // 1개 제한 가구(전화기 등)는 설치 후 슬롯 숨김
            var slot = Instantiate(slotPrefab, content);
            slot.Setup(data, placementSystem, this);
            _spawned.Add(slot);
        }
    }

    /// <summary>설치 모드 진입 직전 등에서 창을 통째로 닫고 싶을 때 호출.</summary>
    public void CloseWindow()
    {
        var root = windowRoot != null ? windowRoot : gameObject;
        root.SetActive(false);
    }

    /// <summary>슬롯 확정 버튼이 호출 — 배치 시작 + 패널 닫기 + 하단 UI 전환을 한 군데서.</summary>
    public void ConfirmSlot(PlacedSlot slot)
    {
        if (slot == null || slot.Data == null || placementSystem == null) return;

        // 돈 부족하면 설치 모드 진입 자체를 막고 실패음 + HUD 흔들림 (실제 차감은 설치 확정 시 — 여기선 차감 X)
        if (MoneySystem.Instance != null && !MoneySystem.Instance.CanAfford(slot.Data.purchaseCost))
        {
            SoundManager.Instance?.PlaySfx(SfxId.PurchaseFail);
            MoneySystem.Instance.NotifyInsufficientFunds();
            SelectSlot(null);
            return;   // 설치모드 진입 X (패널은 열어둠)
        }

        SelectSlot(null);
        CloseWindow();

        if (slot.Data.fixedPlacement)
        {
            placementSystem.InstantPlace(slot.Data);
            Refresh();   // 설치 직후 슬롯 갱신 (중복 설치 방지)
            return;
        }

        placementSystem.StartPlace(slot.Data);
        if (placementModeUI != null) placementModeUI.ShowPlace();
    }

    /// <summary>슬롯이 자기 선택/해제를 알릴 때 호출. 단일 선택을 보장한다 (null이면 전체 해제).</summary>
    public void SelectSlot(PlacedSlot slot)
    {
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.SetSelected(false);

        _selectedSlot = slot;
        if (slot != null) slot.SetSelected(true);
    }
}
