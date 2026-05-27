using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Size")]
    [SerializeField] private int _gridWidth = 9;
    [SerializeField] private int _gridHeight = 12;
    [SerializeField] private int _startGridWidth = 4;
    [SerializeField] private int _startGridHeight = 9;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float cameraPadding = 1f;

    [Header("Floor Tilemap")]
    [SerializeField] private Tilemap floorTilemap;

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

    [Header("직원 통근용 주방 문 (벽으로 안 막는 1칸)")]
    [SerializeField] private Vector2Int kitchenDoorCell = new Vector2Int(1, 8);
    public Vector2Int KitchenDoorCell => kitchenDoorCell;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;
        CreateGrid();
        HideInactiveFloorTiles();
        CenterCameraOnActiveGrid();

    }

    // 실제 활성화된 셀들의 bounding box 중앙으로 카메라 정렬 + ortho size 자동 조정
    // 확장될 때마다 자동 호출됨 (ActivateCells 내부에서)
    // DTLane이 존재하면 그 bbox도 합집합으로 포함 (DT 차로가 그리드 바깥에 있어도 화면에 들어옴)
    public void CenterCameraOnActiveGrid()
    {
        if (mainCamera == null) return;
        if (_cells == null) return;

        // 활성 셀 bounds 계산
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool anyActive = false;
        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < _gridHeight; y++)
        {
            if (!_cells[x, y].isActive) continue;
            anyActive = true;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        if (!anyActive) return;

        // 셀 bbox → world bounds 변환 (셀 좌표 corner-origin, 1셀=1유닛)
        Bounds combined = new Bounds(
            new Vector3((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f, 0f),
            new Vector3(maxX - minX + 1, maxY - minY + 1, 0f));

        // DT 해금 후에만 차로 영역을 카메라에 포함 (전엔 그리드 기준만)
        if (DTLane.Instance != null && DTLane.Instance.WaypointCount > 0
            && DTSystem.Instance != null && DTSystem.Instance.IsUnlocked)
            combined.Encapsulate(DTLane.Instance.GetVisibleBounds());

        Vector3 pos = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(combined.center.x, combined.center.y, pos.z);

        if (mainCamera.orthographic)
        {
            float aspect = mainCamera.aspect > 0.01f ? mainCamera.aspect : 1f;
            float halfH = combined.extents.y;
            float halfW = combined.extents.x / aspect;
            mainCamera.orthographicSize = Mathf.Max(halfH, halfW) + cameraPadding;
        }
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

    // 길찾기용 walkability — 역할별 zone 필터 + 가구 회피 + 벽
    public bool IsCellWalkable(Vector2Int pos, PathRole role)
    {
        var c = GetCell(pos);
        if (c == null) return false;
        if (!c.isActive) return false;
        if (c.isWall) return false;     // 벽은 누구도 못 지남
        if (c.isOccupied) return false; // 가구는 못 지남 (isReserved는 OK)

        // 역할별 zone 제한
        switch (role)
        {
            case PathRole.Customer: return c.zone == CellZone.Hall;
            case PathRole.Cook:     return c.zone == CellZone.Kitchen;
            case PathRole.Server:   return true; // 주방/홀 둘 다 OK
            case PathRole.Rider:    return c.zone == CellZone.Hall || c.zone == CellZone.RiderRoom;
            case PathRole.Commute:  return true; // 통근: 존 무시 (단 벽/가구/비활성은 위에서 이미 차단)
            default: return true;
        }
    }

    // 셀을 벽으로 마킹 (주방-홀 경계 등)
    public void SetWall(Vector2Int pos, bool value = true)
    {
        var c = GetCell(pos);
        if (c != null) c.isWall = value;
    }

    // 셀을 예약으로 마킹 (배치 불가, 통과 가능)
    public void SetReserved(Vector2Int pos, bool value = true)
    {
        var c = GetCell(pos);
        if (c != null) c.isReserved = value;
    }

    // PassWindow 영역 세팅: 4셀 reserved 처리 + 주방 측 셀만 벽 opening으로
    public void SetupPassWindow(IEnumerable<Vector2Int> passWindowCells)
    {
        var openings = new List<Vector2Int>();
        foreach (var pos in passWindowCells)
        {
            SetReserved(pos, true);
            var c = GetCell(pos);
            if (c != null && c.zone == CellZone.Kitchen)
                openings.Add(pos);
        }

        // 직원 통근용 주방 전용 문: 벽 제외 + 가구 배치 금지(reserved)
        openings.Add(kitchenDoorCell);
        SetReserved(kitchenDoorCell, true);

        BuildKitchenLWalls(openings);
    }

    // 주방을 L자로 둘러싸는 벽 (바닥 가로 + 우측 세로)
    // openings에 포함된 셀은 벽 X (PassWindow 위치)
    // 호출 예: BuildKitchenLWalls(new[] { new Vector2Int(1, 8) });
    public void BuildKitchenLWalls(IEnumerable<Vector2Int> openings)
    {
        var openSet = new HashSet<Vector2Int>(openings);
        int kitchenYStart = _gridHeight - 4; // 주방 바닥 행 (y=8)
        int kitchenXEnd = 3;                 // 주방 우측 열 (x=3)

        // 가로벽: 주방 바닥 (y=8), x=0..3
        for (int x = 0; x <= kitchenXEnd; x++)
        {
            var pos = new Vector2Int(x, kitchenYStart);
            if (!openSet.Contains(pos)) SetWall(pos);
        }

        // 세로벽: 주방 우측 (x=3), y=9..11 (y=8 코너는 위 루프에서 이미 처리)
        for (int y = kitchenYStart + 1; y < _gridHeight; y++)
        {
            var pos = new Vector2Int(kitchenXEnd, y);
            if (!openSet.Contains(pos)) SetWall(pos);
        }
    }

    // 가구 footprint(width × height)가 활성 영역(현재 활성 셀들의 bounding box) 안에 들어오도록 origin을 클램프
    // 확장될 때마다 활성 셀이 늘어나면 클램프 범위도 자동으로 늘어남
    public Vector2Int ClampToActiveArea(Vector2Int origin, int width, int height)
    {
        // 현재 활성 셀들의 bounding box 계산
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        bool any = false;
        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < _gridHeight; y++)
        {
            if (_cells[x, y] == null || !_cells[x, y].isActive) continue;
            any = true;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        if (!any) return origin;

        // footprint가 bbox 안에 들어오도록 origin 상한 조정
        int clampMaxX = Mathf.Max(minX, maxX - width + 1);
        int clampMaxY = Mathf.Max(minY, maxY - height + 1);
        return new Vector2Int(
            Mathf.Clamp(origin.x, minX, clampMaxX),
            Mathf.Clamp(origin.y, minY, clampMaxY)
        );
    }
    // 시작 시 호출: 비활성 셀의 Tilemap 타일을 캐싱하고 화면에서 제거
    private void HideInactiveFloorTiles()
    {
        if (floorTilemap == null)
        {
            Debug.LogWarning("[GridManager] floorTilemap 미할당 — 인스펙터에서 Floor Tilemap 슬롯에 Tilemap을 드래그하세요");
            return;
        }
        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < _gridHeight; y++)
        {
            var cell = _cells[x, y];
            var pos = new Vector3Int(x, y, 0);
            cell.cachedTile = floorTilemap.GetTile(pos);
            if (!cell.isActive)
                floorTilemap.SetTile(pos, null);
        }
    }
    public Vector3 CellToWorld(Vector2Int pos, int width = 1, int height = 1)
    {
        return new Vector3(pos.x + width * 0.5f, pos.y + height * 0.5f, 0);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
    }

    // 가구 풋프린트 전체 테두리를 기준으로 인접 walkable 셀 위치 반환
    // 역할별 선호 zone 우선 (Cook→Kitchen, Server/Customer→Hall)
    // from: 요청자 위치. 선호 zone 내에서 from과 가장 가까운 후보를 반환 (방향 우선순위 X)
    public Vector3 GetFurnitureApproachPosition(Vector3 worldPos, PathRole role, Vector3 from)
    {
        Vector2Int anyCell = WorldToCell(worldPos);
        var c = GetCell(anyCell);

        // PlacedObject 풋프린트 파악
        int ox, oy, w, h;
        if (c?.placedObject != null)
        {
            ox = c.placedObject.Origin.x;
            oy = c.placedObject.Origin.y;
            w  = c.placedObject.Width;
            h  = c.placedObject.Height;
        }
        else
        {
            ox = anyCell.x; oy = anyCell.y; w = 1; h = 1;
        }

        Vector2Int[] offsets = { new(0,1), new(0,-1), new(1,0), new(-1,0) };

        // 후보 셀 수집
        var candidates = new List<Vector2Int>();
        for (int x = ox; x < ox + w; x++)
        for (int y = oy; y < oy + h; y++)
        {
            foreach (var off in offsets)
            {
                Vector2Int adj = new Vector2Int(x, y) + off;
                if (adj.x >= ox && adj.x < ox + w && adj.y >= oy && adj.y < oy + h) continue;
                if (IsCellWalkable(adj, role))
                    candidates.Add(adj);
            }
        }

        if (candidates.Count == 0) return worldPos;

        // 역할별 선호 zone: Cook은 Kitchen, 그 외는 Hall
        CellZone preferred = role == PathRole.Cook ? CellZone.Kitchen : CellZone.Hall;

        // 선호 zone 우선, 그 안에서 from과의 거리로 정렬
        Vector2Int best = candidates[0];
        float bestDist = float.MaxValue;
        bool foundPreferred = false;

        foreach (var cand in candidates)
        {
            var cc = GetCell(cand);
            bool inPreferred = cc != null && cc.zone == preferred;
            if (foundPreferred && !inPreferred) continue;          // 이미 선호 후보 있으면 비선호 무시
            if (!foundPreferred && inPreferred)                    // 처음 선호 후보 발견
            {
                best = cand;
                bestDist = Vector3.Distance(CellToWorld(cand), from);
                foundPreferred = true;
                continue;
            }
            float d = Vector3.Distance(CellToWorld(cand), from);
            if (d < bestDist) { best = cand; bestDist = d; }
        }
        return CellToWorld(best);
    }

    public List<Vector3> GetWalkableCellsInZone(CellZone zone)
    {
        var list = new List<Vector3>();
        for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
            {
                var pos = new Vector2Int(x, y);
                var cell = GetCell(pos);
                if (cell == null) continue;
                if (!cell.isActive) continue;
                if (cell.zone != zone) continue;
                if (cell.isWall) continue;
                if (cell.isOccupied) continue;
                list.Add(CellToWorld(pos));
            }
        return list;
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
                if (floorTilemap != null && cell.cachedTile != null)
                    floorTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), cell.cachedTile);
            }
        }
        // 확장 후 카메라 자동 재정렬
        CenterCameraOnActiveGrid();
    }
}
