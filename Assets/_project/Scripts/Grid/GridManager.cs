using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Size")]
    [SerializeField] private int _gridWidth = 9;

    [Header("Floor Layout (각 영역 세로 크기)")]
    [Tooltip("1층 영역 세로 (홀 + 주방). 기본 12")]
    [SerializeField] private int floor1Height = 12;
    [Tooltip("1층/2층 사이 빈 영역 — 크게 잡으면 카메라 fit해도 2층 절대 안 보임 (권장 ≥ 20)")]
    [SerializeField] private int gapHeight = 30;
    [Tooltip("2층 영역 세로 (홀 + 화장실). 기본 12")]
    [SerializeField] private int floor2Height = 12;
    [Tooltip("1층 주방 / 2층 화장실 세로 (공통). 기본 4")]
    [SerializeField] private int kitchenAndToiletRows = 4;
    [Tooltip("1층 홀 중 주방 바로 아래 N줄을 카운터 구역으로. 기본 4 (y4~7)")]
    [SerializeField] private int counterRows = 4;

    [Header("Floor Tilemap")]
    [SerializeField] private Tilemap floorTilemap;

    public int GridWidth => _gridWidth;
    public int GridHeight => floor1Height + gapHeight + floor2Height;
    public int Floor2YStart => floor1Height + gapHeight;
    public int KitchenBoundaryY => floor1Height - kitchenAndToiletRows; // 1층 주방 시작 y (보통 8)
    public int Floor1Height => floor1Height;                            // 1층 주방 상단(배타) y
    public int ActiveCellCount
    {
        get
        {
            int count = 0;
            int h = GridHeight;
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < h; y++)
                    if (_cells[x, y].isActive) count++;
            return count;
        }
    }

    private GridCell[,] _cells;

    private HashSet<Vector2Int> _reservedCells = new HashSet<Vector2Int>
    {
        new Vector2Int(0, 1), // 손님/라이더 출입문 (왼쪽 사이드워크 방향) — 가구 배치 금지, 통과 가능
        // 픽업대 셀은 시작 배치 좌표에 맞춰 추가 (예: new Vector2Int(1, 8))
    };

    [Header("직원 통근용 주방 문 (벽으로 안 막는 1칸)")]
    [SerializeField] private Vector2Int kitchenDoorCell = new Vector2Int(1, 8);
    public Vector2Int KitchenDoorCell => kitchenDoorCell;

    [Header("진입 금지 셀 (길찾기 X, 가구 설치 O — 화장실 칸막이 등)")]
    [SerializeField] private List<Vector2Int> blockedCells;

    [Header("배치 금지 셀 (길찾기 O, 가구 설치 X — 입구·픽업대 옆 통로 등)")]
    [Tooltip("지나다닐 순 있지만 가구는 못 놓는 셀. 인스펙터에서 좌표 직접 추가.")]
    [SerializeField] private List<Vector2Int> reservedCells;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CreateGrid();
        HideInactiveFloorTiles();
        // 카메라 정렬은 CameraController가 Start에서 수행
    }

    // 호환성 유지용 — 실제 동작은 CameraController가 담당.
    public void CenterCameraOnActiveGrid()
    {
        CameraController.Instance?.Refresh();
    }

    // 특정 floor의 활성 셀 world bbox. 활성 셀 없으면 null.
    public Bounds? GetActiveBoundsForFloor(FloorIndex floor)
    {
        if (!TryGetActiveCellBboxForFloor(floor, out int minX, out int minY, out int maxX, out int maxY))
            return null;
        return new Bounds(
            new Vector3((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f, 0f),
            new Vector3(maxX - minX + 1, maxY - minY + 1, 0f));
    }

    public bool IsCellOnFloor(int y, FloorIndex floor)
    {
        int f2start = Floor2YStart;
        if (f2start < 0) return floor == FloorIndex.Floor1; // 2층 미정의: 전부 1층
        if (floor == FloorIndex.Floor1) return y < f2start;
        return y >= f2start;
    }

    public FloorIndex GetFloorAt(Vector3 worldPos)
    {
        Vector2Int cell = WorldToCell(worldPos);
        return IsCellOnFloor(cell.y, FloorIndex.Floor2) ? FloorIndex.Floor2 : FloorIndex.Floor1;
    }

    private void CreateGrid()
    {
        int gridH = GridHeight;
        int f2start = Floor2YStart;
        _cells = new GridCell[_gridWidth, gridH];
        int kitchen1YStart = floor1Height - kitchenAndToiletRows;

        bool hasFloor2 = f2start >= 0 && f2start < gridH;
        int floor2H = hasFloor2 ? gridH - f2start : 0;
        int toilet2YStart = hasFloor2 ? f2start + Mathf.Max(0, floor2H - kitchenAndToiletRows) : -1;

        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < gridH; y++)
        {
            bool isReserved = _reservedCells.Contains(new Vector2Int(x, y));
            CellZone zone;
            bool isActive;

            if (y < floor1Height)
            {
                // Floor 1: 시작부터 전체 활성
                isActive = true;
                // 위에서부터 주방(y8~11) → 카운터(y4~7) → 홀(y0~3)
                if (y >= kitchen1YStart)                       zone = CellZone.Kitchen;
                else if (y >= kitchen1YStart - counterRows)    zone = CellZone.Counter;
                else                                           zone = CellZone.Hall;
            }
            else if (hasFloor2 && y >= f2start)
            {
                // Floor 2: 셀은 비활성으로 시작 (2단/3단 확장 시 활성화)
                isActive = false;
                zone = (y >= toilet2YStart) ? CellZone.Floor2_Toilet : CellZone.Floor2_Hall;
            }
            else
            {
                // Gap: 영구 비활성
                isActive = false;
                zone = CellZone.Hall;
            }

            _cells[x, y] = new GridCell(x, y, isActive, isReserved, zone);
        }

        if (blockedCells != null)
            foreach (var pos in blockedCells)
                SetWall(pos, true);

        if (reservedCells != null)
            foreach (var pos in reservedCells)
                SetReserved(pos, true);   // 통과 O, 배치 X
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

    /// <summary>가구의 PlacementZone → 실제 셀 CellZone 매핑 (두 enum의 번호가 달라 명시 변환 필요).</summary>
    public static CellZone ToCellZone(PlacementZone z) => z switch
    {
        PlacementZone.kitchen   => CellZone.Kitchen,
        PlacementZone.Hall      => CellZone.Hall,
        PlacementZone.RiderRoom => CellZone.RiderRoom,
        PlacementZone.Toilet    => CellZone.Floor2_Toilet,
        PlacementZone.Counter   => CellZone.Counter,
        _ => CellZone.Hall
    };

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < GridHeight;
    }

    // 길찾기용 walkability — 역할별 zone 필터 + 가구 회피 + 벽
    public bool IsCellWalkable(Vector2Int pos, PathRole role)
    {
        var c = GetCell(pos);
        if (c == null) return false;
        if (!c.isActive) return false;
        if (c.isWall) return false;     // 벽은 누구도 못 지남 (화장실 칸막이 등 blockedCells)
        // 가구는 못 지남. 단 reserved 셀(문·픽업대 등 통과 의도) / passThrough 가구는 예외.
        bool walkableFurniture = c.placedObject != null && c.placedObject.Data != null && c.placedObject.Data.passThrough;
        if (c.isOccupied && !c.isReserved && !walkableFurniture) return false;

        // 역할별 zone 제한
        switch (role)
        {
            case PathRole.Customer:
                return c.zone == CellZone.Hall
                    || c.zone == CellZone.Counter
                    || c.zone == CellZone.Floor2_Hall
                    || c.zone == CellZone.Floor2_Toilet;
            case PathRole.Cook:     return c.zone == CellZone.Kitchen;
            case PathRole.Server:   return true; // 모든 zone 통과
            case PathRole.Rider:    return c.zone == CellZone.Hall || c.zone == CellZone.Counter;
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

    // PassWindow 영역 세팅: 픽업대 셀 + 주방문 reserved 처리 (통과 O, 가구 배치 X)
    public void SetupPassWindow(IEnumerable<Vector2Int> passWindowCells)
    {
        foreach (var pos in passWindowCells)
            SetReserved(pos, true);

        SetReserved(kitchenDoorCell, true);
        // 주방 경계벽(y=8 가로벽) 미생성 — 직원 통근/길찾기 방해 제거.
        // (손님=Hall 전용, 요리사=Kitchen 전용이라 벽 없이도 영역 유지됨)
    }

    // 특정 floor의 활성 셀 cell-coord bbox. 활성 셀 없으면 false.
    private bool TryGetActiveCellBboxForFloor(FloorIndex floor,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = int.MaxValue; minY = int.MaxValue;
        maxX = int.MinValue; maxY = int.MinValue;
        if (_cells == null) return false;
        int gridH = GridHeight;
        bool any = false;
        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < gridH; y++)
        {
            if (_cells[x, y] == null || !_cells[x, y].isActive) continue;
            if (!IsCellOnFloor(y, floor)) continue;
            any = true;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        return any;
    }

    // 현재 카메라 floor 기준으로 가구 footprint를 활성 영역 안에 들어오도록 클램프
    public Vector2Int ClampToActiveArea(Vector2Int origin, int width, int height)
    {
        FloorIndex floor = CameraController.Instance != null
            ? CameraController.Instance.CurrentFloor
            : FloorIndex.Floor1;

        if (!TryGetActiveCellBboxForFloor(floor, out int minX, out int minY, out int maxX, out int maxY))
            return origin;

        int clampMaxX = Mathf.Max(minX, maxX - width + 1);
        int clampMaxY = Mathf.Max(minY, maxY - height + 1);
        return new Vector2Int(
            Mathf.Clamp(origin.x, minX, clampMaxX),
            Mathf.Clamp(origin.y, minY, clampMaxY)
        );
    }

    // PlacementSystem 스폰용: 현재 floor 활성 영역 중앙
    public Vector2Int GetActiveAreaCenter(int width, int height)
    {
        FloorIndex floor = CameraController.Instance != null
            ? CameraController.Instance.CurrentFloor
            : FloorIndex.Floor1;
        if (!TryGetActiveCellBboxForFloor(floor, out int minX, out int minY, out int maxX, out int maxY))
            return Vector2Int.zero;
        int cx = (minX + maxX) / 2 - width / 2;
        int cy = (minY + maxY) / 2 - height / 2;
        return ClampToActiveArea(new Vector2Int(cx, cy), width, height);
    }
    // 시작 시 호출: 비활성 셀의 Tilemap 타일을 캐싱하고 화면에서 제거
    private void HideInactiveFloorTiles()
    {
        if (floorTilemap == null)
        {
            Debug.LogWarning("[GridManager] floorTilemap 미할당 — 인스펙터에서 Floor Tilemap 슬롯에 Tilemap을 드래그하세요");
            return;
        }
        int gridH = GridHeight;
        for (int x = 0; x < _gridWidth; x++)
        for (int y = 0; y < gridH; y++)
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
        int gridH = GridHeight;
        for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < gridH; y++)
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

    /// <summary>가구가 요구하는 존이 해당 셀 존에 놓일 수 있는지. 홀 가구는 1·2층 홀 모두 허용.</summary>
    public static bool ZoneAccepts(CellZone furnitureZone, CellZone cellZone)
    {
        if (furnitureZone == cellZone) return true;
        if (furnitureZone == CellZone.Hall && cellZone == CellZone.Floor2_Hall) return true;
        return false;
    }

    public bool CanPlace(Vector2Int origin, int width, int height, CellZone zone, bool wallMount = false)
    {
        for(int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                GridCell cell = GetCell(origin + new Vector2Int(dx, dy));
                if (cell == null || cell.isOccupied || !cell.isActive) return false;
                if (!wallMount && (cell.isReserved || !ZoneAccepts(zone, cell.zone))) return false;
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
        // 카메라 재정렬 없음 — 확장 자체로 카메라 변경 X. 토글/DT 해금 시에만 재계산.
    }
}
