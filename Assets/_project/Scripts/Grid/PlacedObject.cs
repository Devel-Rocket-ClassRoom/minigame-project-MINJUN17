using UnityEngine;

public class PlacedObject
{
    public FurnitureData Data;
    public GameObject Instance;
    public Vector2Int Origin;

    public int RotationStep; // 0 = 0도, 1 = 90도, 2 = 180도, 3 = 270도
    public int Width => RotationStep % 2 == 0 ? Data.width : Data.height;
    public int Height => RotationStep % 2 == 0 ? Data.height : Data.width;

    public Vector2Int Anchor
    {
        get
        {
            int w = Data.width, h = Data.height;
            int ax = Data.anchorX, ay = Data.anchorY;
            return RotationStep switch
            {
                0 => new Vector2Int(ax, ay),
                1 => new Vector2Int(ay, w - 1 - ax),       // 90° CW
                2 => new Vector2Int(w - 1 - ax, h - 1 - ay), // 180°
                3 => new Vector2Int(h - 1 - ay, ax),       // 270° CW
                _ => Vector2Int.zero
            };
        }
    }

    public PlacedObject(FurnitureData data, GameObject instance, Vector2Int origin, int rotationStep = 0)
    {
        Data = data;
        Instance = instance;
        Origin = origin;
        RotationStep = rotationStep;
    }
}
