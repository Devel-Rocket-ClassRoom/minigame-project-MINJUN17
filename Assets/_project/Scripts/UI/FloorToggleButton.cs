using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloorToggleButton : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CameraController cameraController;

    [Header("비주얼")]
    [SerializeField] private Image bgImage;
    [SerializeField] private RectTransform innerRect;   // 위아래로 움직일 내부 이미지
    [SerializeField] private TextMeshProUGUI label;

    [Header("1층 설정")]
    [SerializeField] private Sprite bgFloor1;
    [SerializeField] private string labelFloor1 = "1층";

    [Header("2층 설정")]
    [SerializeField] private Sprite bgFloor2;
    [SerializeField] private string labelFloor2 = "2층";

    [Header("토글 위치")]
    [SerializeField] private float posFloor1Y = -20f;   // 1층일 때 inner Y
    [SerializeField] private float posFloor2Y =  20f;   // 2층일 때 inner Y
    [SerializeField] private float slideDuration = 0.15f;

    private Coroutine _slideCoroutine;

    public void Toggle()
    {
        cameraController.ToggleFloor();
        Refresh(animated: true);
    }

    private void Start()
    {
        // 2층 확장 시점에 버튼을 노출. (비활성 상태여도 델리게이트는 살아있어 콜백 수신됨)
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded += OnExpanded;

        // 층 전환(연출/디버그/DT 해금 등) 시마다 비주얼 동기화 — 확장 연출의 강제 이동과 표시 desync 방지
        if (cameraController != null)
            cameraController.OnFloorChanged += HandleFloorChanged;

        // 세이브 로드(SaveLoadManager, 실행순서 -50)가 끝난 뒤 호출되므로 복원된 해금 상태도 반영됨.
        UpdateVisibility();
    }

    private void OnDestroy()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded -= OnExpanded;

        if (cameraController != null)
            cameraController.OnFloorChanged -= HandleFloorChanged;
    }

    private void OnExpanded(ExpansionStageData _) => UpdateVisibility();

    // 비활성 상태에서도 안전하게 (애니메이션 코루틴 미사용)
    private void HandleFloorChanged(FloorIndex _) => Refresh(animated: false);

    /// <summary>2층이 해금(활성 셀 존재)됐을 때만 버튼 노출.</summary>
    private void UpdateVisibility()
    {
        bool unlocked = GridManager.Instance != null
                     && GridManager.Instance.GetActiveBoundsForFloor(FloorIndex.Floor2).HasValue;

        if (gameObject.activeSelf != unlocked)
            gameObject.SetActive(unlocked);
    }

    private void OnEnable() => Refresh(animated: false);

    private void Refresh(bool animated)
    {
        bool isFloor1 = cameraController == null
                     || cameraController.CurrentFloor == FloorIndex.Floor1;

        if (bgImage != null) bgImage.sprite = isFloor1 ? bgFloor1 : bgFloor2;
        if (label   != null) label.text     = isFloor1 ? labelFloor1 : labelFloor2;

        float targetY = isFloor1 ? posFloor1Y : posFloor2Y;

        if (innerRect == null) return;

        if (animated)
        {
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideTo(targetY));
        }
        else
        {
            var pos = innerRect.anchoredPosition;
            pos.y = targetY;
            innerRect.anchoredPosition = pos;
        }
    }

    private IEnumerator SlideTo(float targetY)
    {
        float startY = innerRect.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            var pos = innerRect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, t);
            innerRect.anchoredPosition = pos;
            yield return null;
        }

        var finalPos = innerRect.anchoredPosition;
        finalPos.y = targetY;
        innerRect.anchoredPosition = finalPos;
    }
}
