using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField] private int _gridWidth = 12;
    [SerializeField] private int _gridHeight = 12;
    [SerializeField] private int _startGridWidth = 6;
    [SerializeField] private int _startGridHeight = 6;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;

    private GridCell[,] _cells;

    private void Awake()
    {
        CreateGrid();
    }
    private void CreateGrid()
    {
        _cells = new GridCell[_gridWidth, _gridHeight];
        for(int x = 0; x < _gridWidth; x++)
        {
            for(int y = 0; y < _gridHeight; y++)
            {
                bool isActive = x < _startGridWidth && y < _startGridHeight;
                _cells[x, y] =  new GridCell(x, y, isActive);
            }
        }
    }
    public GridCell GetCell(int x, int y)
    {
        if(!IsInBounds(x, y)) 
        {
            return null;
        }
        return _cells[x, y];
    }
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;
    }
    public Vector3 CellToWorld(int x, int y) // 가구를 셀에 놓을때 활용
    {
        return new Vector3(x + 0.5f, y + 0.5f, 0);
    }
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        return new Vector2Int(x, y);
    }
}
