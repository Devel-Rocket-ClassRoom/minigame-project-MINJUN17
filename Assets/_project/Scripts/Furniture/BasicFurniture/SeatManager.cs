using System.Collections.Generic;
using UnityEngine;

public class SeatManager : MonoBehaviour
{
    public static SeatManager Instance;
    private List<Seat> seats = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // FindObjectsByType 제거
    }

    public Seat GetFirstAvailableSeat()
    {
        foreach (Seat seat in seats)
        {
            if (!seat.IsOccupied)
            {
                return seat;
            }
        }
        return null;
    }
    public void RegisterSeat(Seat seat)
    {
        if (!seats.Contains(seat)) seats.Add(seat);
    }

    public void UnregisterSeat(Seat seat)
    {
        seats.Remove(seat);
    }
}
