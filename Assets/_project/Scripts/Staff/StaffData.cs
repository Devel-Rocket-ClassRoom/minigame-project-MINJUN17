using UnityEngine;

[CreateAssetMenu(fileName = "StaffData", menuName = "Staff/StaffData")]
public class StaffData : ScriptableObject
{
    public StaffType Type;
    public long HireCost;
    public long Salary;
    public float Speed;
    public float Kindness;
}
