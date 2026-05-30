using UnityEngine;

/// <summary>
/// SfxId → AudioClip 매핑 ScriptableObject. 인스펙터에서 종류별 클립을 꽂는다.
/// 메뉴: Create > Audio > SFX Library
/// </summary>
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Audio/SFX Library")]
public class SfxLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;   // 0이면 1로 취급 (SoundManager에서 보정)
        [Tooltip("클립 앞부분을 잘라낼 시간(초). 시작 무음/싱크 지연 제거용. 0이면 자르지 않음")]
        public float startTrim;
    }

    public Entry[] entries;
}
