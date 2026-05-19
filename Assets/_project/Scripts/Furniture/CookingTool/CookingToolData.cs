using UnityEngine;

[CreateAssetMenu(fileName = "CookingToolData", menuName = "Furniture/CookingToolData")]
public class CookingToolData : FurnitureData
{
    public ToolType toolType;
    public float usingDuration;
}