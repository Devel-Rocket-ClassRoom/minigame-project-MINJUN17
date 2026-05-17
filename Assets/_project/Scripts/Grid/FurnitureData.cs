using UnityEngine;

[CreateAssetMenu(fileName = "FurnitureData", menuName = "Grid/Furniture Data")]
public class FurnitureData : ScriptableObject
{
    public GameObject prefab;
    public int width = 1;
    public int height = 1;
    public int anchorX;
    public int anchorY;
}
