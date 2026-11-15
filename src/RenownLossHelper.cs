using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    internal enum RenownLossClanGroup
    {
        Player,
        PlayerKingdom,
        OtherAi
    }

    internal static class RenownLossHelper
    {
        public const float MinCasualtyFactor = 0.5f;
        public const float MaxCasualtyFactor = 1.5f;
        public const float MinOddsFactor = 0.5f;
        public const float MaxOddsFactor = 1.5f;
        public const float MinContributionFactor = 0.1f;
        public const float MaxContributionFactor = 1.0f;
        public const float PartyDestructionMultiplier = 1.25f;
        public const float OtherCapturedLordSeverityPerLord = 0.05f;
        public const float MaxOtherCapturedLordSeverity = 0.25f;
        public const float ExecutionLordBandPercent = 0.01f;
        public const float ExecutionLeaderBandPercent = 0.03f;

        public static bool FeatureEnabled(Settings settings)
        {
            return settings != null && settings.Enabled && settings.RenownLossFromDefeats;
        }

        public static bool IsEligibleClan(Clan clan)
        {
            if (clan == null || clan.IsEliminated || clan.IsBanditFaction)
                return false;
            if (clan == Clan.PlayerClan)
                return true;
            // Mercenary / minor nobles may lose renown; bandits never.
            return true;
        }

        public static RenownLossClanGroup ClassifyClan(Clan clan)
        {
            if (clan == null || clan == Clan.PlayerClan)
                return RenownLossClanGroup.Player;

            Clan playerClan = Clan.PlayerClan;
            if (playerClan != null && playerClan.Kingdom != null && clan.Kingdom == playerClan.Kingdom)
                return RenownLossClanGroup.PlayerKingdom;

            return RenownLossClanGroup.OtherAi;
        }

        public static float GetGroupScale(Settings settings, RenownLossClanGroup group)
        {
            if (settings == null)
                return 0f;

            float scalePercent;
            switch (group)
            {
                case RenownLossClanGroup.Player:
                    if (!settings.AffectPlayerClan)
                        return 0f;
                    scalePercent = settings.PlayerClanLossScale;
                    break;
                case RenownLossClanGroup.PlayerKingdom:
                    if (!settings.AffectAiClans)
                        return 0f;
                    scalePercent = settings.PlayerKingdomClanLossScale;
                    break;
                default:
                    if (!settings.AffectAiClans)
                        return 0f;
                    scalePercent = settings.OtherAiClanLossScale;
                    break;
            }

            return settings.GetGroupScaleFraction(scalePercent);
        }

        public static bool IsClanAffected(Settings settings, Clan clan)
        {
            if (!FeatureEnabled(settings) || !IsEligibleClan(clan))
                return false;
            return GetGroupScale(settings, ClassifyClan(clan)) > 0f;
        }

        public static Clan GetEligiblePartyClan(PartyBase party)
        {
            if (party == null)
                return null;

            if (party.IsMobile)
            {
                MobileParty mobile = party.MobileParty;
                if (mobile == null)
                    return null;
                if (mobile.IsCaravan || mobile.IsVillager || mobile.IsMilitia || mobile.IsBandit)
                    return null;

                Clan clan = mobile.ActualClan;
                if (clan == null && mobile.Owner != null)
                    clan = mobile.Owner.Clan;
                if (clan == null && mobile.LeaderHero != null)
                    clan = mobile.LeaderHero.Clan;
                if (!IsEligibleClan(clan))
                    return null;
                return clan;
            }

            if (party.IsSettlement && party.Settlement != null)
            {
                Clan owner = party.Settlement.OwnerClan;
                if (!IsEligibleClan(owner))
                    return null;
                return owner;
            }

            return null;
        }

        public static bool IsEligibleBattleType(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return false;

            MapEvent.BattleTypes type = mapEvent.EventType;
            if (type == MapEvent.BattleTypes.Hideout
                || type == MapEvent.BattleTypes.Raid
                || type == MapEvent.BattleTypes.IsForcingVolunteers
                || type == MapEvent.BattleTypes.IsForcingSupplies
                || type == MapEvent.BattleTypes.None)
            {
                return false;
            }

            // Field, siege assault, siege-outside, sally-out, blockade / naval siege variants.
            return type == MapEvent.BattleTypes.FieldBattle
                || type == MapEvent.BattleTypes.Siege
                || type == MapEvent.BattleTypes.SiegeOutside
                || type == MapEvent.BattleTypes.SallyOut
                || type == MapEvent.BattleTypes.BlockadeBattle
                || type == MapEvent.BattleTypes.BlockadeSallyOutBattle;
        }

        public static bool IsSiegeRelatedBattle(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return false;
            MapEvent.BattleTypes type = mapEvent.EventType;
            return type == MapEvent.BattleTypes.Siege
                || type == MapEvent.BattleTypes.SiegeOutside
                || type == MapEvent.BattleTypes.SallyOut
                || type == MapEvent.BattleTypes.BlockadeBattle
                || type == MapEvent.BattleTypes.BlockadeSallyOutBattle;
        }

        public static long GetTierBand(ClanTierModel model, int currentTier)
        {
            if (model == null)
                return 1L;

            if (currentTier < model.MinClanTier)
                currentTier = model.MinClanTier;

            int currentRequired = model.GetRequiredRenownForTier(currentTier);
            int previousTier = currentTier - 1;
            if (previousTier < model.MinClanTier)
                previousTier = model.MinClanTier;
            int previousRequired = model.GetRequiredRenownForTier(previousTier);

            long band = (long)currentRequired - (long)previousRequired;
            if (band < 1L)
                band = 1L;
            return band;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        public static float ComputeBattleLossPercent(
            Settings settings,
            float casualtyFraction,
            float ownStrength,
            float enemyStrength,
            float clanContributionFraction,
            bool partyDestroyed,
            bool leaderCaptured,
            int otherCapturedAdultLords,
            float groupScale)
        {
            float basePercent = settings.GetBaseLossFraction();
            float maxPercent = settings.GetMaximumLossFraction();
            if (maxPercent < basePercent)
                maxPercent = basePercent;

            float casualtyFactor = Clamp(0.5f + Clamp01(casualtyFraction), MinCasualtyFactor, MaxCasualtyFactor);

            float ratio = 1f;
            if (enemyStrength > 0.01f)
                ratio = ownStrength / enemyStrength;
            else if (ownStrength > 0.01f)
                ratio = 2f;
            float oddsFactor = Clamp(0.5f + 0.5f * ratio, MinOddsFactor, MaxOddsFactor);

            float contributionFactor = Clamp(clanContributionFraction, MinContributionFactor, MaxContributionFactor);

            float lossPercent = basePercent * casualtyFactor * oddsFactor * contributionFactor * groupScale;

            if (partyDestroyed)
                lossPercent *= PartyDestructionMultiplier;

            float captureMultiplier = settings.GetCaptureMultiplier();
            if (leaderCaptured)
                lossPercent *= captureMultiplier;

            if (otherCapturedAdultLords > 0)
            {
                float extra = OtherCapturedLordSeverityPerLord * (float)otherCapturedAdultLords;
                if (extra > MaxOtherCapturedLordSeverity)
                    extra = MaxOtherCapturedLordSeverity;
                lossPercent *= (1f + extra);
            }

            if (lossPercent > maxPercent * Math.Max(1f, groupScale))
                lossPercent = maxPercent * Math.Max(1f, groupScale);
            // Absolute ceiling still respects configured max when scale is 1; when scale > 1 allow proportional room but keep sane.
            float absoluteMax = maxPercent * 3f;
            if (lossPercent > absoluteMax)
                lossPercent = absoluteMax;
            if (lossPercent < 0f)
                lossPercent = 0f;
            return lossPercent;
        }

        public static float ComputeRenownLossFromPercent(long tierBand, float lossPercent)
        {
            if (tierBand < 1L)
                tierBand = 1L;
            if (lossPercent <= 0f)
                return 0f;

            double raw = (double)tierBand * (double)lossPercent;
            if (raw < 1.0)
                raw = 1.0;
            if (raw > (double)float.MaxValue)
                raw = (double)float.MaxValue;
            return (float)Math.Round(raw);
        }

        public static float GetMinimumRenownFloor(ClanTierModel model, Settings settings, int currentTier)
        {
            if (model == null)
                return 0f;

            int minTier = settings != null ? settings.MinimumClanTierForDecline : 1;
            if (settings != null)
            {
                int configuredMax = settings.GetConfiguredMaxTier();
                if (minTier > configuredMax)
                    minTier = configuredMax;
            }
            if (minTier < model.MinClanTier)
                minTier = model.MinClanTier;
            if (minTier > model.MaxClanTier)
                minTier = model.MaxClanTier;

            // At most one tier down from the battle's starting tier.
            int floorFromOneTier = currentTier - 1;
            if (floorFromOneTier < minTier)
                floorFromOneTier = minTier;
            if (floorFromOneTier < model.MinClanTier)
                floorFromOneTier = model.MinClanTier;

            int required = model.GetRequiredRenownForTier(floorFromOneTier);
            if (required < 0)
                required = 0;
            return (float)required;
        }

        public static float GetMaxLossForPercent(long tierBand, float maxPercent, float groupScale)
        {
            float cappedPercent = maxPercent;
            if (cappedPercent < 0f)
                cappedPercent = 0f;
            if (groupScale > 1f)
                cappedPercent *= groupScale;
            if (cappedPercent > maxPercent * 3f)
                cappedPercent = maxPercent * 3f;
            return ComputeRenownLossFromPercent(tierBand, cappedPercent);
        }

        // Only heroes whose StringIds appear in capturedHeroIds (from this MapEvent via HeroPrisonerTaken).
        public static void CountBattleCaptures(Clan clan, HashSet<string> capturedHeroIds, out bool leaderCaptured, out int otherCaptured)
        {
            leaderCaptured = false;
            otherCaptured = 0;
            if (clan == null || capturedHeroIds == null || capturedHeroIds.Count == 0)
                return;

            if (clan.Leader != null
                && clan.Leader.StringId != null
                && capturedHeroIds.Contains(clan.Leader.StringId))
            {
                leaderCaptured = true;
            }

            MBReadOnlyList<Hero> lords = clan.AliveLords;
            if (lords == null)
                return;

            for (int i = 0; i < lords.Count; i++)
            {
                Hero hero = lords[i];
                if (hero == null || hero.StringId == null || hero.IsChild || !hero.IsLord)
                    continue;
                if (clan.Leader != null && hero == clan.Leader)
                    continue;
                if (!capturedHeroIds.Contains(hero.StringId))
                    continue;
                otherCaptured++;
            }
        }

        public static bool SettlementIsTownOrCastle(Settlement settlement)
        {
            if (settlement == null)
                return false;
            return settlement.IsTown || settlement.IsCastle;
        }

        public static void DebugLog(string message)
        {
            Debug.Print("[ClanTierExpanded][RenownDecline] " + message);
        }
    }
}
