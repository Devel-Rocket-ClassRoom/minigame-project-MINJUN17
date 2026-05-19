using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField] private int _gridWidth = 9;
    [SerializeField] private int _gridHeight = 12;
    [SerializeField] private int _startGridWidth = 4;
    [SerializeField] private int _startGridHeight = 9;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float cameraPadding = 1f;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int StartGridWidth => _startGridWidth;
    public int StartGridHeight => _startGridHeight;
    public int ActiveCellCount
    {
        get
        {
            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    if (_cells[x, y].isActive) count++;
            return count;
        }
    }

    private GridCell[,] _cells;

    private HashSet<Vector2Int> _reservedCells = new HashSet<Vector2Int>
    {
        new Vector2Int(0, 3), // 출입구 (홀 좌하단)
        // 픽업대 셀은 시작 배치 좌표에 맞춰 추가 (예: new Vector2Int(1, 8))
    };

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        CreateGrid();
        CenterCameraOnActiveGrid();

        // ※ 좌표계 변경(좌상단 시작) 후 통로/예약 셀 정책 미정.
        //    필요해지면 새 좌표 기준으로 다시 작성.
        //
        // for (int x = 0; x < _gridWidth; x++)
        // {
        //     for (int y = 0; y < _gridHeight; y++)
        //     {
        //         if (/* 새 좌표 기준 조건 */)
        //             _reservedCells.Add(new Vector2Int(x, y));
        //     }
        // }
    }

    // 활성 영역(_startGridWidth × _startGridHeight) 중앙으로 카메라 정렬
    // 활성 영역이 확장될 때마다 다시 호출하면 됨
    public void CenterCameraOnActiveGrid()
    {
        if (mainCamera == null) return;

        float centerX = _startGridWidth / 2f;
        float centerY = _gridHeight - _startGridHeight / 2f;   // 좌상단 기준

        Vector3 pos = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(centerX, centerY, pos.z);

        if (mainCamera.orthographic)
            mainCamera.orthographicSize = 6f;
    }

    private void CreateGrid()
    {
        _cells = new GridCell[_gridWidth, _gridHeight];
        int activeYStart = _gridHeight - _startGridHeight;   // 12 - 9 = 3
        int kitchenYStart = _gridHeight - 4;                 // 12 - 4 = 8 (위 4행이 주방)

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                bool isActive = x < _startGridWidth && y >= activeYStart;
                bool isReserved = _reservedCells.Contains(new Vector2Int(x, y));
                CellZone zone = (x < 4 && y >= kitchenYStart) ? CellZone.Kitchen : CellZone.Hall;
                _cells[x, y] = new GridCell(x, y, isActive, isReserved, zone);
            }
        }
    }

    public GridCell GetCell(Vector2Int pos)
    {
        if (!IsInBounds(pos)) return null;
        return _cells[pos.x, pos.y];
    }
    public CellZone GetZone(Vector2Int pos)
    {
        var cell = GetCell(pos);
        return cell.zone;
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
    }

    // 가구 footprint(width × height)가 활성 영역 안에 들어오도록 origin을 클램프
    public Vector2Int ClampToActiveArea(Vector2Int origin, int width, int height)
    {
        int maxX = Mathf.Max(0, _startGridWidth - width);
        int minY = _gridHeight - _startGridHeight;                  // 3
        int maxY = Mathf.Max(minY, _gridHeight - height);           // 12 - h
        return new Vector2Int(
            Mathf.Clamp(origin.x, 0, maxX),
            Mathf.Clamp(origin.y, minY, maxY)
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

    public bool CanPlace(Vector2Int origin, int width, int height, CellZone zone)
    {
        for(int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                GridCell cell = GetCell(origin + new Vector2Int(dx, dy));
                if (cell == null || cell.isOccupied || !cell.isActive || cell.isReserved || cell.zone != zone) return false;
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

    public void ActivateCells(ExpansionStageData stage)
    {
        for (int dx = 0; dx < stage.width; dx++)
        {
            for (int dy = 0; dy < stage.height; dy++)
            {
                var pos = stage.origin + new Vector2Int(dx, dy);
                if (!IsInBounds(pos)) continue;
                var cell = _cells[pos.x, pos.y];
                cell.isActive = true;
                cell.zone = stage.newZone;
            }
        }
    }
}
