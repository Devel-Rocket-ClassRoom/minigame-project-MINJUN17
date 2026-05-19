using System;
using UnityEngine;

public enum RecruitmentTier
{
    Normal,
    High,
    Rare,
}

[Serializable]
public class RecruitmentTierConfig
{
    public RecruitmentTier tier;
    public int satisfactionCost;
    [Range(0, 100)] public int juniorWeight;
    [Range(0, 100)] public int seniorWeight;
    [Range(0, 100)] public int managerWeight;
}

public class RecruitmentTicket
{
    public RecruitmentTier tier;
    public int monthsRemaining;
}
