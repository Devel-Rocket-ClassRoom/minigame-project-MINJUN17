using UnityEngine;

[CreateAssetMenu(fileName = "FurnitureData", menuName = "Furniture/Furniture Data")]
public class FurnitureData : ScriptableObject
{
    public GameObject prefab;
    public PlacementZone zone;
    public int width = 1;
    public int height = 1;
    public int anchorX;
    public int anchorY;
}
