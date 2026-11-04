using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ClanTierExpanded
{
    // Clan Tier Expanded for Bannerlord 1.4.8 (module id ClanTierExpanded).
    public class SubModule : MBSubModuleBase
    {
        public const string HarmonyId = "com.atanu.bannerlord.ClanTierExpanded";
        private readonly Harmony _harmony = new Harmony(HarmonyId);

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony.PatchAll(typeof(SubModule).Assembly);
            }
            catch (Exception e)
            {
                Debug.Print("[ClanTierExpanded] Harmony patch failed: " + e.Message);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter == null)
                return;

            // Always register; ExpandedClanTierModel falls back to vanilla when Enabled is off.
            // ClanTierReconcileBehavior rewrites saved Clan.Tier on load / setting changes.
            campaignStarter.AddModel(new ExpandedClanTierModel());
            campaignStarter.AddBehavior(new ClanTierReconcileBehavior());
        }
    }
}
