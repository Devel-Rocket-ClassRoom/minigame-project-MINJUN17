using UnityEngine;

public class PlacedObject
{
    public FurnitureData Data;
    public GameObject Instance;
    public Vector2Int Origin;

    public PlacedObject(FurnitureData data, GameObject instance, Vector2Int origin)
    {
        Data = data;
        Instance = instance;
        Origin = origin;
    }
}
