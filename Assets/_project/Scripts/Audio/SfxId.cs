/// <summary>
/// 효과음 종류. 필요할 때마다 여기에 추가하면 됨 (SfxLibrary 에셋에서 클립 매핑).
/// </summary>
public enum SfxId
{
    None = 0,

    ButtonClick,      // 모든 버튼 클릭
    CustomerPopup,    // 새 손님 팝업 등장
    Drumroll,         // 연말 랭킹 발표 직전 두구두구
    RankingReveal,    // 랭킹 발표되는 순간
    SettlementItem,   // 월정산 내역 하나씩 뜰 때마다

    Upgrade,          // (기존) 업그레이드/해금
}
