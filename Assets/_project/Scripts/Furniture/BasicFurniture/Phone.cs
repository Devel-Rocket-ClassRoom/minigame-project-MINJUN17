using UnityEngine;

public class Phone : MonoBehaviour
{
    [SerializeField] private float minCallTimer = 8f;
    [SerializeField] private float maxCallTimer = 20f;

    private bool _isRinging;
    private float _ringTimer;
    private ServerStaff _claimer;

    public float MinCallTimer => minCallTimer;
    public float MaxCallTimer => maxCallTimer;
    public bool IsRinging => _isRinging;
    public float RingTimer => _ringTimer;
    public ServerStaff Claimer => _claimer;
    public bool IsClaimedByOther(ServerStaff me) => _claimer != null && _claimer != me;

    public bool TryClaim(ServerStaff server)
    {
        if (_claimer != null && _claimer != server) return false;
        _claimer = server;
        return true;
    }

    public void ReleaseClaim(ServerStaff server)
    {
        if (_claimer == server) _claimer = null;
    }

    private void Awake()
    {
        PhoneManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (PhoneManager.Instance != null)
            PhoneManager.Instance.Unregister(this);
    }

    private void Update()
    {
        if (_isRinging) _ringTimer += Time.deltaTime;
    }

    public void StartRinging()
    {
        _isRinging = true;
        _ringTimer = 0f;
    }

    public void StopRinging()
    {
        _isRinging = false;
        _ringTimer = 0f;
        _claimer = null;
    }
}
