using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace ClanTierExpanded
{
    // Slow companion / kin relation recovery toward the clan leader for high-tier dynasties.
    internal sealed class DynastyBonusBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailyTickClan(Clan clan)
        {
            Settings settings = DynastyBonusHelper.GetEnabledSettings();
            if (settings == null || !settings.DynastyCourtEnabled || !DynastyBonusHelper.ClanEligible(settings, clan))
                return;
            if (clan.Leader == null || settings.DynastyCompanionRelationRecoveryChance <= 0f)
                return;

            int excess = DynastyBonusHelper.GetExcessTier(settings, clan);
            if (excess <= 0)
                return;

            float depth = (float)settings.GetDynastyBonusTierDepth();
            float scale = (float)excess / depth;
            float chance = (settings.DynastyCompanionRelationRecoveryChance / 100f) * scale;
            if (chance <= 0f)
                return;

            Hero leader = clan.Leader;
            foreach (Hero hero in clan.Heroes)
            {
                if (hero == null || hero == leader || !hero.IsAlive || hero.IsChild)
                    continue;

                int relation = leader.GetRelation(hero);
                if (relation >= 50)
                    continue;
                if (MBRandom.RandomFloat > chance)
                    continue;

                // Apply +1 exactly as documented; do not let the diplomacy % boost turn it into +2.
                bool previous = DynastyBonusHelper.SuppressRelationBoost;
                DynastyBonusHelper.SuppressRelationBoost = true;
                try
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(leader, hero, 1, false);
                }
                finally
                {
                    DynastyBonusHelper.SuppressRelationBoost = previous;
                }
            }
        }
    }
}
