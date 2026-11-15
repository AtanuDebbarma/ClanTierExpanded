using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Keeps saved Clan.Tier in sync with CalculateTier when settings change or CTE is toggled.
    // Vanilla AddRenown only raises tier and never lowers it.
    internal sealed class ClanTierReconcileBehavior : CampaignBehaviorBase
    {
        private static readonly MethodInfo TierSetter = AccessTools.PropertySetter(typeof(Clan), "Tier");
        private static readonly FieldInfo TierField = AccessTools.Field(typeof(Clan), "_tier");
        private static readonly MethodInfo OnClanTierChangedMethod = AccessTools.Method(
            typeof(CampaignEventDispatcher), "OnClanTierChanged", new Type[] { typeof(Clan), typeof(bool) });

        private string _settingsFingerprint;
        private bool _reconcileQueued;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _settingsFingerprint = BuildFingerprint(Settings.Instance);
            _reconcileQueued = true;
        }

        private void OnAfterSessionLaunched(CampaignGameStarter starter)
        {
            TryReconcile("session");
        }

        private void OnHourlyTick()
        {
            CheckSettingsChanged();
            TryReconcile("hourly");
        }

        private void OnDailyTick()
        {
            CheckSettingsChanged();
            TryReconcile("daily");
        }

        private void CheckSettingsChanged()
        {
            string fingerprint = BuildFingerprint(Settings.Instance);
            if (fingerprint == _settingsFingerprint)
                return;

            _settingsFingerprint = fingerprint;
            _reconcileQueued = true;
        }

        private void TryReconcile(string reason)
        {
            if (!_reconcileQueued || Campaign.Current == null)
                return;

            _reconcileQueued = false;
            ReconcileAllClans(reason);
        }

        internal static void ReconcileAllClans(string reason)
        {
            if (Campaign.Current == null || Campaign.Current.Models == null || Campaign.Current.Models.ClanTierModel == null)
                return;

            int changed = 0;
            foreach (Clan clan in Clan.All)
            {
                if (clan == null)
                    continue;

                // Include eliminated clans so disable/remove leaves no leftover high tiers.
                int calculated = Campaign.Current.Models.ClanTierModel.CalculateTier(clan);
                if (calculated == clan.Tier)
                    continue;

                if (!TrySetTier(clan, calculated))
                    continue;

                changed++;
                if (!clan.IsEliminated)
                    TryNotifyTierChanged(clan, false);
            }

            if (changed > 0)
                Debug.Print("[ClanTierExpanded] Reconciled clan tiers (" + reason + "): " + changed + " clan(s) updated.");
        }

        // Sets Clan.Renown then recalculates Tier immediately (does not wait for settings reconcile).
        // Vanilla Clan.AddRenown ignores negative values, so renown is assigned directly.
        internal static bool ApplyRenownAndRecalculateTier(Clan clan, float newRenown, out int oldTier, out int newTier)
        {
            oldTier = 0;
            newTier = 0;
            if (clan == null || Campaign.Current == null || Campaign.Current.Models == null || Campaign.Current.Models.ClanTierModel == null)
                return false;

            if (newRenown < 0f)
                newRenown = 0f;
            if (float.IsNaN(newRenown) || float.IsInfinity(newRenown))
                newRenown = 0f;

            oldTier = clan.Tier;
            clan.Renown = newRenown;
            newTier = Campaign.Current.Models.ClanTierModel.CalculateTier(clan);
            if (newTier == oldTier)
                return true;

            if (!TrySetTier(clan, newTier))
            {
                newTier = clan.Tier;
                return false;
            }

            TryNotifyTierChanged(clan, false);
            return true;
        }

        internal static bool TrySetTier(Clan clan, int tier)
        {
            try
            {
                int min = Campaign.Current.Models.ClanTierModel.MinClanTier;
                int max = Campaign.Current.Models.ClanTierModel.MaxClanTier;
                if (tier > max)
                    tier = max;
                if (tier < min)
                    tier = min;

                if (TierSetter != null)
                {
                    TierSetter.Invoke(clan, new object[] { tier });
                }
                else if (TierField != null)
                {
                    TierField.SetValue(clan, tier);
                }
                else
                {
                    return false;
                }

                return clan.Tier == tier;
            }
            catch (Exception e)
            {
                Debug.Print("[ClanTierExpanded] Could not set Clan.Tier: " + e.Message);
            }

            return false;
        }

        internal static void TryNotifyTierChanged(Clan clan, bool shouldNotify)
        {
            try
            {
                if (OnClanTierChangedMethod == null || CampaignEventDispatcher.Instance == null)
                    return;

                OnClanTierChangedMethod.Invoke(CampaignEventDispatcher.Instance, new object[] { clan, shouldNotify });
            }
            catch
            {
            }
        }

        private static string BuildFingerprint(Settings settings)
        {
            if (settings == null)
                return "null";

            // Anything that changes CalculateTier / MaxClanTier must be included.
            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            sb.Append(settings.Enabled ? '1' : '0');
            sb.Append('|').Append(settings.GetConfiguredMaxTier());
            sb.Append('|').Append(settings.IsManualRenown() ? 'M' : 'F');
            sb.Append('|').Append(settings.RenownMultiplier.ToString("R"));
            sb.Append('|').Append(settings.GrowthPerTier.ToString("R"));
            int[] limits = settings.GetRenownLimits();
            if (limits != null)
            {
                for (int i = 0; i < limits.Length; i++)
                {
                    sb.Append('|').Append(limits[i]);
                }
            }

            return sb.ToString();
        }
    }
}
