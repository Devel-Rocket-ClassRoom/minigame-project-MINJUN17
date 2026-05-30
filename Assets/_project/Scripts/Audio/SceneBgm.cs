using UnityEngine;

/// <summary>
/// 씬에 배치하면 Start 시 지정한 BGM을 자동 재생. 씬마다 다른 곡을 쉽게 깔 때 사용.
/// </summary>
public class SceneBgm : MonoBehaviour
{
    [SerializeField] private AudioClip bgm;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Start()
    {
        if (bgm != null) SoundManager.Get()?.PlayBgm(bgm, fadeDuration);
    }
}
