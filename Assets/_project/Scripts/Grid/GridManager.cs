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
        return _cells[x, y];
    }
}
