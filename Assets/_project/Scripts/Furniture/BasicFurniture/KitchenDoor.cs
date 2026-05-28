using UnityEngine;

// 주방-홀 연결문 연출. 통근(출퇴근) 중인 직원이 가까이 오면 열고, 멀어지면 닫는다.
// 통행 가능 여부와는 무관 — 그리드 워크빌리티가 통행을 결정하고, 이건 비주얼만 담당.
public class KitchenDoor : MonoBehaviour
{
    [SerializeField] private Animator animator;     // "Speed" float 파라미터 사용 (1=열림 재생, -1=닫힘 역재생)
    [SerializeField] private float openRadius = 1.5f;   // 키우면 더 일찍 열림
    [SerializeField] private float closeDelay = 1.0f;   // 마지막 직원이 멀어진 뒤 닫히기까지 대기(초)
    private bool _open;
    private float _closeTimer;

    private void Update()
    {
        if (StaffManager.Instance == null) return;

        bool near = false;
        foreach (var s in StaffManager.Instance.GetAllStaffs())
        {
            if (s == null || !s.gameObject.activeSelf) continue;
            if (!s.IsCommuting) continue;   // 출퇴근 중인 직원만 문을 연다
            if (Vector3.Distance(s.transform.position, transform.position) <= openRadius)
            {
                near = true;
                break;
            }
        }

        if (near)
        {
            _closeTimer = closeDelay;                 // 근처면 타이머 리셋(계속 열림)
            if (!_open) { _open = true; SetSpeed(1f); }
        }
        else if (_open)
        {
            _closeTimer -= Time.deltaTime;            // 멀어지면 카운트다운
            if (_closeTimer <= 0f) { _open = false; SetSpeed(-1f); }
        }

        // Door_Open clip은 LoopTime=false. Speed=-1로 역재생하면 normalizedTime이 0을 넘어
        // 음수로 무한 누적 → 다음 열림 시 음수 시간 climb-up 동안 보이는 변화가 없는 버그.
        // 양 끝에 도달하면 Speed=0으로 정지시켜 누적 방지.
        ClampAtEnds();
    }

    private void ClampAtEnds()
    {
        if (animator == null) return;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        float currentSpeed = animator.GetFloat("Speed");

        if (currentSpeed < 0f && info.normalizedTime <= 0f)
        {
            animator.Play(info.fullPathHash, 0, 0f);
            SetSpeed(0f);
        }
        else if (currentSpeed > 0f && info.normalizedTime >= 1f)
        {
            animator.Play(info.fullPathHash, 0, 1f);
            SetSpeed(0f);
        }
    }

    private void SetSpeed(float v)
    {
        if (animator != null) animator.SetFloat("Speed", v);
    }
}
