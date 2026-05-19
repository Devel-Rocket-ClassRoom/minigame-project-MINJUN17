using UnityEngine;

[CreateAssetMenu(fileName = "StaffData", menuName = "Staff/StaffData")]
public class StaffData : ScriptableObject
{
    public StaffRole role;           // Cook / Server / Rider 
    public StaffType grade;          // Junior / Senior / Manager 

    [Header("외형")]
    public Sprite sprite;

    [Header("능력치")]
    public float moveSpeed;
    public float kindness;
    public float speedMultiplier;   
    public long hireCost;
    public long salary;
}
