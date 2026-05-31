using System;
using System.Collections.Generic;

/// <summary>
/// 세이브 파일 최상위 구조. Phase가 진행되면서 필드가 추가됨.
/// JSON 직렬화 대상 — 모든 필드는 public, 기본 생성자 가능해야 함.
/// </summary>
[Serializable]
public class SaveData
{
    public int  version = 1;
    public long timestampTicks;     // System.DateTime.UtcNow.Ticks

    // Phase 1
    public MoneyData        money;
    public SatisfactionData satisfaction;

    // Phase 2
    public TimeData         time;
    public ReputationData   reputation;
    public SalesData        sales;

    // Phase 3
    public ExpansionData    expansion;
    public PlacementData[]  placements;

    // Phase 4
    public StaffSaveData[]  staff;

    // Phase 5
    public CatalogData       catalog;
    public MarketingSaveData marketing;
    public CandidatePoolData candidatePool;

    // Phase 6
    public RankingData       ranking;

    // 손님 해금 (누적 만족도 임계점으로 랜덤 해금된 손님 목록)
    public CustomerUnlockData customers;
}

[Serializable]
public class CustomerUnlockData
{
    public List<string> unlockedIds;     // CustomerData saveId 목록
    public List<string> introducedIds;   // "새 손님 등장" 팝업을 이미 띄운 손님 saveId 목록 (중복 노출 방지)
}

[Serializable]
public class MoneyData
{
    public long money;
}

[Serializable]
public class SatisfactionData
{
    public int  satisfaction;
    public long lifetimeSatisfaction;   // 평생 누적(초기화/차감 없음) — 손님 해금 임계점용
}

[Serializable]
public class TimeData
{
    public int   hour;
    public int   month;
    public int   year;
    public bool  ticking;       // 영업 중 여부
    public float timer;         // 현재 시간 진행도 (0~hourInterval)
}

[Serializable]
public class ReputationData
{
    public long annualReputation;
}

[Serializable]
public class SalesData
{
    public long annualRevenue;
    public List<MonthlySaleEntry> monthlySales;

    // 정보 탭 통계 (구버전 세이브엔 없음 → 로드 시 0/빈 값으로 들어옴, 호환 OK)
    public long lifetimeRevenue;     // 총매출 (누적, 리셋 없음)
    public long monthlyRevenueAcc;   // 진행 중인 이번 달 매출 (아직 정산 안 됨)
    public long totalCustomers;      // 누적 방문 손님 수
    public List<MonthlyRevenueEntry> monthlyHistory;   // 월별 매출 기록 (그래프용)
}

[Serializable]
public class MonthlySaleEntry
{
    public string menuId;
    public int    count;
}

[Serializable]
public class MonthlyRevenueEntry
{
    public int  year;
    public int  month;     // 1~12
    public long revenue;
}

[Serializable]
public class ExpansionData
{
    public int currentStage;
}

[Serializable]
public class PlacementData
{
    public string furnitureSaveId;
    public int    originX;
    public int    originY;
    public int    rotationStep;
}

[Serializable]
public class StaffSaveData
{
    public string role;            // "Cook" / "Server" / "Rider"
    public string staffDataSaveId; // 현재 등급 SO ID
    public string nameKey;
    public int    id;
    public int    tenureMonths;
    public int    growthBumps;
    public float  hireVariance;
    public float  posX, posY, posZ;
}

// ─────────────────────────────────────── Phase 5

[Serializable]
public class CatalogData
{
    public List<string> unlockedFurnitureIds;
    public List<string> unlockedMenuIds;
}

[Serializable]
public class MarketingSaveData
{
    public List<ActiveCampaignEntry> active;
    public List<string>              pending;   // MarketingData saveIds
}

[Serializable]
public class ActiveCampaignEntry
{
    public string marketingSaveId;
    public int    remainingMonths;
}

[Serializable]
public class CandidatePoolData
{
    public List<CandidateEntry> applicants;
    public List<TicketEntry>    pendingTickets;
    public bool                 purchasedThisMonth;
}

[Serializable]
public class CandidateEntry
{
    public string candidateName;
    public string staffDataSaveId;
    public float  hireVariance;
}

[Serializable]
public class TicketEntry
{
    public int tier;             // RecruitmentTier 캐스팅
    public int monthsRemaining;
}

// ─────────────────────────────────────── Phase 6

[Serializable]
public class RankingData
{
    public bool hasLastResult;   // false면 아직 연말 정산 한 번도 안 함
    public long score;
    public bool qualified;
    public int  rank;
    public long revenue;
    public long reputation;
    public int  bestRank;        // 역대 최고(가장 낮은 숫자) 순위. 0=기록 없음.
}
