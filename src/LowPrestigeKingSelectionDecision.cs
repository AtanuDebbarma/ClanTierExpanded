using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ClanTierExpanded
{
    // Prestige challenge: ruling clan is below the configured tier/renown mark.
    // Unlike vanilla KingSelectionKingdomDecision, IsKingsVoteAllowed is true so the
    // ruling clan can spend Influence to keep the crown. Support Influence costs are
    // also raised for this decision only.
    public class LowPrestigeKingSelectionDecision : KingSelectionKingdomDecision
    {
        public LowPrestigeKingSelectionDecision(Clan proposerClan)
            : base(proposerClan, null)
        {
        }

        // Enables the ruling-clan chooser path that spends Influence to override / contest.
        public override bool IsKingsVoteAllowed
        {
            get { return true; }
        }

        public override bool IsAllowed()
        {
            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.LowPrestigeRulerChallenge)
                return false;
            return Campaign.Current.Models.KingdomDecisionPermissionModel.IsKingSelectionDecisionAllowed(base.Kingdom);
        }

        public override int GetProposalInfluenceCost()
        {
            // Auto-triggered; proposal cost is ignored when AddDecision(..., ignoreInfluenceCost: true).
            return 0;
        }

        public override TextObject GetGeneralTitle()
        {
            TextObject text = new TextObject("{=cte_lpks_title}Low prestige: King of {KINGDOM_NAME}");
            text.SetTextVariable("KINGDOM_NAME", base.Kingdom.Name);
            return text;
        }

        public override TextObject GetSupportTitle()
        {
            TextObject text = new TextObject("{=cte_lpks_support}Vote on the ruler of {KINGDOM_NAME} after the ruling clan's low prestige");
            text.SetTextVariable("KINGDOM_NAME", base.Kingdom.Name);
            return text;
        }

        public override TextObject GetChooseTitle()
        {
            TextObject text = new TextObject("{=cte_lpks_choose}Choose the ruler of {KINGDOM_NAME}");
            text.SetTextVariable("KINGDOM_NAME", base.Kingdom.Name);
            return text;
        }

        public override TextObject GetSupportDescription()
        {
            TextObject text = new TextObject("{=cte_lpks_support_desc}The ruling clan of {KINGDOM_NAME} has fallen below the prestige expected of a crown. The council will decide who should rule. The current ruler may contest this, but Influence will be spent.");
            text.SetTextVariable("KINGDOM_NAME", base.Kingdom.Name);
            return text;
        }

        public override TextObject GetChooseDescription()
        {
            return GetChooseTitle();
        }

        // Candidates by Renown (not strength). Always try to keep the ruling clan in the shortlist.
        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            Dictionary<Clan, float> scores = new Dictionary<Clan, float>();
            Clan ruling = base.Kingdom.RulingClan;
            foreach (Clan clan in base.Kingdom.Clans)
            {
                if (!Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan))
                    continue;
                scores[clan] = clan.Renown;
            }

            List<Clan> ordered = scores.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
            List<Clan> picked = new List<Clan>();
            for (int i = 0; i < ordered.Count && picked.Count < 3; i++)
            {
                picked.Add(ordered[i]);
            }

            if (ruling != null
                && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(ruling)
                && !picked.Contains(ruling)
                && picked.Count > 0)
            {
                // Replace the weakest shortlisted clan so the incumbent can still stand.
                picked[picked.Count - 1] = ruling;
            }

            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i].Leader != null)
                    yield return new KingSelectionKingdomDecision.KingSelectionDecisionOutcome(picked[i].Leader);
            }
        }

        public override float CalculateMeritOfOutcome(DecisionOutcome candidateOutcome)
        {
            float total = 1f;
            foreach (Clan clan in base.Kingdom.Clans)
            {
                if (clan.Leader != Hero.MainHero)
                    total += CalculateMeritForClan(clan, candidateOutcome);
            }
            return total;
        }

        public override float DetermineSupport(Clan clan, DecisionOutcome possibleOutcome)
        {
            return CalculateMeritForClan(clan, possibleOutcome) * 10f;
        }

        private float CalculateMeritForClan(Clan voter, DecisionOutcome candidateOutcome)
        {
            float merit = 0f;
            Hero king = ((KingSelectionKingdomDecision.KingSelectionDecisionOutcome)candidateOutcome).King;
            if (king == null || king.Clan == null || voter == null || voter.Leader == null)
                return 0f;

            Clan ruling = base.Kingdom.RulingClan;
            bool candidateIsIncumbent = ruling != null && king.Clan == ruling;

            // Soft loyalty to the crown, then Renown-weighted preference.
            if (candidateIsIncumbent)
            {
                int authoritarian = voter.Leader.GetTraitLevel(DefaultTraits.Authoritarian);
                int oligarchic = voter.Leader.GetTraitLevel(DefaultTraits.Oligarchic);
                if (authoritarian > 0)
                    merit += 2f;
                else if (oligarchic > 0)
                    merit += 1f;
                else
                    merit += 0.5f;
            }

            List<float> renowns = new List<float>();
            foreach (Clan clan in base.Kingdom.Clans)
            {
                if (Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan))
                    renowns.Add(clan.Renown);
            }
            renowns.Sort();
            renowns.Reverse();

            float top = renowns.Count > 0 ? renowns[0] : 1f;
            float bottom = renowns.Count > 0 ? renowns[renowns.Count - 1] : 0f;
            float span = top - bottom;
            if (span < 1f)
                span = 1f;
            float scale = 6f / span;
            float offset = 3f - scale * top;
            merit += king.Clan.Renown * scale + offset;

            if (candidateIsIncumbent)
            {
                Settings settings = Settings.Instance;
                float multiplier = settings != null ? settings.GetIncumbentMeritFraction() : 0.45f;
                merit *= multiplier;
            }

            return MathF.Clamp(merit, -3f, 8f);
        }

        // Higher support Influence cost for this prestige challenge only (vanilla death elections unchanged).
        protected override int GetInfluenceCostOfSupportInternal(Supporter.SupportWeights supportWeight)
        {
            int baseCost = base.GetInfluenceCostOfSupportInternal(supportWeight);
            Settings settings = Settings.Instance;
            float multiplier = settings != null ? settings.GetChallengeSupportInfluenceMultiplier() : 1.5f;
            int cost = (int)Math.Round((double)baseCost * (double)multiplier);
            if (cost < baseCost)
                cost = baseCost;
            return cost;
        }

        public override TextObject GetChosenOutcomeText(DecisionOutcome chosenOutcome, SupportStatus supportStatus, bool isShortVersion = false)
        {
            TextObject text = new TextObject("{=cte_lpks_chosen}After the ruling clan's low prestige, the council of the {KINGDOM} has chosen {KING.NAME} as ruler.");
            text.SetTextVariable("KINGDOM", base.Kingdom.Name);
            StringHelpers.SetCharacterProperties("KING", ((KingSelectionKingdomDecision.KingSelectionDecisionOutcome)chosenOutcome).King.CharacterObject, text);
            return text;
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.OrderByDescending(outcome => outcome.Merit).FirstOrDefault();
        }
    }
}
