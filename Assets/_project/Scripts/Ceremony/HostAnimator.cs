using UnityEngine;

/// <summary>
/// 시상식 사회자 애니메이션 상태 전환기.
/// 사회자 밑에 상태별 자식(각각 Image + UIImageAnimator)을 두고 하나만 켠다.
///   - Walk     : 걷기 (loop)
///   - Idle     : 가만히 (loop)
///   - Envelope : 편지 꺼내기 (보통 한 번만 재생 = UIImageAnimator의 loop 끄기)
///
/// 프레임 재생은 기존 UIImageAnimator를 그대로 재활용. 여기선 활성 전환만 담당.
/// HostEntrance / CeremonyDirector가 PlayWalk/PlayIdle/PlayEnvelope 호출.
/// </summary>
public class HostAnimator : MonoBehaviour
{
    [Header("상태별 애니 (각자 Image + UIImageAnimator)")]
    [SerializeField] private UIImageAnimator walk;
    [SerializeField] private UIImageAnimator idle;
    [SerializeField] private UIImageAnimator envelope;

    [Tooltip("시작할 때 보여줄 상태")]
    [SerializeField] private State initial = State.Idle;

    public enum State { None, Walk, Idle, Envelope }

    private void Awake() => Show(initial);

    public void PlayWalk()     => Show(State.Walk);
    public void PlayIdle()     => Show(State.Idle);
    public void PlayEnvelope() => Show(State.Envelope);

    /// <summary>편지 꺼내기 애니 한 번 재생 길이(초). 끝나는 타이밍 계산용.</summary>
    public float EnvelopeDuration => envelope != null ? envelope.Duration : 0f;

    /// <summary>지정 상태만 켜고 나머지는 끔. 켠 애니는 처음부터 재생.</summary>
    public void Show(State state)
    {
        Apply(walk,     state == State.Walk);
        Apply(idle,     state == State.Idle);
        Apply(envelope, state == State.Envelope);
    }

    private static void Apply(UIImageAnimator anim, bool on)
    {
        if (anim == null) return;
        anim.gameObject.SetActive(on);
        if (on) anim.Play();   // playOnEnable 설정과 무관하게 항상 처음부터
    }
}
