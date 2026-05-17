using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    public GridManager gridManager;
    public Color activeColor = new Color(0f, 1f, 0f, 0.2f);
    public Color occupiedColor= new Color(0f, 0f, 1f, 0.3f);
    public Color inActiveColor= new Color(1f, 0f, 0f, 0.1f);

    private void OnDrawGizmos()
    {
        if(gridManager == null) return;
        if(!Application.isPlaying) return;

        for(int x = 0; x < gridManager.GridWidth; x++)
        {
            for(int y = 0; y < gridManager.GridHeight; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                GridCell cell = gridManager.GetCell(pos);
                if(cell == null) continue;
                if (cell.isOccupied)
                {
                    Gizmos.color = occupiedColor;
                }
                else if (cell.isActive)
                {
                    Gizmos.color = activeColor;
                }
                else
                {
                    Gizmos.color = inActiveColor;
                }
                Vector3 center = gridManager.CellToWorld(pos);
                Gizmos.DrawCube(center, new Vector3(0.95f, 0.95f, 0.01f));
            }
        }
    }
}
