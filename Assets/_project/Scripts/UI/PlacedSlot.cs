using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 패널의 슬롯 한 칸.
/// 1차 클릭(slotButton) → 패널에 "내가 선택됐다" 통보 → 체크 버튼 오버레이 표시
/// 2차 클릭(confirmButton) → 실제로 PlacementSystem.StartPlace 호출 + 선택 해제
/// 선택 해제는 (a) 다른 슬롯 선택, (b) 확정 후 자동, 두 경로만.
///
/// displayName은 LocalizedString — 로케일 변경 시 자동 갱신.
/// </summary>
public class PlacedSlot : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;   // 옵션 — 비워두면 표시 안함

    [Header("선택/확정")]
    [Tooltip("슬롯 전체에 깔린 1차 클릭 버튼")]
    [SerializeField] private Button slotButton;
    [Tooltip("선택됐을 때만 보이는 체크 오버레이 (기본 비활성)")]
    [SerializeField] private GameObject checkOverlay;
    [Tooltip("체크 오버레이 내부의 확정 버튼 — 누르면 배치 모드 진입")]
    [SerializeField] private Button confirmButton;

    private FurnitureData _data;
    private PlacementSystem _placement;
    private PlacedShopPanel _panel;
    private bool _isSelected;

    public FurnitureData Data => _data;
    public bool IsSelected => _isSelected;

    public void Setup(FurnitureData data, PlacementSystem placement, PlacedShopPanel panel)
    {
        // 이전 데이터의 LocalizedString 구독 해제
        UnsubscribeDisplayName();

        _data = data;
        _placement = placement;
        _panel = panel;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (nameText != null)
        {
            if (data.displayName != null && !data.displayName.IsEmpty)
            {
                data.displayName.StringChanged += OnDisplayNameChanged;
                data.displayName.RefreshString();
            }
            else
            {
                nameText.text = data.name; // 폴백: 에셋 이름
            }
        }

        if (priceText != null)
            priceText.text = data.purchaseCost > 0 ? $"{data.purchaseCost:N0}원" : "무료";

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        SetSelected(false);
    }

    private void OnDisplayNameChanged(string s)
    {
        if (nameText != null) nameText.text = s;
    }

    private void UnsubscribeDisplayName()
    {
        if (_data != null && _data.displayName != null)
            _data.displayName.StringChanged -= OnDisplayNameChanged;
    }

    private void OnDestroy()
    {
        UnsubscribeDisplayName();
    }

    private void OnSlotClicked()
    {
        if (_panel == null || _isSelected) return;  // 이미 선택된 상태면 무시
        _panel.SelectSlot(this);
    }

    private void OnConfirmClicked()
    {
        if (_panel != null) _panel.ConfirmSlot(this);
    }

    /// <summary>패널에서 호출 — 단일 선택 보장용.</summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (checkOverlay != null) checkOverlay.SetActive(selected);
    }
}
