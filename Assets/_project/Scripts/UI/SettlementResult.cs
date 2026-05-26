public struct SettlementResult
{
    public long MaterialCost;   // 재료비
    public long SalaryCost;     // 급여
    public long OperationCost;  // 운영비(유지비)
    public long TotalExpense => MaterialCost + SalaryCost + OperationCost;
    /// <summary>총합 — 지출액을 음수로 표시 (UI에 그대로 사용).</summary>
    public long NetProfit    => -TotalExpense;
}