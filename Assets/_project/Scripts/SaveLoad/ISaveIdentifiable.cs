/// <summary>
/// 세이브/로드 시 ScriptableObject를 식별하기 위한 영구 문자열 ID 인터페이스.
/// 모든 직렬화 대상 SO (FurnitureData, MenuData, StaffData, ExpansionStageData 등)가 구현.
/// </summary>
public interface ISaveIdentifiable
{
    string SaveId { get; }
}
