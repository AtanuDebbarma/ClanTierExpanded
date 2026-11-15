using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Postfix concrete model overrides. Only the active Campaign.Current.Models.* instance
    // applies the bonus so NavalDLC wrappers that call BaseModel do not double-stack.
    internal static class DynastyBonusPatches
    {
        private static IEnumerable<MethodBase> Overrides(Type modelBase, string methodName, Type[] parameters)
        {
            foreach (Type type in AccessTools.AllTypes())
            {
                if (type == null || type.IsAbstract || !modelBase.IsAssignableFrom(type))
                    continue;

                MethodInfo method = AccessTools.Method(type, methodName, parameters);
                if (method == null || method.IsAbstract || method.DeclaringType != type)
                    continue;
                yield return method;
            }
        }

        private static bool Active(object instance, object activeModel)
        {
            return DynastyBonusHelper.IsActiveCampaignModel(instance, activeModel);
        }

        private static void AddPositiveFactor(ref ExplainedNumber result, float scaled, float milestone, TaleWorlds.Localization.TextObject scaledText)
        {
            if (scaled != 0f)
                result.AddFactor(scaled, scaledText);
            if (milestone != 0f)
                result.AddFactor(milestone, DynastyBonusHelper.GetMilestoneText());
        }

        private static void AddNegativeFactor(ref ExplainedNumber result, float scaled, float milestone, TaleWorlds.Localization.TextObject scaledText)
        {
            if (scaled != 0f)
                result.AddFactor(-scaled, scaledText);
            if (milestone != 0f)
                result.AddFactor(-milestone, DynastyBonusHelper.GetMilestoneText());
        }

        [HarmonyPatch]
        private static class PartySpeedFinalPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(PartySpeedModel), "CalculateFinalSpeed", new Type[] { typeof(MobileParty), typeof(ExplainedNumber) });
            }

            static void Postfix(object __instance, MobileParty mobileParty, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.PartySpeedCalculatingModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetSpeedFactors(mobileParty, out scaled, out milestone);
                AddPositiveFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetLogisticsText());
            }
        }

        [HarmonyPatch]
        private static class PartyWagePatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(PartyWageModel), "GetTotalWage", new Type[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) });
            }

            static void Postfix(object __instance, MobileParty mobileParty, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.PartyWageModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetWageDiscountFactors(mobileParty, out scaled, out milestone);
                AddNegativeFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetLogisticsText());
            }
        }

        [HarmonyPatch]
        private static class FoodConsumptionPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(MobilePartyFoodConsumptionModel), "CalculateDailyFoodConsumptionf", new Type[] { typeof(MobileParty), typeof(ExplainedNumber) });
            }

            static void Postfix(object __instance, MobileParty party, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.MobilePartyFoodConsumptionModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetFoodSavingFactors(party, out scaled, out milestone);
                AddNegativeFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetLogisticsText());
            }
        }

        [HarmonyPatch]
        private static class HealingRegularsPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(PartyHealingModel), "GetDailyHealingForRegulars", new Type[] { typeof(PartyBase), typeof(bool), typeof(bool) });
            }

            // Positional args: vanilla/War Sails rename party/isPrisoner(s). Skip prisoner pass (__1).
            static void Postfix(object __instance, PartyBase __0, bool __1, ref ExplainedNumber __result)
            {
                if (__1)
                    return;
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.PartyHealingModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetHealingFactors(__0, out scaled, out milestone);
                AddPositiveFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetLogisticsText());
            }
        }

        [HarmonyPatch]
        private static class HealingHeroesPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(PartyHealingModel), "GetDailyHealingHpForHeroes", new Type[] { typeof(PartyBase), typeof(bool), typeof(bool) });
            }

            // Positional args: same rename hazard as regulars. Skip imprisoned-hero pass (__1).
            static void Postfix(object __instance, PartyBase __0, bool __1, ref ExplainedNumber __result)
            {
                if (__1)
                    return;
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.PartyHealingModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetHealingFactors(__0, out scaled, out milestone);
                AddPositiveFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetLogisticsText());
            }
        }

        [HarmonyPatch]
        private static class InfluencePatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(ClanPoliticsModel), "CalculateInfluenceChange", new Type[] { typeof(Clan), typeof(bool) });
            }

            static void Postfix(object __instance, Clan clan, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.ClanPoliticsModel))
                    return;
                // Income bonus only — never amplify daily Influence drains (e.g. mercenary clans).
                if (__result.ResultNumber <= 0f)
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetInfluenceFactors(clan, out scaled, out milestone);
                AddPositiveFactor(ref __result, scaled, milestone, DynastyBonusHelper.GetCourtText());
            }
        }

        [HarmonyPatch]
        private static class RelationPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(DiplomacyModel), "GetEffectiveRelationChange", new Type[] { typeof(Hero), typeof(Hero), typeof(int) });
            }

            static void Postfix(object __instance, Hero originalHero, Hero originalGainedRelationWith, int relationChange, ref int __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.DiplomacyModel))
                    return;
                if (__result <= 0 || relationChange <= 0)
                    return;

                float boost = 0f;
                if (originalHero != null)
                    boost = Math.Max(boost, DynastyBonusHelper.GetRelationBoostFactor(originalHero.Clan));
                if (originalGainedRelationWith != null)
                    boost = Math.Max(boost, DynastyBonusHelper.GetRelationBoostFactor(originalGainedRelationWith.Clan));
                if (boost <= 0f)
                    return;

                // Fractional % as whole + probabilistic remainder (no forced min +1).
                int extra = DynastyBonusHelper.RollRelationBonus(__result, boost);
                if (extra <= 0)
                    return;
                __result += extra;
            }
        }

        [HarmonyPatch]
        private static class LoyaltyPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(SettlementLoyaltyModel), "CalculateLoyaltyChange", new Type[] { typeof(Town), typeof(bool) });
            }

            static void Postfix(object __instance, Town town, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.SettlementLoyaltyModel))
                    return;
                if (town == null || town.Settlement == null || !town.Settlement.IsTown)
                    return;
                Clan owner = DynastyBonusHelper.OwnerClanOfSettlement(town.Settlement);
                float scaled;
                float milestone;
                DynastyBonusHelper.GetLoyaltyBonuses(owner, out scaled, out milestone);
                if (scaled != 0f)
                    __result.Add(scaled, DynastyBonusHelper.GetReputationText());
                if (milestone != 0f)
                    __result.Add(milestone, DynastyBonusHelper.GetMilestoneText());
            }
        }

        // Priority.Last so other volunteer multipliers (e.g. CMT castle recruit %) apply first; one final clamp.
        [HarmonyPatch]
        private static class VolunteerPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(VolunteerModel), "GetDailyVolunteerProductionProbability", new Type[] { typeof(Hero), typeof(int), typeof(Settlement) });
            }

            [HarmonyPriority(Priority.Last)]
            static void Postfix(object __instance, Settlement settlement, ref float __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.VolunteerModel))
                    return;
                float factor = DynastyBonusHelper.GetVolunteerFactor(settlement);
                if (factor == 0f)
                    return;
                __result *= 1f + factor;
                if (__result > 1f)
                    __result = 1f;
            }
        }

        [HarmonyPatch]
        private static class MilitiaPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(SettlementMilitiaModel), "CalculateMilitiaChange", new Type[] { typeof(Settlement), typeof(bool) });
            }

            static void Postfix(object __instance, Settlement settlement, ref ExplainedNumber __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.SettlementMilitiaModel))
                    return;
                // Growth bonus only — never speed up militia decline.
                if (__result.ResultNumber <= 0f)
                    return;
                float factor = DynastyBonusHelper.GetMilitiaFactor(settlement);
                if (factor == 0f)
                    return;
                __result.AddFactor(factor, DynastyBonusHelper.GetReputationText());
            }
        }

        [HarmonyPatch]
        private static class CaravanIncomePatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(ClanFinanceModel), "CalculateOwnerIncomeFromCaravan", new Type[] { typeof(MobileParty) });
            }

            static void Postfix(object __instance, MobileParty caravan, ref int __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.ClanFinanceModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetCaravanIncomeFactors(caravan, out scaled, out milestone);
                __result = DynastyBonusHelper.ScaleIncome(__result, scaled, milestone);
            }
        }

        [HarmonyPatch]
        private static class WorkshopIncomePatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return Overrides(typeof(ClanFinanceModel), "CalculateOwnerIncomeFromWorkshop", new Type[] { typeof(Workshop) });
            }

            static void Postfix(object __instance, Workshop workshop, ref int __result)
            {
                if (Campaign.Current == null || !Active(__instance, Campaign.Current.Models.ClanFinanceModel))
                    return;
                float scaled;
                float milestone;
                DynastyBonusHelper.GetWorkshopIncomeFactors(workshop, out scaled, out milestone);
                __result = DynastyBonusHelper.ScaleIncome(__result, scaled, milestone);
            }
        }
    }
}
