using UnityEngine;

public class RiderStaff : Staff
{
    private RiderState _state;

    protected override PathRole GetPathRole() => PathRole.Rider;

    private void ChangeState(RiderState next)
    {
        _state = next;
        _stateTimer = 0f;
    }
}
