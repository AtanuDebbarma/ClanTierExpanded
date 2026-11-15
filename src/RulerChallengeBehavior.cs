using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Starts a kingdom ruler vote when the ruling clan stays below the prestige tier/renown mark.
    internal sealed class RulerChallengeBehavior : CampaignBehaviorBase
    {
        private const string LogPrefix = "[ClanTierExpanded][RulerChallenge] ";

        // kingdomId|firstDayBelow|lastChallengeDay|rulingClanId;...
        private string _challengeState = string.Empty;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            // Clear countdown soon after disable, not only on the next daily tick.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("cte_ruler_challenge_state", ref _challengeState);
            if (_challengeState == null)
                _challengeState = string.Empty;
        }

        private void OnHourlyTick()
        {
            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.LowPrestigeRulerChallenge)
                ResetAllFirstDayBelow();
        }

        private void OnDailyTick()
        {
            Settings settings = Settings.Instance;
            if (settings == null || !settings.Enabled || !settings.LowPrestigeRulerChallenge)
            {
                // Disabled time must not count as consecutive days below threshold.
                ResetAllFirstDayBelow();
                return;
            }
            if (Campaign.Current == null)
                return;

            int today = CurrentDay();
            Dictionary<string, KingdomState> byId = ParseStates(_challengeState);

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated)
                    continue;

                string kingdomId = kingdom.StringId != null ? kingdom.StringId : kingdom.GetHashCode().ToString();
                string rulingId = kingdom.RulingClan != null && kingdom.RulingClan.StringId != null
                    ? kingdom.RulingClan.StringId
                    : string.Empty;

                KingdomState state;
                if (!byId.TryGetValue(kingdomId, out state) || state == null)
                {
                    state = new KingdomState();
                    state.KingdomId = kingdomId;
                    state.FirstDayBelow = -1;
                    state.LastChallengeDay = -99999;
                    state.RulingClanId = rulingId;
                    byId[kingdomId] = state;
                }

                // Countdown belongs to the current ruling clan; kingdom-wide cooldown stays.
                if (!string.Equals(state.RulingClanId, rulingId, StringComparison.Ordinal))
                {
                    state.RulingClanId = rulingId;
                    state.FirstDayBelow = -1;
                }

                if (!IsRulingClanBelowPrestigeMark(kingdom, settings))
                {
                    state.FirstDayBelow = -1;
                    state.RulingClanId = rulingId;
                    byId[kingdomId] = state;
                    continue;
                }

                if (state.FirstDayBelow < 0)
                    state.FirstDayBelow = today;

                state.RulingClanId = rulingId;
                byId[kingdomId] = state;

                int daysBelow = today - state.FirstDayBelow + 1;
                int requiredDays = settings.RulerChallengeDaysBelowThreshold;
                if (requiredDays < 1)
                    requiredDays = 1;

                if (daysBelow < requiredDays)
                    continue;

                int cooldown = settings.RulerChallengeCooldownDays;
                if (cooldown < 1)
                    cooldown = 1;
                if (today - state.LastChallengeDay < cooldown)
                    continue;

                if (HasOpenRulerDecision(kingdom))
                    continue;

                if (TryStartChallenge(kingdom, settings))
                {
                    state.LastChallengeDay = today;
                    state.FirstDayBelow = -1;
                    state.RulingClanId = rulingId;
                    byId[kingdomId] = state;
                    Debug.Print(LogPrefix + "Started low-prestige ruler vote in " + kingdom.Name);
                }
            }

            _challengeState = SerializeStates(byId.Values);
        }

        private void ResetAllFirstDayBelow()
        {
            Dictionary<string, KingdomState> byId = ParseStates(_challengeState);
            bool changed = false;
            foreach (KeyValuePair<string, KingdomState> pair in byId)
            {
                KingdomState state = pair.Value;
                if (state == null)
                    continue;
                if (state.FirstDayBelow < 0)
                    continue;
                state.FirstDayBelow = -1;
                changed = true;
            }

            if (changed)
                _challengeState = SerializeStates(byId.Values);
        }

        private static bool IsRulingClanBelowPrestigeMark(Kingdom kingdom, Settings settings)
        {
            Clan ruling = kingdom.RulingClan;
            if (ruling == null || ruling.IsEliminated || ruling.IsBanditFaction)
                return false;
            if (ruling.Leader == null || !ruling.Leader.IsAlive)
                return false;

            int thresholdTier = settings.RulerChallengeTierThreshold;
            if (thresholdTier < 1)
                thresholdTier = 1;
            if (ruling.Tier >= thresholdTier)
                return false;

            if (Campaign.Current.Models == null || Campaign.Current.Models.ClanTierModel == null)
                return false;

            int renownMark = Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(thresholdTier);
            if (ruling.Renown >= (float)renownMark)
                return false;

            return true;
        }

        private static bool HasOpenRulerDecision(Kingdom kingdom)
        {
            MBReadOnlyList<KingdomDecision> unresolved = kingdom.UnresolvedDecisions;
            if (unresolved == null)
                return false;

            for (int i = 0; i < unresolved.Count; i++)
            {
                KingdomDecision decision = unresolved[i];
                if (decision is KingSelectionKingdomDecision)
                    return true;
            }

            return false;
        }

        private static bool TryStartChallenge(Kingdom kingdom, Settings settings)
        {
            Clan ruling = kingdom.RulingClan;
            Clan proposer = FindHighestRenownChallenger(kingdom, ruling);
            if (proposer == null)
                return false;

            int eligible = CountEligibleClans(kingdom);
            if (eligible < 2)
                return false;

            try
            {
                LowPrestigeKingSelectionDecision decision = new LowPrestigeKingSelectionDecision(proposer);
                kingdom.AddDecision(decision, ignoreInfluenceCost: true);

                if (settings.NotifyPlayerRenownLoss && Clan.PlayerClan != null && Clan.PlayerClan.Kingdom == kingdom)
                {
                    string message = "A low-prestige challenge for the throne of "
                        + kingdom.Name
                        + " has been raised by "
                        + proposer.Name
                        + ". The current ruler may contest it, but Influence will be spent.";
                    InformationManager.DisplayMessage(new InformationMessage(message));
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.Print(LogPrefix + "Failed to start challenge: " + e.Message);
                return false;
            }
        }

        private static Clan FindHighestRenownChallenger(Kingdom kingdom, Clan ruling)
        {
            Clan best = null;
            float bestRenown = float.MinValue;
            foreach (Clan clan in kingdom.Clans)
            {
                if (clan == null || clan == ruling)
                    continue;
                if (!Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan))
                    continue;
                if (clan.Renown > bestRenown)
                {
                    bestRenown = clan.Renown;
                    best = clan;
                }
            }

            return best;
        }

        private static int CountEligibleClans(Kingdom kingdom)
        {
            int count = 0;
            foreach (Clan clan in kingdom.Clans)
            {
                if (Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan))
                    count++;
            }
            return count;
        }

        private static int CurrentDay()
        {
            if (Campaign.Current == null)
                return 0;
            return (int)CampaignTime.Now.ToDays;
        }

        private sealed class KingdomState
        {
            public string KingdomId;
            public int FirstDayBelow;
            public int LastChallengeDay;
            public string RulingClanId;
        }

        private static Dictionary<string, KingdomState> ParseStates(string raw)
        {
            Dictionary<string, KingdomState> map = new Dictionary<string, KingdomState>();
            if (string.IsNullOrEmpty(raw))
                return map;

            string[] parts = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] fields = parts[i].Split(new char[] { '|' });
                if (fields.Length < 3)
                    continue;
                int first;
                int last;
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out first))
                    continue;
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out last))
                    continue;
                KingdomState state = new KingdomState();
                state.KingdomId = fields[0];
                state.FirstDayBelow = first;
                state.LastChallengeDay = last;
                state.RulingClanId = fields.Length >= 4 ? fields[3] : string.Empty;
                map[state.KingdomId] = state;
            }

            return map;
        }

        private static string SerializeStates(IEnumerable<KingdomState> states)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (KingdomState state in states)
            {
                if (state == null || state.KingdomId == null)
                    continue;
                if (state.FirstDayBelow < 0 && state.LastChallengeDay < -90000)
                    continue;
                if (!first)
                    sb.Append(';');
                first = false;
                string ruling = state.RulingClanId != null ? state.RulingClanId : string.Empty;
                sb.Append(Sanitize(state.KingdomId)).Append('|')
                    .Append(state.FirstDayBelow.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(state.LastChallengeDay.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(Sanitize(ruling));
            }
            return sb.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("|", "_").Replace(";", "_");
        }
    }
}
