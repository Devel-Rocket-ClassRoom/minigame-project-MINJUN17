using UnityEngine;

[ExecuteAlways]
public class GridVisualizer : MonoBehaviour
{
    public GridManager gridManager;
    public Color activeColor = new Color(0f, 1f, 0f, 0.2f);
    public Color occupiedColor = new Color(0f, 0f, 1f, 0.3f);
    public Color inActiveColor = new Color(1f, 0f, 0f, 0.1f);
    public Color lineColor = new Color(1f, 1f, 1f, 0.4f);

    private void OnDrawGizmos()
    {
        if (gridManager == null) return;

        int w = gridManager.GridWidth;
        int h = gridManager.GridHeight;
        int sw = gridManager.StartGridWidth;
        int sh = gridManager.StartGridHeight;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Vector3 center = gridManager.CellToWorld(pos);
                Gizmos.color = GetCellColor(pos, x < sw && y < sh);
                Gizmos.DrawCube(center, new Vector3(0.95f, 0.95f, 0.01f));
            }
        }

        // 격자 라인
        Gizmos.color = lineColor;
        for (int x = 0; x <= w; x++)
            Gizmos.DrawLine(new Vector3(x, 0, 0), new Vector3(x, h, 0));
        for (int y = 0; y <= h; y++)
            Gizmos.DrawLine(new Vector3(0, y, 0), new Vector3(w, y, 0));
    }

    private Color GetCellColor(Vector2Int pos, bool isInStartArea)
    {
        // 플레이 중에는 실제 셀 상태 반영
        if (Application.isPlaying)
        {
            GridCell cell = gridManager.GetCell(pos);
            if (cell != null)
            {
                if (cell.isOccupied) return occupiedColor;
                if (cell.isActive) return activeColor;
                return inActiveColor;
            }
        }
        // 에디터에서는 시작 활성 영역만 활성색으로
        return isInStartArea ? activeColor : inActiveColor;
    }
}
