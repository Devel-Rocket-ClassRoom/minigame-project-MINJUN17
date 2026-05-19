using UnityEngine;

public class PlacedObjectInit : MonoBehaviour
{
    // 처음부터 설치될 오브젝트를 위한 클래스
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private FurnitureData counterData;
    [SerializeField] private FurnitureData chairData;
    [SerializeField] private FurnitureData grillData;
    [SerializeField] private FurnitureData passWindowData;

    private void Start()
    {
        placementSystem.PlaceInitial(counterData, new Vector2Int(1, 3));
        placementSystem.PlaceInitial(counterData, new Vector2Int(2, 3));
        placementSystem.PlaceInitial(passWindowData, new Vector2Int(1, 5));
        placementSystem.PlaceInitial(grillData, new Vector2Int(1, 7));
        placementSystem.PlaceInitial(chairData, new Vector2Int(1, 0));
        placementSystem.PlaceInitial(chairData, new Vector2Int(3, 0));
        placementSystem.PlaceInitial(chairData, new Vector2Int(1, 1));
        placementSystem.PlaceInitial(chairData, new Vector2Int(3, 1));
    }
}
