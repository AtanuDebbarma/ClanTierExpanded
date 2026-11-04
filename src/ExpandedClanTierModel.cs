using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace ClanTierExpanded
{
    // Clan tier / renown model for Clan Tier Expanded.
    internal sealed class ExpandedClanTierModel : DefaultClanTierModel
    {
        public override int MaxClanTier
        {
            get
            {
                Settings settings = Settings.Instance;
                if (settings == null || !settings.Enabled)
                    return base.MaxClanTier;
                return settings.GetConfiguredMaxTier();
            }
        }

        public override int GetRequiredRenownForTier(int tierLevel)
        {
            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled)
            {
                // Clamp so a leftover high Tier from CTE cannot index vanilla's 7-element array.
                if (tierLevel < MinClanTier)
                    tierLevel = MinClanTier;
                if (tierLevel > base.MaxClanTier)
                    tierLevel = base.MaxClanTier;
                return base.GetRequiredRenownForTier(tierLevel);
            }

            int[] limits = settings.GetRenownLimits();
            if (limits == null || limits.Length == 0)
                return base.GetRequiredRenownForTier(tierLevel);
            if (tierLevel < 0)
                return limits[0];
            if (tierLevel >= limits.Length)
                return limits[limits.Length - 1];
            return limits[tierLevel];
        }

        public override int CalculateTier(Clan clan)
        {
            if (clan == null)
                return MinClanTier;

            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled)
                return base.CalculateTier(clan);

            int result = MinClanTier;
            int[] limits = settings.GetRenownLimits();
            if (limits == null || limits.Length == 0)
                return base.CalculateTier(clan);

            int max = MaxClanTier;
            for (int tier = MinClanTier + 1; tier <= max; tier++)
            {
                int required = tier < limits.Length ? limits[tier] : limits[limits.Length - 1];
                if (clan.Renown >= required)
                    result = tier;
            }
            return result;
        }

        public override int CalculateInitialRenown(Clan clan)
        {
            if (clan == null)
                return 0;

            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled)
                return base.CalculateInitialRenown(clan);

            int tier = clan.Tier;
            int lower = GetRequiredRenownForTier(tier);
            int upper;
            if (tier < MaxClanTier)
            {
                upper = GetRequiredRenownForTier(tier + 1);
            }
            else
            {
                int maxRequired = GetRequiredRenownForTier(MaxClanTier);
                if (maxRequired > int.MaxValue - 1500)
                    upper = int.MaxValue;
                else
                    upper = maxRequired + 1500;
            }

            if (upper <= lower)
            {
                if (lower >= int.MaxValue)
                    return int.MaxValue;
                upper = lower + 1;
            }

            int span = upper - lower;
            int high = upper - (int)(span * 0.4f);
            if (high <= lower)
            {
                if (lower >= int.MaxValue - 1)
                    return lower;
                high = lower + 1;
            }

            if (high <= lower)
                return lower;
            return MBRandom.RandomInt(lower, high);
        }

        public override int GetPartyLimitForTier(Clan clan, int clanTierToConsider)
        {
            int limit = base.GetPartyLimitForTier(clan, clanTierToConsider);

            Settings settings = Settings.Instance;
            if (clan == null || settings == null || !settings.Enabled || !settings.ExtraPartiesAtHighTiers)
                return limit;

            // CPLE replaces party limits; stacking CTE bonuses would exceed its MCM values.
            if (ModuleDetect.CompanionPartyLimitsExpandedActive())
                return limit;

            if (clanTierToConsider >= 7)
                limit++;
            if (clanTierToConsider >= 9)
                limit++;
            if (clanTierToConsider >= 11)
                limit++;
            return limit;
        }
    }
}
