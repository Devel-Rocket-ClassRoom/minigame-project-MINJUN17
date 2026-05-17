using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField] private int _gridWidth = 12;
    [SerializeField] private int _gridHeight = 12;
    [SerializeField] private int _startGridWidth = 6;
    [SerializeField] private int _startGridHeight = 6;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float cameraPadding = 1f;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int StartGridWidth => _startGridWidth;
    public int StartGridHeight => _startGridHeight;

    private GridCell[,] _cells;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        CreateGrid();
        CenterCameraOnActiveGrid();
    }

    // 활성 영역(_startGridWidth × _startGridHeight) 중앙으로 카메라 정렬
    // 활성 영역이 확장될 때마다 다시 호출하면 됨
    public void CenterCameraOnActiveGrid()
    {
        if (mainCamera == null) return;

        float centerX = _startGridWidth / 2f;
        float centerY = _startGridHeight / 2f;

        Vector3 pos = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(centerX, centerY, pos.z);

        if (mainCamera.orthographic)
        {
            float requiredHalfH = _startGridHeight / 2f + cameraPadding;
            float requiredHalfW = (_startGridWidth / 2f + cameraPadding) / mainCamera.aspect;
            mainCamera.orthographicSize = Mathf.Max(requiredHalfH, requiredHalfW);
        }
    }

    private void CreateGrid()
    {
        _cells = new GridCell[_gridWidth, _gridHeight];
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                bool isActive = x < _startGridWidth && y < _startGridHeight;
                _cells[x, y] = new GridCell(x, y, isActive);
            }
        }
    }

    public GridCell GetCell(Vector2Int pos)
    {
        if (!IsInBounds(pos)) return null;
        return _cells[pos.x, pos.y];
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
    }

    // 가구 footprint(width × height)가 활성 영역 안에 들어오도록 origin을 클램프
    public Vector2Int ClampToActiveArea(Vector2Int origin, int width, int height)
    {
        int maxX = Mathf.Max(0, _startGridWidth - width);
        int maxY = Mathf.Max(0, _startGridHeight - height);
        return new Vector2Int(
            Mathf.Clamp(origin.x, 0, maxX),
            Mathf.Clamp(origin.y, 0, maxY)
        );
    }

    public Vector3 CellToWorld(Vector2Int pos, int width = 1, int height = 1)
    {
        return new Vector3(pos.x + width * 0.5f, pos.y + height * 0.5f, 0);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
    }

    public bool CanPlace(Vector2Int origin, int width, int height)
    {
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                GridCell cell = GetCell(origin + new Vector2Int(dx, dy));
                if (cell == null || cell.isOccupied || !cell.isActive) return false;
            }
        }
        return true;
    }

    public void PlaceObject(PlacedObject placed)
    {
        for (int dx = 0; dx < placed.Width; dx++)
        {
            for (int dy = 0; dy < placed.Height; dy++)
            {
                GridCell cell = GetCell(placed.Origin + new Vector2Int(dx, dy));
                cell.isOccupied = true;
                cell.placedObject = placed;
            }
        }
    }

    public void RemoveObject(PlacedObject placed)
    {
        for (int dx = 0; dx < placed.Width; dx++)
        {
            for (int dy = 0; dy < placed.Height; dy++)
            {
                GridCell cell = GetCell(placed.Origin + new Vector2Int(dx, dy));
                cell.isOccupied = false;
                cell.placedObject = null;
            }
        }
    }
}