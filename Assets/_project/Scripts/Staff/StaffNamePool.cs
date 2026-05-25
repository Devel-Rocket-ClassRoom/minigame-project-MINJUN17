using UnityEngine;
using UnityEngine.Localization.Settings;

[CreateAssetMenu(fileName = "StaffNamePool", menuName = "Staff/StaffNamePool")]
public class StaffNamePool : ScriptableObject
{
    [Header("String Table 이름 (StaffNames CSV 가 import 된 테이블)")]
    [SerializeField] private string tableName = "StaffNames";

    [Header("자동 생성 키 (수동 키가 비어있을 때만 사용)")]
    [Tooltip("키 prefix — CSV 의 Key 컬럼 앞부분")]
    [SerializeField] private string keyPrefix = "staff.name.";
    [Tooltip("자동 생성할 키 개수 (1 ~ Count)")]
    [SerializeField] private int autoCount = 50;
    [Tooltip("번호 패딩 자릿수 (3 → \"001\")")]
    [SerializeField] private int padding = 3;

    [Header("수동 키 (지정 시 자동 생성 무시)")]
    [SerializeField] private string[] manualKeys;

    /// <summary>랜덤 키 1개 반환. 채용 시점에 호출.</summary>
    public string PickRandomKey()
    {
        if (manualKeys != null && manualKeys.Length > 0)
            return manualKeys[Random.Range(0, manualKeys.Length)];

        if (autoCount <= 0) return null;
        int idx = Random.Range(1, autoCount + 1);
        return keyPrefix + idx.ToString().PadLeft(padding, '0');
    }

    /// <summary>키 → 현재 로케일 문자열. 미해석/오류 시 키 자체 반환 (디버그용).</summary>
    public string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        try
        {
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(tableName, key);
            return string.IsNullOrEmpty(s) ? key : s;
        }
        catch
        {
            return key;
        }
    }
}
