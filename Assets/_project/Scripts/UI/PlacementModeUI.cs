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

    [Header("설치 중 버튼군 (설치, 취소)")]
    [SerializeField] private GameObject placeButtons;

    private void Awake() => ShowNormal();

    /// <summary>PlacedShopPanel.ConfirmSlot에서 호출 — Place 모드 진입 직후 UI 전환.</summary>
    public void ShowPlace()
    {
        if (normalButtons != null) normalButtons.SetActive(false);
        if (placeButtons  != null) placeButtons.SetActive(true);
    }

    public void ShowNormal()
    {
        if (normalButtons != null) normalButtons.SetActive(true);
        if (placeButtons  != null) placeButtons.SetActive(false);
    }

    /// <summary>설치 버튼 OnClick — 확정 성공해서 모드가 풀리면 원래 UI로 복귀, 실패면 UI 유지.</summary>
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
