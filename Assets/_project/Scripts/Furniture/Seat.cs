using UnityEngine;

public class Seat : MonoBehaviour
{
    private bool _isOccupied;
    public bool IsOccupied => _isOccupied;
    public void Occupy() => _isOccupied = true;
    public void Release() => _isOccupied = false;
}
