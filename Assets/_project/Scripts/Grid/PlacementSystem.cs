using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public enum Mode { None, Place, Move, Remove }

public class PlacementSystem : MonoBehaviour
{
    public Mode Mode { get; private set; } = Mode.None;

    [SerializeField] private new Camera camera;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color removeColor = new Color(1f, 0f, 0f, 0.5f);

    // Place / Move 공통: 드래그 중인 preview
    private FurnitureData _previewData;
    private GameObject _previewInstance;
    private SpriteRenderer _previewRenderer;
    private Vector2Int _currentOrigin;

    // Move 전용: 이동 중인 원본 (취소 시 복원용)
    private PlacedObject _movingOriginal;
    private Vector2Int _originalOrigin;

    // Remove 전용: 선택된 삭제 후보
    private PlacedObject _removeTarget;
    private SpriteRenderer _removeTargetRenderer;
    private Color _removeTargetOriginalColor;

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
        BeginDragging(data, preview, startOrigin);
        Mode = Mode.Place;
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

    // ========== 모드 내부: 탭으로 오브젝트 선택 ==========
    private void TrySelectForMove()
    {
        if (!TryGetTappedObject(out PlacedObject target)) return;

        _movingOriginal = target;
        _originalOrigin = target.Origin;

        gridManager.RemoveObject(target);
        target.Instance.SetActive(false);

        GameObject preview = Instantiate(target.Data.prefab);
        BeginDragging(target.Data, preview, target.Origin);
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
        switch (Mode)
        {
            case Mode.Place: ApplyPlace(); break;
            case Mode.Move:
                if (_movingOriginal == null) return; // 선택 안된 상태면 무시
                ApplyMove();
                break;
            case Mode.Remove:
                if (_removeTarget == null) return; // 선택 안된 상태면 무시
                ApplyRemove();
                break;
            default: return;
        }
        ResetState();
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

    // ========== 내부 적용 로직 ==========
    private void ApplyPlace()
    {
        if (!gridManager.CanPlace(_currentOrigin, _previewData.width, _previewData.height)) return;

        GameObject instance = Instantiate(
            _previewData.prefab,
            gridManager.CellToWorld(_currentOrigin, _previewData.width, _previewData.height),
            Quaternion.identity);

        PlacedObject placed = new PlacedObject(_previewData, instance, _currentOrigin);
        gridManager.PlaceObject(placed);
        Destroy(_previewInstance);
    }

    private void ApplyMove()
    {
        if (!gridManager.CanPlace(_currentOrigin, _previewData.width, _previewData.height))
        {
            // 못 놓으면 원래 자리로 복원
            _movingOriginal.Origin = _originalOrigin;
            gridManager.PlaceObject(_movingOriginal);
            _movingOriginal.Instance.SetActive(true);
            Destroy(_previewInstance);
            return;
        }

        _movingOriginal.Origin = _currentOrigin;
        _movingOriginal.Instance.transform.position =
            gridManager.CellToWorld(_currentOrigin, _previewData.width, _previewData.height);
        _movingOriginal.Instance.SetActive(true);
        gridManager.PlaceObject(_movingOriginal);
        Destroy(_previewInstance);
    }

    private void ApplyRemove()
    {
        if (_removeTarget == null) return;

        gridManager.RemoveObject(_removeTarget);
        Destroy(_removeTarget.Instance);
    }

    // ========== 드래그 ==========
    private void BeginDragging(FurnitureData data, GameObject preview, Vector2Int startOrigin)
    {
        _previewData = data;
        _previewInstance = preview;
        _previewRenderer = preview.GetComponent<SpriteRenderer>();
        _currentOrigin = startOrigin;

        _previewInstance.transform.position = gridManager.CellToWorld(startOrigin, data.width, data.height);
        _previewRenderer.color = gridManager.CanPlace(startOrigin, data.width, data.height) ? validColor : invalidColor;
    }

    private void DragMove()
    {
        if (!TryFindCell(out Vector2Int cell)) return;

        Vector2Int rawOrigin = cell - new Vector2Int(_previewData.anchorX, _previewData.anchorY);
        _currentOrigin = gridManager.ClampToActiveArea(rawOrigin, _previewData.width, _previewData.height);

        _previewInstance.transform.position =
            gridManager.CellToWorld(_currentOrigin, _previewData.width, _previewData.height);
        _previewRenderer.color =
            gridManager.CanPlace(_currentOrigin, _previewData.width, _previewData.height) ? validColor : invalidColor;
    }

    // ========== 유틸 ==========
    private bool TryFindCell(out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        if (Touch.activeTouches.Count == 0) return false;
        Vector3 worldPos = camera.ScreenToWorldPoint(Touch.activeTouches[0].screenPosition);
        cell = gridManager.WorldToCell(worldPos);
        return true;
    }

    // 새 탭(Began 프레임) + 그 위치에 배치된 오브젝트가 있을 때만 반환
    private bool TryGetTappedObject(out PlacedObject placed)
    {
        placed = null;
        var touches = Touch.activeTouches;
        if (touches.Count == 0) return false;
        if (touches[0].phase != TouchPhase.Began) return false;

        Vector3 worldPos = camera.ScreenToWorldPoint(touches[0].screenPosition);
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
        _previewRenderer = null;
        _movingOriginal = null;
        _removeTarget = null;
        _removeTargetRenderer = null;
        Mode = Mode.None;
    }
}
