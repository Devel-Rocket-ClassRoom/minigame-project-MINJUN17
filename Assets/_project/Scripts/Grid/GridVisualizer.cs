using UnityEngine;

[ExecuteAlways]
public class GridVisualizer : MonoBehaviour
{
    public GridManager gridManager;
    public Color hallColor = new Color(0f, 1f, 0f, 0.2f);
    public Color kitchenColor = new Color(1f, 0.92f, 0f, 0.2f);
    public Color counterColor = new Color(1f, 0.5f, 0f, 0.2f);
    public Color floor2HallColor = new Color(0f, 0.7f, 1f, 0.2f);
    public Color floor2ToiletColor = new Color(1f, 0.5f, 0.8f, 0.2f);
    public Color occupiedColor = new Color(0f, 0f, 1f, 0.3f);
    public Color reservedColor = new Color(0f, 0f, 1f, 0.3f);
    public Color inActiveColor = new Color(1f, 0f, 0f, 0.1f);
    public Color lineColor = new Color(1f, 1f, 1f, 0.4f);

    private void OnDrawGizmos()
    {
        if (gridManager == null) return;

        int w = gridManager.GridWidth;
        int h = gridManager.GridHeight;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            Vector2Int pos = new Vector2Int(x, y);
            Vector3 center = gridManager.CellToWorld(pos);
            Gizmos.color = GetCellColor(pos);
            Gizmos.DrawCube(center, new Vector3(0.95f, 0.95f, 0.01f));
        }

        Gizmos.color = lineColor;
        for (int x = 0; x <= w; x++)
            Gizmos.DrawLine(new Vector3(x, 0, 0), new Vector3(x, h, 0));
        for (int y = 0; y <= h; y++)
            Gizmos.DrawLine(new Vector3(0, y, 0), new Vector3(w, y, 0));
    }

    private Color GetCellColor(Vector2Int pos)
    {
        if (Application.isPlaying)
        {
            GridCell cell = gridManager.GetCell(pos);
            if (cell != null)
            {
                if (cell.isReserved) return reservedColor;
                if (cell.isOccupied) return occupiedColor;
                if (!cell.isActive) return inActiveColor;
                return ZoneColor(cell.zone);
            }
            return inActiveColor;
        }
        // 에디터 모드: 활성/비활성 정보를 모르므로 비활성 색만 표시
        return inActiveColor;
    }

    private Color ZoneColor(CellZone zone)
    {
        switch (zone)
        {
            case CellZone.Kitchen: return kitchenColor;
            case CellZone.Hall: return hallColor;
            case CellZone.Counter: return counterColor;
            case CellZone.Floor2_Hall: return floor2HallColor;
            case CellZone.Floor2_Toilet: return floor2ToiletColor;
            default: return hallColor;
        }
    }
}
