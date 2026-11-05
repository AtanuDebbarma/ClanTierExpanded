using System;
using System.Collections.Generic;
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

        private const string LogPrefix = "[ClanTierExpanded] ";

        private static readonly List<string> PendingProblems = new List<string>();

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
                Debug.Print(LogPrefix + "Harmony patch failed: " + e.Message);
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

        // Diplomacy / StoryMode targets are final here, including replacements from other mods.
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            if (!(game.GameType is Campaign))
                return;

            try
            {
                VanillaStrengthPatches.Patch(_harmony);
            }
            catch (Exception e)
            {
                ReportProblem("AI strength shield failed: " + e.Message);
            }

            ShowPendingProblems();
        }

        internal static void ReportProblem(string message)
        {
            Debug.Print(LogPrefix + message);
            PendingProblems.Add(message);
        }

        private static void ShowPendingProblems()
        {
            foreach (string message in PendingProblems)
            {
                InformationManager.DisplayMessage(new InformationMessage(LogPrefix + message, Colors.Red));
            }

            PendingProblems.Clear();
        }
    }
}
