using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Localization;

namespace ClanTierExpanded
{
    // Shields the same AI score surfaces as CPLE. Vanilla 1.4.8 reads party limits (WarPartyLimit) here,
    // not companion limits; companion capping in ExpandedClanTierModel is only for third-party readers.
    internal static class VanillaStrengthPatches
    {
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Patch(Harmony harmony)
        {
            if (harmony == null)
                return;

            try
            {
                PatchDiplomacyScores(harmony);
            }
            catch (Exception e)
            {
                SubModule.ReportProblem("Diplomacy strength shield failed: " + e.Message);
            }

            try
            {
                PatchConspiracyClans(harmony);
            }
            catch (Exception e)
            {
                SubModule.ReportProblem("Conspiracy clan packing shield failed: " + e.Message);
            }
        }

        private static void PatchDiplomacyScores(Harmony harmony)
        {
            DiplomacyModel model = Campaign.Current != null ? Campaign.Current.Models.DiplomacyModel : null;
            if (model == null)
            {
                SubModule.ReportProblem("No diplomacy model; AI scores may see expanded party limits.");
                return;
            }

            Type type = model.GetType();
            TextObject reason;
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfClanToJoinKingdom(null, null))),
                "Could not find GetScoreOfClanToJoinKingdom; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfClanToLeaveKingdom(null, null))),
                "Could not find GetScoreOfClanToLeaveKingdom; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfKingdomToGetClan(null, null))),
                "Could not find GetScoreOfKingdomToGetClan; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfKingdomToSackClan(null, null))),
                "Could not find GetScoreOfKingdomToSackClan; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfMercenaryToJoinKingdom(null, null))),
                "Could not find GetScoreOfMercenaryToJoinKingdom; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfMercenaryToLeaveKingdom(null, null))),
                "Could not find GetScoreOfMercenaryToLeaveKingdom; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfKingdomToHireMercenary(null, null))),
                "Could not find GetScoreOfKingdomToHireMercenary; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfDeclaringWar(null, null, null, out reason, false))),
                "Could not find GetScoreOfDeclaringWar; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfDeclaringPeace(null, null))),
                "Could not find GetScoreOfDeclaringPeace; that AI score may see expanded party limits.");
            UseVanillaStrength(
                harmony,
                Override(type, SymbolExtensions.GetMethodInfo((DiplomacyModel m) => m.GetScoreOfDeclaringPeaceForClan(null, null, null, out reason, false))),
                "Could not find GetScoreOfDeclaringPeaceForClan; that AI score may see expanded party limits.");
        }

        private static void PatchConspiracyClans(Harmony harmony)
        {
            // StoryMode is optional; resolve by name so this assembly does not hard-require StoryMode.dll.
            Type behavior = AccessTools.TypeByName("StoryMode.Quests.ThirdPhase.DefeatTheConspiracyQuestBehavior");
            if (behavior == null)
                return;

            MethodInfo init = AccessTools.Method(behavior, "InitializeFinalPhase");
            UseVanillaStrength(
                harmony,
                init,
                "Could not find the story's final phase; conspiracy clan packing may see expanded party limits.");
        }

        private static void UseVanillaStrength(Harmony harmony, MethodInfo target, string missingMessage)
        {
            if (target == null || target.IsAbstract)
            {
                SubModule.ReportProblem(missingMessage);
                return;
            }

            if (!Patched.Add(target))
                return;

            try
            {
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(typeof(VanillaStrengthPatches).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)),
                    finalizer: new HarmonyMethod(typeof(VanillaStrengthPatches).GetMethod("Finalizer", BindingFlags.Static | BindingFlags.NonPublic)));
            }
            catch (Exception e)
            {
                Patched.Remove(target);
                SubModule.ReportProblem("Could not shield " + target.DeclaringType + "." + target.Name + ": " + e.Message);
            }
        }

        private static void Prefix(out bool __state)
        {
            VanillaStrengthScope.Enter();
            __state = true;
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
                VanillaStrengthScope.Exit();
            return __exception;
        }

        private static MethodInfo Override(Type type, MethodInfo baseMethod)
        {
            if (type == null || baseMethod == null)
                return null;

            Type[] argTypes = baseMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            MethodInfo method = type.GetMethod(baseMethod.Name, argTypes);
            if (method == null || method.IsAbstract)
                return null;
            return method;
        }
    }
}
