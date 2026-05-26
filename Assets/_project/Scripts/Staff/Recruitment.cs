using System;
using UnityEngine;
using UnityEngine.Localization;

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

    [Header("해금 패널 표시용")]
    public LocalizedString displayName;
    public LocalizedString description;
    public Sprite icon;

    [Header("비용 / 가중치")]
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
