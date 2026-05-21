using UnityEngine;
public enum CellZone { Kitchen, Hall, RiderRoom }
public class GridCell
{
    public CellZone zone;
    public int x;
    public int y;
    public bool isActive; // 해당 타일이 활성화 상태인지 비활성화시 접근 x
    public bool isOccupied; // 해당타일에 물체가 있는지 활성화는 되었지만 물건을 놓을수 있는지 없는지 판단
    public bool isReserved; // 활성화 되면 지나다닐수는 있지만 물건 배치는 못하는 타일 (ex - 입구, 음식 픽업 장소)
    public bool isWall;     // 벽, 누구도 못 지나감 (주방-홀 경계 등)
    public PlacedObject placedObject; // 타일 위에 올라와 있는 배치 인스턴스

    public GridCell(int x, int y, bool isActive, bool isReserved, CellZone zone)
    {
        this.x = x;
        this.y = y;
        this.isActive = isActive;
        this.isReserved = isReserved;
        this.isOccupied = false;
        this.isWall = false;
        this.placedObject = null;
        this.zone = zone;
    }
}
