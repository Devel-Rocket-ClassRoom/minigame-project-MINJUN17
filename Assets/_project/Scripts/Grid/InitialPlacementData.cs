using UnityEngine;

[CreateAssetMenu(fileName = "InitialPlacementData", menuName = "Initial/PlacementData")]
public class InitialPlacementData : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public FurnitureData furniture;
        public Vector2Int position;
        public int rotationStep;   // 0=0°, 1=90°, 2=180°, 3=270°
    }

    public Entry[] entries;
}
