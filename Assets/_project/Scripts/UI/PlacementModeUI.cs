using TMPro;
using UnityEngine;

/// <summary>
/// 설치 모드 진입/종료에 맞춰 하단 메뉴 버튼군을 토글한다.
/// 평소: 저장/메뉴 노출, 설치/취소 숨김
/// 설치 중: 저장/메뉴 숨김, 설치/취소 노출
/// </summary>
public class PlacementModeUI : MonoBehaviour
{
    [SerializeField] private PlacementSystem placementSystem;

    [Header("평소 버튼군 (저장, 메뉴)")]
    [SerializeField] private GameObject normalButtons;

    [Header("설치/이동 중 버튼군 (설치, 취소)")]
    [SerializeField] private GameObject placeButtons;

    [Header("제거 중 버튼군 (제거, 취소)")]
    [SerializeField] private GameObject removeButtons;

    [Header("설치 중 숨길 HUD (CanvasGroup — GameObject 끄지 않아 이벤트 구독 유지)")]
    [SerializeField] private CanvasGroup hudGroup;

    [Header("설치 중에도 유지할 층전환 토글 (hudGroup 바깥에 배치해야 함)")]
    [SerializeField] private GameObject floorToggle;

    [Header("모드 안내 텍스트 (이동/제거 시 대상 선택 안내)")]
    [SerializeField] private GameObject hintRoot;          // 안내 텍스트 루트 (없으면 미사용)
    [SerializeField] private TextMeshProUGUI hintLabel;
    [SerializeField] private string moveHintText   = "이동할 가구를 선택해 주세요";
    [SerializeField] private string removeHintText = "제거할 가구를 선택해주세요";

    private void Awake()
    {
        ShowNormal();
        if (placementSystem != null) placementSystem.OnSelectionStarted += HideHint;
    }

    private void OnDestroy()
    {
        if (placementSystem != null) placementSystem.OnSelectionStarted -= HideHint;
    }

    private void ShowHint(string text)
    {
        if (hintLabel != null) hintLabel.text = text;
        if (hintRoot  != null) hintRoot.SetActive(true);
    }

    private void HideHint()
    {
        if (hintRoot != null) hintRoot.SetActive(false);
    }

    private void SetHud(bool visible)
    {
        if (hudGroup == null) return;
        hudGroup.alpha = visible ? 1f : 0f;
        hudGroup.interactable = visible;
        hudGroup.blocksRaycasts = visible;
    }

    /// <summary>층전환 토글은 설치 중에도 유지 (2층 해금 시에만 표시). hudGroup 바깥에 있어야 가려지지 않음.</summary>
    private void RefreshFloorToggle()
    {
        if (floorToggle == null) return;
        bool unlocked = GridManager.Instance != null
            && GridManager.Instance.GetActiveBoundsForFloor(FloorIndex.Floor2).HasValue;
        floorToggle.SetActive(unlocked);
    }

    /// <summary>PlacedShopPanel.ConfirmSlot에서 호출 — Place/이동 모드 진입 직후 UI 전환 (설치/취소 버튼).</summary>
    public void ShowPlace()
    {
        if (normalButtons != null) normalButtons.SetActive(false);
        if (placeButtons  != null) placeButtons.SetActive(true);
        if (removeButtons != null) removeButtons.SetActive(false);
        SetHud(false);
        RefreshFloorToggle();
    }

    /// <summary>제거 모드 진입 직후 UI 전환 (제거/취소 버튼).</summary>
    public void ShowRemove()
    {
        if (normalButtons != null) normalButtons.SetActive(false);
        if (placeButtons  != null) placeButtons.SetActive(false);
        if (removeButtons != null) removeButtons.SetActive(true);
        SetHud(false);
        RefreshFloorToggle();
    }

    public void ShowNormal()
    {
        if (normalButtons != null) normalButtons.SetActive(true);
        if (placeButtons  != null) placeButtons.SetActive(false);
        if (removeButtons != null) removeButtons.SetActive(false);
        SetHud(true);
        HideHint();
    }

    /// <summary>이동 버튼 OnClick — 이동 모드 진입 (탭으로 대상 선택 후 드래그 → 설치 버튼으로 확정).</summary>
    public void OnMoveClicked()
    {
        if (placementSystem == null) return;
        placementSystem.StartMove();
        ShowPlace();   // 설치/취소 버튼 노출 + HUD 숨김
        ShowHint(moveHintText);
    }

    /// <summary>제거 버튼 OnClick — 삭제 모드 진입 (탭으로 대상 선택 후 설치 버튼으로 확정).</summary>
    public void OnRemoveClicked()
    {
        if (placementSystem == null) return;
        placementSystem.StartRemove();
        ShowRemove();
        ShowHint(removeHintText);
    }

    /// <summary>설치/제거 확정 버튼 OnClick — 확정 성공해서 모드가 풀리면 원래 UI로 복귀, 실패면 UI 유지.</summary>
    public void OnInstallClicked()
    {
        if (placementSystem == null) return;
        placementSystem.Confirm();
        if (placementSystem.Mode == Mode.None) ShowNormal();
    }

    /// <summary>취소 버튼 OnClick — 무조건 복귀.</summary>
    public void OnCancelClicked()
    {
        if (placementSystem == null) return;
        placementSystem.Cancel();
        ShowNormal();
    }
}
