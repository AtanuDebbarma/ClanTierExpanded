using System;
using TaleWorlds.ModuleManager;

namespace ClanTierExpanded
{
    internal static class ModuleDetect
    {
        private const string CaravanModuleId = "CaravanPartyLimitIncreasedFixed";
        private const string LegacyCaravanModuleId = "CaravanPartyLimitIncreased";
        private const string CompanionPartyLimitsId = "CompanionPartyLimitsExpanded";

        internal static bool CaravanPartyLimitIncreasedSelected()
        {
            try
            {
                if (ModuleHelper.IsModuleActive(CaravanModuleId))
                    return true;
                // Soft accept legacy id during rename transition / old launcher lists.
                return ModuleHelper.IsModuleActive(LegacyCaravanModuleId);
            }
            catch
            {
                return false;
            }
        }

        // When CPLE is loaded it replaces party limits for the clans it covers.
        // Extra high-tier slots must not stack on top of those values.
        internal static bool CompanionPartyLimitsExpandedActive()
        {
            try
            {
                return ModuleHelper.IsModuleActive(CompanionPartyLimitsId);
            }
            catch
            {
                return false;
            }
        }
    }
}
