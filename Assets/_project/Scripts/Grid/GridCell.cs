using UnityEngine;

public class GridCell
{
    public int x;
    public int y;
    public bool isActive; // 해당 타일이 활성화 상태인지 비활성화시 접근 x
    public bool isOccupied; // 해당타일에 물체가 있는지 활성화는 되었지만 물건을 놓을수 있는지 없는지 판단  
    public GameObject placedObject; // 타일 위에 올라와 있는 오브젝트(삭제 할 때 사용)

    public GridCell(int x, int y, bool isActive)
    {
        this.x = x;
        this.y = y;
        this.isActive = isActive;
        this.isOccupied = false;
        this.placedObject = null;
    }
}
