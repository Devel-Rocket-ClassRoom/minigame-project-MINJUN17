using UnityEngine;

[CreateAssetMenu(fileName = "StaffData", menuName = "Staff/StaffData")]
public class StaffData : ScriptableObject
{
    public StaffType Type;
    public long hireCost;
    public long salary;
    public float moveSpeed;
    public float kindness;
}
