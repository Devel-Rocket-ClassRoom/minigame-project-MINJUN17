using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;   // 프로젝트에 포함된 DOTween (DOTweenModuleAudio로 AudioSource.DOFade 지원)

/// <summary>
/// BGM / SFX 중앙 관리 싱글톤.
/// - StartScene에 1개 배치 → DontDestroyOnLoad로 씬 전환에도 유지.
/// - 호출: SoundManager.Instance?.PlaySfx(SfxId.XXX) / PlayBgm(clip).
/// - 볼륨은 PlayerPrefs에 저장 (설정 UI 슬라이더에서 SetBgmVolume/SetSfxVolume 호출).
/// </summary>
[DefaultExecutionOrder(-100)]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    /// <summary>
    /// 어디서든 매니저를 가져온다. 씬에 없으면 Resources/SoundManager 프리팹을 자동 생성.
    /// (게임씬 단독 실행에도 BGM/SFX가 동작하도록 보장)
    /// </summary>
    public static SoundManager Get()
    {
        if (Instance == null)
        {
            var prefab = Resources.Load<SoundManager>("SoundManager");
            if (prefab != null) Instantiate(prefab);   // Awake에서 Instance 세팅됨
        }
        return Instance;
    }

    [Header("참조")]
    [SerializeField] private SfxLibrary sfxLibrary;
    [SerializeField] private AudioSource bgmSource;   // loop 전용
    [SerializeField] private AudioSource sfxSource;   // PlayOneShot 전용

    [Header("기본 볼륨")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private readonly Dictionary<SfxId, SfxLibrary.Entry> _map = new();

    private const string kBgmPref = "audio.bgm";
    private const string kSfxPref = "audio.sfx";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmVolume = PlayerPrefs.GetFloat(kBgmPref, bgmVolume);
        sfxVolume = PlayerPrefs.GetFloat(kSfxPref, sfxVolume);

        if (sfxLibrary != null && sfxLibrary.entries != null)
            foreach (var e in sfxLibrary.entries)
            {
                var entry = e;
                if (entry.startTrim > 0f && entry.clip != null)
                    entry.clip = TrimStart(entry.clip, entry.startTrim);
                _map[entry.id] = entry;
            }

        if (bgmSource != null) { bgmSource.loop = true; bgmSource.volume = bgmVolume; }
    }

    // ─────────────────────────────────────── SFX
    public void PlaySfx(SfxId id)
    {
        if (id == SfxId.None || sfxSource == null) return;
        if (!_map.TryGetValue(id, out var e) || e.clip == null) return;
        float v = (e.volume <= 0f ? 1f : e.volume) * sfxVolume;
        sfxSource.PlayOneShot(e.clip, v);
    }

    /// <summary>
    /// 클립 앞부분 seconds초를 잘라낸 새 AudioClip 생성. (원본은 보존)
    /// 시작 무음/싱크 지연 제거용. 짧은 SFX 전용.
    /// ※ 클립 Import Settings의 Load Type을 'Decompress On Load'로 둬야 GetData가 정상 동작.
    /// </summary>
    private static AudioClip TrimStart(AudioClip src, float seconds)
    {
        if (src == null || seconds <= 0f) return src;

        int startSample = Mathf.Clamp(Mathf.RoundToInt(seconds * src.frequency), 0, src.samples);
        int remaining = src.samples - startSample;
        if (remaining <= 0) return src;   // 자를 양이 클립보다 길면 원본 유지

        int ch = src.channels;
        var data = new float[src.samples * ch];
        if (!src.GetData(data, 0)) return src;   // 로드 타입 문제 시 원본 유지

        var trimmed = new float[remaining * ch];
        System.Array.Copy(data, startSample * ch, trimmed, 0, remaining * ch);

        var clip = AudioClip.Create($"{src.name}_trim", remaining, ch, src.frequency, false);
        clip.SetData(trimmed, 0);
        return clip;
    }

    // ─────────────────────────────────────── BGM (크로스페이드)
    public void PlayBgm(AudioClip clip, float fade = 0.5f)
    {
        if (bgmSource == null || clip == null || bgmSource.clip == clip) return;

        bgmSource.DOKill();
        if (bgmSource.isPlaying && fade > 0f)
        {
            bgmSource.DOFade(0f, fade).OnComplete(() =>
            {
                bgmSource.clip = clip;
                bgmSource.Play();
                bgmSource.DOFade(bgmVolume, fade);
            });
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }
    }

    public void StopBgm(float fade = 0.5f)
    {
        if (bgmSource == null) return;
        bgmSource.DOKill();
        if (fade > 0f) bgmSource.DOFade(0f, fade).OnComplete(() => bgmSource.Stop());
        else           bgmSource.Stop();
    }

    // ─────────────────────────────────────── 볼륨 (설정 UI에서 호출)
    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    public void SetBgmVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        if (bgmSource != null) bgmSource.volume = bgmVolume;
        PlayerPrefs.SetFloat(kBgmPref, bgmVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(kSfxPref, sfxVolume);
        PlayerPrefs.Save();
    }
}
