using System.Collections.Generic;
using UnityEngine;

public class PlacedObjectInit : MonoBehaviour
{
    // 처음부터 설치될 오브젝트를 위한 클래스
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private InitialPlacementData layoutData;
    //[SerializeField] private FurnitureData counterData;
    //[SerializeField] private FurnitureData chairData;
    //[SerializeField] private FurnitureData grillData;
    //[SerializeField] private FurnitureData passWindowData;

    private void Start()
    {
        foreach (var e in layoutData.entries)
            placementSystem.PlaceInitial(e.furniture, e.position, e.rotationStep);

        // PassWindow 위치 자동 감지 → 풋프린트 셀들 reserved + 주방측은 벽 opening 처리
        var passWindowCells = new List<Vector2Int>();
        foreach (var pw in PassWindowManager.Instance.PassWindows)
        {
            Vector2Int anyCell = GridManager.Instance.WorldToCell(pw.transform.position);
            var c = GridManager.Instance.GetCell(anyCell);
            if (c?.placedObject == null) continue;
            var po = c.placedObject;
            for (int x = po.Origin.x; x < po.Origin.x + po.Width; x++)
                for (int y = po.Origin.y; y < po.Origin.y + po.Height; y++)
                    passWindowCells.Add(new Vector2Int(x, y));
        }
        GridManager.Instance.SetupPassWindow(passWindowCells);

        StaffManager.Instance.Init();
        //placementSystem.PlaceInitial(counterData, new Vector2Int(1, 3));
        //placementSystem.PlaceInitial(counterData, new Vector2Int(2, 3));
        //placementSystem.PlaceInitial(passWindowData, new Vector2Int(1, 5));
        //placementSystem.PlaceInitial(grillData, new Vector2Int(1, 7));
        //placementSystem.PlaceInitial(chairData, new Vector2Int(1, 0));
        //placementSystem.PlaceInitial(chairData, new Vector2Int(3, 0));
        //placementSystem.PlaceInitial(chairData, new Vector2Int(1, 1));
        //placementSystem.PlaceInitial(chairData, new Vector2Int(3, 1));
        //StaffManager.Instance.Init();
    }
}
