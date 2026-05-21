using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public enum Mode { None, Place, Move, Remove }

public class PlacementSystem : MonoBehaviour
{
    public Mode Mode { get; private set; } = Mode.None;

    [SerializeField] private Camera cam;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color removeColor = new Color(1f, 0f, 0f, 0.5f);

    // Place / Move 공통: 드래그 중인 preview
    private FurnitureData _previewData;
    private GameObject _previewInstance;
    private SpriteRenderer[] _previewRenderers;   // 부모 + 자식 SpriteRenderer 전부 (TableSet 같이 자식에만 sprite 있는 케이스 지원)
    private Vector2Int _currentOrigin;
    private int _previewRotationStep;

    // Move 전용: 이동 중인 원본 (취소 시 복원용)
    private PlacedObject _movingOriginal;
    private Vector2Int _originalOrigin;

    // Remove 전용: 선택된 삭제 후보
    private PlacedObject _removeTarget;
    private SpriteRenderer _removeTargetRenderer;
    private Color _removeTargetOriginalColor;

    // 회전을 반영한 preview footprint / anchor (PlacedObject의 계산과 동일)
    private int PreviewWidth  => _previewRotationStep % 2 == 0 ? _previewData.width  : _previewData.height;
    private int PreviewHeight => _previewRotationStep % 2 == 0 ? _previewData.height : _previewData.width;
    private Vector2Int PreviewAnchor
    {
        get
        {
            int w = _previewData.width, h = _previewData.height;
            int ax = _previewData.anchorX, ay = _previewData.anchorY;
            return _previewRotationStep switch
            {
                0 => new Vector2Int(ax, ay),
                1 => new Vector2Int(ay, w - 1 - ax),
                2 => new Vector2Int(w - 1 - ax, h - 1 - ay),
                3 => new Vector2Int(h - 1 - ay, ax),
                _ => Vector2Int.zero
            };
        }
    }

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();

    // ========== Update ==========
    private void Update()
    {
        // UI 위에서의 터치는 전부 무시 (IMGUI 컨트롤과 상호작용 중일 때)
        if (GUIUtility.hotControl != 0) return;

        switch (Mode)
        {
            case Mode.Place:
                DragMove();
                break;
            case Mode.Move:
                if (_movingOriginal == null) TrySelectForMove();
                else DragMove();
                break;
            case Mode.Remove:
                TrySelectForRemove();
                break;
        }
    }
    public void PlaceInitial(FurnitureData data, Vector2Int pos, int rotationStep = 0)
    {
        GameObject instance = Instantiate(
            data.prefab,
            gridManager.CellToWorld(pos, data.width, data.height),
            Quaternion.Euler(0, 0, -90 * rotationStep));

        PlacedObject placed = new PlacedObject(data, instance, pos, rotationStep);
        gridManager.PlaceObject(placed);
    }
    // ========== UI: 진입점 ==========
    public void StartPlace(FurnitureData data)
    {
        if (Mode != Mode.None) return;

        // 활성 영역의 중앙에 spawn (놓을 수 있는지와 무관하게 일단 가운데)
        Vector2Int startOrigin = new Vector2Int(
            (gridManager.StartGridWidth - data.width) / 2,
            (gridManager.StartGridHeight - data.height) / 2
        );
        startOrigin = gridManager.ClampToActiveArea(startOrigin, data.width, data.height);

        GameObject preview = Instantiate(data.prefab);
        StripLogicComponents(preview);
        BeginDragging(data, preview, startOrigin, 0);
        Mode = Mode.Place;
    }

    // Preview용: 매니저 자동등록되는 로직 컴포넌트 즉시 제거
    // (Awake에서 이미 등록은 됐지만, OnDestroy/Unregister가 호출되도록 Destroy 처리)
    private void StripLogicComponents(GameObject preview)
    {
        foreach (var c in preview.GetComponentsInChildren<Counter>(true))            Destroy(c);
        foreach (var p in preview.GetComponentsInChildren<PassWindow>(true))         Destroy(p);
        foreach (var s in preview.GetComponentsInChildren<Seat>(true))               Destroy(s);
        foreach (var t in preview.GetComponentsInChildren<CookingToolInstance>(true)) Destroy(t);
    }

    public void StartMove()
    {
        if (Mode != Mode.None) return;
        Mode = Mode.Move;
    }

    public void StartRemove()
    {
        if (Mode != Mode.None) return;
        Mode = Mode.Remove;
    }

    // Place / Move 모드에서 preview를 90° CW 회전
    public void RotatePreview()
    {
        if (Mode != Mode.Place && Mode != Mode.Move) return;
        if (_previewInstance == null) return; // Move 모드에서 아직 선택 안 한 상태 방어

        _previewRotationStep = (_previewRotationStep + 1) % 4;
        _previewInstance.transform.rotation = Quaternion.Euler(0, 0, -90 * _previewRotationStep);

        // 회전 후 footprint 크기가 바뀌었으므로 origin을 다시 클램프
        _currentOrigin = gridManager.ClampToActiveArea(_currentOrigin, PreviewWidth, PreviewHeight);
        UpdatePreviewVisuals();
    }

    // ========== 모드 내부: 탭으로 오브젝트 선택 ==========
    private void TrySelectForMove()
    {
        if (!TryGetTappedObject(out PlacedObject target)) return;

        _movingOriginal = target;
        _originalOrigin = target.Origin;

        gridManager.RemoveObject(target);
        target.Instance.SetActive(false);

        GameObject preview = Instantiate(target.Data.prefab);
        StripLogicComponents(preview);
        BeginDragging(target.Data, preview, target.Origin, target.RotationStep);
    }

    private void TrySelectForRemove()
    {
        if (!TryGetTappedObject(out PlacedObject target)) return;

        if (_removeTarget != null)
            _removeTargetRenderer.color = _removeTargetOriginalColor;

        _removeTarget = target;
        _removeTargetRenderer = target.Instance.GetComponent<SpriteRenderer>();
        _removeTargetOriginalColor = _removeTargetRenderer.color;
        _removeTargetRenderer.color = removeColor;
    }

    // ========== UI: 통합 확정 ==========
    public void Confirm()
    {
        bool success;
        switch (Mode)
        {
            case Mode.Place:
                success = ApplyPlace();
                break;
            case Mode.Move:
                if (_movingOriginal == null) return; // 선택 안된 상태면 무시
                success = ApplyMove();
                break;
            case Mode.Remove:
                if (_removeTarget == null) return; // 선택 안된 상태면 무시
                success = ApplyRemove();
                break;
            default: return;
        }

        // 실패하면 모드 유지 (사용자가 다시 시도하거나 Cancel을 누르도록)
        if (success) ResetState();
    }

    // ========== UI: 통합 취소 ==========
    public void Cancel()
    {
        switch (Mode)
        {
            case Mode.Place:
                if (_previewInstance != null) Destroy(_previewInstance);
                break;
            case Mode.Move:
                if (_movingOriginal != null)
                {
                    _movingOriginal.Origin = _originalOrigin;
                    gridManager.PlaceObject(_movingOriginal);
                    _movingOriginal.Instance.SetActive(true);
                }
                if (_previewInstance != null) Destroy(_previewInstance);
                break;
            case Mode.Remove:
                if (_removeTarget != null)
                    _removeTargetRenderer.color = _removeTargetOriginalColor;
                break;
            default: return;
        }
        ResetState();
    }

    // ========== 내부 적용 로직 (성공 시 true, 실패 시 false) ==========
    private bool ApplyPlace()
    {
        if (!gridManager.CanPlace(_currentOrigin, PreviewWidth, PreviewHeight, gridManager.GetZone(_currentOrigin))) return false;

        GameObject instance = Instantiate(
            _previewData.prefab,
            gridManager.CellToWorld(_currentOrigin, PreviewWidth, PreviewHeight),
            Quaternion.Euler(0, 0, -90 * _previewRotationStep));

        PlacedObject placed = new PlacedObject(_previewData, instance, _currentOrigin, _previewRotationStep);
        gridManager.PlaceObject(placed);
        Destroy(_previewInstance);
        return true;
    }

    private bool ApplyMove()
    {
        // 못 놓는 위치면 아무것도 하지 않고 false (사용자가 계속 드래그하거나 Cancel)
        if (!gridManager.CanPlace(_currentOrigin, PreviewWidth, PreviewHeight, gridManager.GetZone(_currentOrigin))) return false;

        _movingOriginal.Origin = _currentOrigin;
        _movingOriginal.RotationStep = _previewRotationStep;
        _movingOriginal.Instance.transform.position =
            gridManager.CellToWorld(_currentOrigin, PreviewWidth, PreviewHeight);
        _movingOriginal.Instance.transform.rotation = Quaternion.Euler(0, 0, -90 * _previewRotationStep);
        _movingOriginal.Instance.SetActive(true);
        gridManager.PlaceObject(_movingOriginal);
        Destroy(_previewInstance);
        return true;
    }

    private bool ApplyRemove()
    {
        gridManager.RemoveObject(_removeTarget);
        Destroy(_removeTarget.Instance);
        return true;
    }

    // ========== 드래그 ==========
    private void BeginDragging(FurnitureData data, GameObject preview, Vector2Int startOrigin, int rotationStep)
    {
        _previewData = data;
        _previewInstance = preview;
        _previewRenderers = preview.GetComponentsInChildren<SpriteRenderer>(true);
        _previewRotationStep = rotationStep;
        _currentOrigin = startOrigin;

        _previewInstance.transform.rotation = Quaternion.Euler(0, 0, -90 * rotationStep);
        UpdatePreviewVisuals();
    }

    private void DragMove()
    {
        if (!TryFindCell(out Vector2Int cell)) return;

        Vector2Int rawOrigin = cell - PreviewAnchor;
        _currentOrigin = gridManager.ClampToActiveArea(rawOrigin, PreviewWidth, PreviewHeight);

        UpdatePreviewVisuals();
    }

    private void UpdatePreviewVisuals()
    {
        _previewInstance.transform.position =
            gridManager.CellToWorld(_currentOrigin, PreviewWidth, PreviewHeight);
        Color color = gridManager.CanPlace(_currentOrigin, PreviewWidth, PreviewHeight, gridManager.GetZone(_currentOrigin))
            ? validColor : invalidColor;
        if (_previewRenderers != null)
        {
            foreach (var sr in _previewRenderers)
                if (sr != null) sr.color = color;
        }
    }

    // ========== 유틸 ==========
    private bool TryFindCell(out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        var touches = Touch.activeTouches;
        if (touches.Count == 0) return false;

        // UI 버튼을 떼고난 직후 한 프레임 동안 Ended 상태로 남는 터치가
        // 버튼의 화면 좌표를 그리드 셀로 변환해버리는 걸 방지
        Touch touch = touches[0];
        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) return false;

        // Canvas UI 위에서 발생한 터치는 무시 (버튼 클릭이 그리드 좌표로 변환 안되게)
        if (IsPointerOverUI(touch)) return false;

        Vector3 worldPos = cam.ScreenToWorldPoint(touch.screenPosition);
        cell = gridManager.WorldToCell(worldPos);
        return true;
    }

    private static readonly List<RaycastResult> _uiRaycastResults = new();
    private bool IsPointerOverUI(Touch touch)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = touch.screenPosition };
        _uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(data, _uiRaycastResults);
        return _uiRaycastResults.Count > 0;
    }

    // 새 탭(Began 프레임) + 그 위치에 배치된 오브젝트가 있을 때만 반환
    private bool TryGetTappedObject(out PlacedObject placed)
    {
        placed = null;
        var touches = Touch.activeTouches;
        if (touches.Count == 0) return false;
        if (touches[0].phase != TouchPhase.Began) return false;

        // Canvas UI 위 탭은 무시
        if (IsPointerOverUI(touches[0])) return false;

        Vector3 worldPos = cam.ScreenToWorldPoint(touches[0].screenPosition);
        Vector2Int cellPos = gridManager.WorldToCell(worldPos);
        GridCell cell = gridManager.GetCell(cellPos);
        if (cell == null || cell.placedObject == null) return false;

        placed = cell.placedObject;
        return true;
    }

    private void ResetState()
    {
        _previewData = null;
        _previewInstance = null;
        _previewRenderers = null;
        _previewRotationStep = 0;
        _movingOriginal = null;
        _removeTarget = null;
        _removeTargetRenderer = null;
        Mode = Mode.None;
    }
}
