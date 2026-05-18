using System.Collections.Generic;
using UnityEngine;

public class SeatManager : MonoBehaviour
{
    private List<Seat> seats = new();
    private void Awake()
    {
        seats.AddRange(FindObjectsByType<Seat>(FindObjectsSortMode.None));
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
