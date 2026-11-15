using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ClanTierExpanded
{
    // Shared scaling for high-tier dynasty bonuses. Excess tier = clamp(tier - 6, 0, depth).
    internal static class DynastyBonusHelper
    {
        private static readonly TextObject LogisticsText = new TextObject("{=cte_dynasty_logistics}Clan dynasty (logistics)");
        private static readonly TextObject CourtText = new TextObject("{=cte_dynasty_court}Clan dynasty (court)");
        private static readonly TextObject ReputationText = new TextObject("{=cte_dynasty_reputation}Clan dynasty (reputation)");
        private static readonly TextObject TradeText = new TextObject("{=cte_dynasty_trade}Clan dynasty (trade)");
        private static readonly TextObject MilestoneText = new TextObject("{=cte_dynasty_milestone}Clan dynasty (milestone)");

        // Suppresses the diplomacy relation % boost while our own daily +1 recovery runs
        // (otherwise +1 becomes +2 through GetEffectiveRelationChange).
        [System.ThreadStatic]
        private static bool _suppressRelationBoost;

        internal static TextObject GetLogisticsText()
        {
            return LogisticsText;
        }

        internal static TextObject GetCourtText()
        {
            return CourtText;
        }

        internal static TextObject GetReputationText()
        {
            return ReputationText;
        }

        internal static TextObject GetTradeText()
        {
            return TradeText;
        }

        internal static TextObject GetMilestoneText()
        {
            return MilestoneText;
        }

        internal static bool SuppressRelationBoost
        {
            get { return _suppressRelationBoost; }
            set { _suppressRelationBoost = value; }
        }

        internal static bool IsActiveCampaignModel(object instance, object activeModel)
        {
            if (instance == null || activeModel == null)
                return false;
            return object.ReferenceEquals(instance, activeModel);
        }

        internal static Settings GetEnabledSettings()
        {
            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.HighTierDynastyBonuses)
                return null;
            return settings;
        }

        internal static bool ClanEligible(Settings settings, Clan clan)
        {
            if (settings == null || clan == null || clan.IsEliminated || clan.IsBanditFaction)
                return false;
            if (clan == Clan.PlayerClan)
                return settings.DynastyAffectPlayerClan;
            return settings.DynastyAffectAiClans && clan.IsNoble;
        }

        internal static int GetExcessTier(Settings settings, Clan clan)
        {
            if (clan == null)
                return 0;
            int excess = clan.Tier - Settings.DynastyBaseTier;
            if (excess <= 0)
                return 0;
            int depth = settings.GetDynastyBonusTierDepth();
            if (excess > depth)
                return depth;
            return excess;
        }

        internal static Clan ClanFromParty(MobileParty party)
        {
            if (party == null)
                return null;
            if (party.ActualClan != null)
                return party.ActualClan;
            // PartyBase.get_Owner can NullReference on broken/garrison parties; never let that escape a postfix.
            try
            {
                Hero owner = party.Owner;
                if (owner != null)
                    return owner.Clan;
            }
            catch (NullReferenceException)
            {
            }
            if (party.LeaderHero != null)
                return party.LeaderHero.Clan;
            return null;
        }

        internal static Clan ClanFromPartyBase(PartyBase party)
        {
            if (party == null)
                return null;
            if (party.MobileParty != null)
                return ClanFromParty(party.MobileParty);
            try
            {
                Hero owner = party.Owner;
                if (owner != null)
                    return owner.Clan;
            }
            catch (NullReferenceException)
            {
            }
            return null;
        }

        internal static float PercentToFactor(float percentPoints)
        {
            if (percentPoints <= 0f)
                return 0f;
            return percentPoints / 100f;
        }

        internal static float ScaledPercentFactor(Settings settings, Clan clan, float percentPerTier)
        {
            int excess = GetExcessTier(settings, clan);
            if (excess <= 0 || percentPerTier <= 0f)
                return 0f;
            return PercentToFactor(percentPerTier) * (float)excess;
        }

        internal static void GetSpeedFactors(MobileParty party, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = ClanFromParty(party);
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyLogisticsEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastySpeedPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier7) && settings.DynastyMilestoneTier7Choice == 0)
                milestone = 0.02f;
        }

        internal static void GetWageDiscountFactors(MobileParty party, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = ClanFromParty(party);
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyLogisticsEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyWageDiscountPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier7) && settings.DynastyMilestoneTier7Choice == 1)
                milestone = 0.02f;
        }

        internal static void GetFoodSavingFactors(MobileParty party, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = ClanFromParty(party);
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyLogisticsEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyFoodSavingPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier13) && settings.DynastyMilestoneTier13Choice == 1)
                milestone = 0.03f;
        }

        internal static void GetHealingFactors(PartyBase party, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = ClanFromPartyBase(party);
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyLogisticsEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyHealingPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier13) && settings.DynastyMilestoneTier13Choice == 0)
                milestone = 0.03f;
        }

        internal static void GetInfluenceFactors(Clan clan, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyCourtEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyInfluencePercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier9) && settings.DynastyMilestoneTier9Choice == 0)
                milestone = 0.03f;
        }

        internal static float GetRelationBoostFactor(Clan clan)
        {
            if (_suppressRelationBoost)
                return 0f;
            Settings settings = GetEnabledSettings();
            if (settings == null || !settings.DynastyCourtEnabled || !ClanEligible(settings, clan))
                return 0f;
            return ScaledPercentFactor(settings, clan, settings.DynastyRelationBoostPercentPerTier);
        }

        // Maps a fractional percentage onto integer relation changes:
        // floor(expected) always, then +1 with probability equal to the remainder.
        // Example: +1 at 6% => 6% chance of +1 extra; +10 at 6% => 60% chance of +1.
        internal static int RollRelationBonus(int baseChange, float boostFactor)
        {
            if (baseChange <= 0 || boostFactor <= 0f)
                return 0;

            double expected = (double)baseChange * (double)boostFactor;
            int guaranteed = (int)System.Math.Floor(expected + 1e-9);
            double frac = expected - guaranteed;
            int bonus = guaranteed;
            if (frac > 1e-9 && MBRandom.RandomFloat < (float)frac)
                bonus++;
            return bonus;
        }

        internal static void GetLoyaltyBonuses(Clan ownerClan, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            if (settings == null || !ClanEligible(settings, ownerClan))
                return;
            if (settings.DynastyReputationEnabled)
                scaled = settings.DynastyLoyaltyPerExcessTier * (float)GetExcessTier(settings, ownerClan);
            if (MilestonesReady(settings, ownerClan, Settings.MilestoneTier9) && settings.DynastyMilestoneTier9Choice == 1)
                milestone = 0.15f;
        }

        internal static float GetVolunteerFactor(Settlement settlement)
        {
            Clan owner = OwnerClanOfSettlement(settlement);
            Settings settings = GetEnabledSettings();
            if (settings == null || !settings.DynastyReputationEnabled || !ClanEligible(settings, owner))
                return 0f;
            return ScaledPercentFactor(settings, owner, settings.DynastyVolunteerPercentPerTier);
        }

        internal static float GetMilitiaFactor(Settlement settlement)
        {
            Clan owner = OwnerClanOfSettlement(settlement);
            Settings settings = GetEnabledSettings();
            if (settings == null || !settings.DynastyReputationEnabled || !ClanEligible(settings, owner))
                return 0f;
            return ScaledPercentFactor(settings, owner, settings.DynastyMilitiaPercentPerTier);
        }

        internal static void GetCaravanIncomeFactors(MobileParty caravan, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = ClanFromParty(caravan);
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyTradeEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyCaravanPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier11) && settings.DynastyMilestoneTier11Choice == 0)
                milestone = 0.05f;
        }

        internal static void GetWorkshopIncomeFactors(Workshop workshop, out float scaled, out float milestone)
        {
            scaled = 0f;
            milestone = 0f;
            Settings settings = GetEnabledSettings();
            Clan clan = null;
            if (workshop != null && workshop.Owner != null)
                clan = workshop.Owner.Clan;
            if (settings == null || !ClanEligible(settings, clan))
                return;
            if (settings.DynastyTradeEnabled)
                scaled = ScaledPercentFactor(settings, clan, settings.DynastyWorkshopPercentPerTier);
            if (MilestonesReady(settings, clan, Settings.MilestoneTier11) && settings.DynastyMilestoneTier11Choice == 1)
                milestone = 0.05f;
        }

        internal static Clan OwnerClanOfSettlement(Settlement settlement)
        {
            if (settlement == null)
                return null;
            return settlement.OwnerClan;
        }

        private static bool MilestonesReady(Settings settings, Clan clan, int requiredTier)
        {
            if (settings == null || clan == null || !settings.DynastyMilestonesEnabled || !ClanEligible(settings, clan))
                return false;
            return clan.Tier >= requiredTier;
        }

        // Only boost gains. Never amplify negative owner income from third-party finance models.
        internal static int ScaleIncome(int baseIncome, float factor)
        {
            if (factor == 0f || baseIncome <= 0)
                return baseIncome;
            int scaled = (int)System.Math.Round((double)baseIncome * (1.0 + (double)factor));
            if (scaled < baseIncome)
                return baseIncome;
            return scaled;
        }

        internal static int ScaleIncome(int baseIncome, float scaledFactor, float milestoneFactor)
        {
            return ScaleIncome(baseIncome, scaledFactor + milestoneFactor);
        }
    }
}
