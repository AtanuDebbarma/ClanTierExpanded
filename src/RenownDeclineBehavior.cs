using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Optional renown decline after military defeats and lost fortifications.
    // No custom save types: ledger state is a primitive string.
    internal sealed class RenownDeclineBehavior : CampaignBehaviorBase
    {
        private const string LogPrefix = "[ClanTierExpanded][RenownDecline] ";
        private const int LedgerRetentionDays = 2;

        // clanId|contextId|day|renownBefore|lossApplied|tierBefore;...
        private string _lossLedger = string.Empty;

        // eventKey|defenderStrength|attackerStrength;... captured on MapEventStarted (survives save/load).
        private string _initialStrengthCache = string.Empty;

        // alias|primaryKey;... party:/id: aliases so leader-change + reload still finds the start snapshot.
        private string _primaryKeyAliasCache = string.Empty;

        // Serialized pending battle losses (tiny MapEventEnded->destroy window across autosave).
        private string _pendingBattleCache = string.Empty;

        // Exact map-event key -> hero StringIds captured during that MapEvent (HeroPrisonerTaken). Session-only.
        private readonly Dictionary<string, HashSet<string>> _capturesByMapEvent = new Dictionary<string, HashSet<string>>();

        // Session identity: MapEvent instance -> primary stable key created at start (survives leader changes).
        private readonly Dictionary<MapEvent, string> _primaryKeyByMapEvent = new Dictionary<MapEvent, string>();

        // MapEventEnded runs before vanilla destroys defeated parties; flush after MobilePartyDestroyed + ticks.
        private readonly List<PendingBattleLoss> _pendingBattles = new List<PendingBattleLoss>();

        // Campaign objects are not reliable inside SyncData; reconcile on the first tick after load.
        private bool _reconcileCachesAfterLoad;

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnPartyAddedToMapEventEvent.AddNonSerializedListener(this, OnPartyAddedToMapEvent);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            // BeforeHeroKilled: leadership / elimination have not run yet, so Leader and survival are still reliable.
            CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(this, OnBeforeHeroKilled);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
                _pendingBattleCache = SerializePendingBattles(_pendingBattles);

            dataStore.SyncData("cte_renown_loss_ledger", ref _lossLedger);
            dataStore.SyncData("cte_map_event_initial_strength", ref _initialStrengthCache);
            dataStore.SyncData("cte_map_event_primary_aliases", ref _primaryKeyAliasCache);
            dataStore.SyncData("cte_pending_battle_loss", ref _pendingBattleCache);

            if (_lossLedger == null)
                _lossLedger = string.Empty;
            if (_initialStrengthCache == null)
                _initialStrengthCache = string.Empty;
            if (_primaryKeyAliasCache == null)
                _primaryKeyAliasCache = string.Empty;
            if (_pendingBattleCache == null)
                _pendingBattleCache = string.Empty;

            if (dataStore.IsLoading)
            {
                _primaryKeyByMapEvent.Clear();
                _capturesByMapEvent.Clear();
                _pendingBattles.Clear();
                DeserializePendingBattles(_pendingBattleCache, _pendingBattles);
                _reconcileCachesAfterLoad = true;
            }
        }

        private void OnDailyTick()
        {
            PruneLedger();
            ReconcileMapEventCaches();
            if (_pendingBattles.Count > 32)
            {
                for (int i = 0; i < _pendingBattles.Count; i++)
                    FlushPendingBattle(_pendingBattles[i]);
                _pendingBattles.Clear();
                ReconcileMapEventCaches();
            }
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null || mapEvent.StrengthOfSide == null || mapEvent.StrengthOfSide.Length < 2)
                return;

            string key = BuildStableMapEventKey(mapEvent, attackerParty, defenderParty);
            BindMapEventPrimaryKey(mapEvent, key);

            float defenderStrength = mapEvent.StrengthOfSide[0];
            float attackerStrength = mapEvent.StrengthOfSide[1];
            PutStrengthSnapshot(key, defenderStrength, attackerStrength);

            // Exact alias by StringId when present (never a bare time prefix).
            if (!string.IsNullOrEmpty(mapEvent.StringId))
            {
                string idKey = "id:" + mapEvent.StringId;
                if (idKey != key)
                    PutStrengthSnapshot(idKey, defenderStrength, attackerStrength);
            }
        }

        // Reinforcements join after start; refresh the cached odds snapshot to include them.
        private void OnPartyAddedToMapEvent(PartyBase party)
        {
            if (party == null || party.MapEvent == null)
                return;

            MapEvent mapEvent = party.MapEvent;
            if (mapEvent.StrengthOfSide == null || mapEvent.StrengthOfSide.Length < 2)
                return;

            float defenderStrength = mapEvent.StrengthOfSide[0];
            float attackerStrength = mapEvent.StrengthOfSide[1];
            string[] keys = GetExactLookupKeysForMapEvent(mapEvent);
            bool updated = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (HasStrengthSnapshot(keys[i]))
                {
                    PutStrengthSnapshot(keys[i], defenderStrength, attackerStrength);
                    updated = true;
                }
            }

            if (!updated)
            {
                string key = BuildStableMapEventKey(mapEvent, null, null);
                BindMapEventPrimaryKey(mapEvent, key);
                PutStrengthSnapshot(key, defenderStrength, attackerStrength);
            }
            else
            {
                string primary = ResolvePrimaryKey(mapEvent);
                if (!string.IsNullOrEmpty(primary))
                {
                    BindPartyAlias(party, primary);
                    // Keep party aliases current for every participant after reinforcements.
                    BindPartyAliasesForMapEvent(mapEvent, primary);
                }
            }
        }

        private void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
        {
            if (mobileParty == null || _pendingBattles.Count == 0)
                return;

            // Only stable MobileParty.StringId identities participate in destruction tracking.
            string partyId = mobileParty.StringId;
            if (string.IsNullOrEmpty(partyId))
                return;

            for (int i = 0; i < _pendingBattles.Count; i++)
            {
                PendingBattleLoss pending = _pendingBattles[i];
                if (pending == null || pending.TrackedDestroyablePartyIds == null)
                    continue;
                if (!pending.TrackedDestroyablePartyIds.Contains(partyId))
                    continue;
                if (pending.DestroyedPartyIds == null)
                    pending.DestroyedPartyIds = new HashSet<string>();
                pending.DestroyedPartyIds.Add(partyId);
            }
        }

        private void OnCampaignTick(float dt)
        {
            if (_reconcileCachesAfterLoad)
            {
                _reconcileCachesAfterLoad = false;
                ReconcileMapEventCaches();
            }

            if (_pendingBattles.Count == 0)
                return;

            bool flushed = false;
            for (int i = _pendingBattles.Count - 1; i >= 0; i--)
            {
                PendingBattleLoss pending = _pendingBattles[i];
                if (pending == null)
                {
                    _pendingBattles.RemoveAt(i);
                    continue;
                }

                pending.TicksRemaining--;
                if (pending.TicksRemaining > 0)
                    continue;

                FlushPendingBattle(pending);
                _pendingBattles.RemoveAt(i);
                flushed = true;
            }

            if (flushed)
                ReconcileMapEventCaches();
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner == null || prisoner.StringId == null)
                return;
            if (!prisoner.IsLord || prisoner.IsChild)
                return;

            MapEvent mapEvent = null;
            if (capturer != null)
                mapEvent = capturer.MapEvent;
            if (mapEvent == null && prisoner.PartyBelongedTo != null)
                mapEvent = prisoner.PartyBelongedTo.MapEvent;
            if (mapEvent == null)
                return;

            string[] keys = GetExactLookupKeysForMapEvent(mapEvent);
            for (int k = 0; k < keys.Length; k++)
            {
                HashSet<string> set;
                if (!_capturesByMapEvent.TryGetValue(keys[k], out set))
                {
                    set = new HashSet<string>();
                    _capturesByMapEvent[keys[k]] = set;
                }
                set.Add(prisoner.StringId);
            }
        }

        // Prefer StringId; otherwise BattleStartTime + attacker/defender party indices.
        // Always include ":a:" / ":d:" so time alone never matches another battle.
        private static string BuildStableMapEventKey(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent != null && !string.IsNullOrEmpty(mapEvent.StringId))
                return "id:" + mapEvent.StringId;

            if (attackerParty == null && mapEvent != null && mapEvent.AttackerSide != null)
                attackerParty = mapEvent.AttackerSide.LeaderParty;
            if (defenderParty == null && mapEvent != null && mapEvent.DefenderSide != null)
                defenderParty = mapEvent.DefenderSide.LeaderParty;

            double startDays = 0.0;
            if (mapEvent != null)
                startDays = mapEvent.BattleStartTime.ToDays;

            int attackerIndex = attackerParty != null ? attackerParty.Index : -1;
            int defenderIndex = defenderParty != null ? defenderParty.Index : -1;
            return "t:" + startDays.ToString("R", CultureInfo.InvariantCulture)
                + ":a:" + attackerIndex.ToString(CultureInfo.InvariantCulture)
                + ":d:" + defenderIndex.ToString(CultureInfo.InvariantCulture);
        }

        // Exact keys only - never a bare time prefix (avoids simultaneous-battle collisions).
        private string[] GetExactLookupKeysForMapEvent(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return new string[0];

            List<string> keys = new List<string>();
            string primary = ResolvePrimaryKey(mapEvent);
            if (!string.IsNullOrEmpty(primary))
                keys.Add(primary);

            if (!string.IsNullOrEmpty(mapEvent.StringId))
            {
                string idKey = "id:" + mapEvent.StringId;
                if (!keys.Contains(idKey))
                    keys.Add(idKey);
            }

            string rebuilt = BuildStableMapEventKey(mapEvent, null, null);
            if (!keys.Contains(rebuilt))
                keys.Add(rebuilt);

            return keys.ToArray();
        }

        private string ResolvePrimaryKey(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return null;

            string primary;
            if (_primaryKeyByMapEvent.TryGetValue(mapEvent, out primary) && !string.IsNullOrEmpty(primary))
                return primary;

            if (!string.IsNullOrEmpty(mapEvent.StringId))
            {
                string fromId;
                if (TryGetPrimaryAlias("id:" + mapEvent.StringId, out fromId))
                    return fromId;
            }

            string fromParty = ResolvePrimaryKeyFromParties(mapEvent);
            if (!string.IsNullOrEmpty(fromParty))
                return fromParty;

            return null;
        }

        private string ResolvePrimaryKeyFromParties(MapEvent mapEvent)
        {
            string found = ResolvePrimaryKeyFromSide(mapEvent.AttackerSide);
            if (!string.IsNullOrEmpty(found))
                return found;
            return ResolvePrimaryKeyFromSide(mapEvent.DefenderSide);
        }

        private string ResolvePrimaryKeyFromSide(MapEventSide side)
        {
            if (side == null || side.Parties == null)
                return null;

            MBReadOnlyList<MapEventParty> parties = side.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                MapEventParty mapParty = parties[i];
                if (mapParty == null || mapParty.Party == null)
                    continue;
                string trackId = GetStablePartyAliasId(mapParty.Party);
                if (string.IsNullOrEmpty(trackId))
                    continue;
                string primary;
                if (TryGetPrimaryAlias("party:" + trackId, out primary))
                    return primary;
            }
            return null;
        }

        private void BindMapEventPrimaryKey(MapEvent mapEvent, string primaryKey)
        {
            if (mapEvent == null || string.IsNullOrEmpty(primaryKey))
                return;

            _primaryKeyByMapEvent[mapEvent] = primaryKey;
            if (!string.IsNullOrEmpty(mapEvent.StringId))
                PutPrimaryAlias("id:" + mapEvent.StringId, primaryKey);
            BindPartyAliasesForMapEvent(mapEvent, primaryKey);
        }

        private void BindPartyAliasesForMapEvent(MapEvent mapEvent, string primaryKey)
        {
            if (mapEvent == null || string.IsNullOrEmpty(primaryKey))
                return;
            BindPartyAliasesForSide(mapEvent.AttackerSide, primaryKey);
            BindPartyAliasesForSide(mapEvent.DefenderSide, primaryKey);
        }

        private void BindPartyAliasesForSide(MapEventSide side, string primaryKey)
        {
            if (side == null || side.Parties == null)
                return;

            MBReadOnlyList<MapEventParty> parties = side.Parties;
            for (int i = 0; i < parties.Count; i++)
            {
                MapEventParty mapParty = parties[i];
                if (mapParty == null)
                    continue;
                BindPartyAlias(mapParty.Party, primaryKey);
            }
        }

        private void BindPartyAlias(PartyBase party, string primaryKey)
        {
            string trackId = GetStablePartyAliasId(party);
            if (string.IsNullOrEmpty(trackId) || string.IsNullOrEmpty(primaryKey))
                return;
            PutPrimaryAlias("party:" + trackId, primaryKey);
        }

        // Stable cross-load identity for aliases only. Never uses PartyBase.Index.
        private static string GetStablePartyAliasId(PartyBase party)
        {
            if (party == null)
                return null;
            if (party.IsMobile)
            {
                if (party.MobileParty != null && !string.IsNullOrEmpty(party.MobileParty.StringId))
                    return party.MobileParty.StringId;
                return null;
            }
            if (!string.IsNullOrEmpty(party.Id))
                return party.Id;
            return null;
        }

        // Only destroyable mobile parties with a stable StringId participate in destruction tracking.
        private static string GetDestroyableMobileTrackId(PartyBase party)
        {
            if (party == null || !party.IsMobile || party.MobileParty == null)
                return null;
            if (string.IsNullOrEmpty(party.MobileParty.StringId))
                return null;
            return party.MobileParty.StringId;
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            bool queued = false;
            try
            {
                Settings settings = Settings.Instance;
                if (!RenownLossHelper.FeatureEnabled(settings))
                    return;
                if (mapEvent == null || !RenownLossHelper.IsEligibleBattleType(mapEvent))
                    return;
                if (!mapEvent.HasWinner)
                    return;
                // Diplomatic/peaceful ends without a clear military winner are skipped via HasWinner.

                BattleSideEnum defeatedSide = mapEvent.DefeatedSide;
                if (defeatedSide != BattleSideEnum.Attacker && defeatedSide != BattleSideEnum.Defender)
                    return;

                MapEventSide defeated = mapEvent.GetMapEventSide(defeatedSide);
                MapEventSide winner = mapEvent.GetMapEventSide(mapEvent.WinningSide);
                if (defeated == null || winner == null || defeated.Parties == null)
                    return;

                float defenderStrength;
                float attackerStrength;
                ResolveInitialStrengths(mapEvent, out defenderStrength, out attackerStrength);

                float ownStrength = defeatedSide == BattleSideEnum.Defender ? defenderStrength : attackerStrength;
                float enemyStrength = defeatedSide == BattleSideEnum.Defender ? attackerStrength : defenderStrength;

                // Denominator includes every eligible noble clan on the defeated side, even if that
                // clan's MCM group scale is 0 (disabled). Only affected clans receive loss.
                // Settlement parties still contribute troops/casualties; only mobile StringIds track destruction.
                int sideStartTroops = 0;
                Dictionary<Clan, ClanBattleAgg> byClan = new Dictionary<Clan, ClanBattleAgg>();
                HashSet<string> trackedDestroyablePartyIds = new HashSet<string>();
                MBReadOnlyList<MapEventParty> parties = defeated.Parties;
                for (int i = 0; i < parties.Count; i++)
                {
                    MapEventParty mapParty = parties[i];
                    if (mapParty == null || mapParty.Party == null)
                        continue;

                    Clan clan = RenownLossHelper.GetEligiblePartyClan(mapParty.Party);
                    if (clan == null)
                        continue;

                    int start = mapParty.HealthyManCountAtStart;
                    if (start < 0)
                        start = 0;
                    sideStartTroops += start;

                    if (!RenownLossHelper.IsClanAffected(settings, clan))
                        continue;

                    int died = mapParty.DiedInBattle != null ? mapParty.DiedInBattle.TotalManCount : 0;
                    int wounded = mapParty.WoundedInBattle != null ? mapParty.WoundedInBattle.TotalManCount : 0;
                    int casualties = died + wounded;
                    if (casualties > start)
                        casualties = start;

                    ClanBattleAgg agg;
                    if (!byClan.TryGetValue(clan, out agg))
                    {
                        agg = new ClanBattleAgg();
                        byClan[clan] = agg;
                    }

                    agg.StartTroops += start;
                    agg.Casualties += casualties;
                    agg.PartyCount++;

                    string destroyableId = GetDestroyableMobileTrackId(mapParty.Party);
                    if (!string.IsNullOrEmpty(destroyableId))
                    {
                        if (!agg.DestroyableMobilePartyIds.Contains(destroyableId))
                            agg.DestroyableMobilePartyIds.Add(destroyableId);
                        trackedDestroyablePartyIds.Add(destroyableId);
                    }
                }

                if (byClan.Count == 0)
                    return;

                // Defer apply: MapEventEnded runs before vanilla destroys defeated parties.
                PendingBattleLoss pending = new PendingBattleLoss();
                pending.MapEvent = mapEvent;
                pending.ExactKeys = GetExactLookupKeysForMapEvent(mapEvent);
                pending.OwnStrength = ownStrength;
                pending.EnemyStrength = enemyStrength;
                pending.SideStartTroops = sideStartTroops;
                pending.ContextId = BuildBattleContextId(mapEvent);
                pending.IsNaval = mapEvent.IsNavalMapEvent;
                pending.BattleCaptures = GetMapEventCaptures(mapEvent);
                pending.ByClan = byClan;
                pending.TrackedDestroyablePartyIds = trackedDestroyablePartyIds;
                pending.DestroyedPartyIds = new HashSet<string>();
                pending.TicksRemaining = 2;
                _pendingBattles.Add(pending);
                queued = true;
            }
            finally
            {
                // Keep strength/captures until flush so deferred apply still has event-scoped data.
                if (!queued)
                    ClearMapEventSessionState(mapEvent);
            }
        }

        private void FlushPendingBattle(PendingBattleLoss pending)
        {
            if (pending == null)
                return;

            RefreshDestroyedFromWorld(pending);

            Settings settings = Settings.Instance;
            if (RenownLossHelper.FeatureEnabled(settings) && pending.ByClan != null)
            {
                foreach (KeyValuePair<Clan, ClanBattleAgg> pair in pending.ByClan)
                {
                    ClanBattleAgg agg = pair.Value;
                    if (agg == null)
                        continue;

                    int destroyed = 0;
                    if (pending.DestroyedPartyIds != null && agg.DestroyableMobilePartyIds != null)
                    {
                        for (int i = 0; i < agg.DestroyableMobilePartyIds.Count; i++)
                        {
                            if (pending.DestroyedPartyIds.Contains(agg.DestroyableMobilePartyIds[i]))
                                destroyed++;
                        }
                    }
                    agg.DestroyedDestroyableParties = destroyed;
                    ApplyBattleLossForClan(
                        settings,
                        pending.IsNaval,
                        pair.Key,
                        agg,
                        pending.SideStartTroops,
                        pending.OwnStrength,
                        pending.EnemyStrength,
                        pending.ContextId,
                        pending.BattleCaptures);
                }
            }

            ClearExactKeys(pending.ExactKeys);
            if (pending.MapEvent != null)
                _primaryKeyByMapEvent.Remove(pending.MapEvent);
        }

        // After load (or if destroy events were missed), treat missing/inactive tracked mobiles as destroyed.
        private void RefreshDestroyedFromWorld(PendingBattleLoss pending)
        {
            if (pending == null
                || pending.TrackedDestroyablePartyIds == null
                || pending.TrackedDestroyablePartyIds.Count == 0)
            {
                return;
            }
            if (pending.DestroyedPartyIds == null)
                pending.DestroyedPartyIds = new HashSet<string>();

            foreach (string partyId in pending.TrackedDestroyablePartyIds)
            {
                if (string.IsNullOrEmpty(partyId) || pending.DestroyedPartyIds.Contains(partyId))
                    continue;
                MobileParty mobile = FindMobilePartyByTrackId(partyId);
                if (mobile == null || !mobile.IsActive)
                    pending.DestroyedPartyIds.Add(partyId);
            }
        }

        private static MobileParty FindMobilePartyByTrackId(string partyId)
        {
            if (string.IsNullOrEmpty(partyId) || Campaign.Current == null)
                return null;

            MBReadOnlyList<MobileParty> all = MobileParty.All;
            if (all == null)
                return null;

            for (int i = 0; i < all.Count; i++)
            {
                MobileParty party = all[i];
                if (party != null && party.StringId == partyId)
                    return party;
            }
            return null;
        }

        private void ResolveInitialStrengths(MapEvent mapEvent, out float defenderStrength, out float attackerStrength)
        {
            defenderStrength = 0f;
            attackerStrength = 0f;

            string[] keys = GetExactLookupKeysForMapEvent(mapEvent);
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetStrengthSnapshot(keys[i], out defenderStrength, out attackerStrength))
                    return;
            }

            // Fail-safe: live StrengthOfSide (may already be post-casualty on continued/naval fights).
            if (mapEvent != null && mapEvent.StrengthOfSide != null && mapEvent.StrengthOfSide.Length > 1)
            {
                defenderStrength = mapEvent.StrengthOfSide[0];
                attackerStrength = mapEvent.StrengthOfSide[1];
            }
        }

        private HashSet<string> GetMapEventCaptures(MapEvent mapEvent)
        {
            HashSet<string> merged = new HashSet<string>();
            string[] keys = GetExactLookupKeysForMapEvent(mapEvent);
            for (int i = 0; i < keys.Length; i++)
            {
                HashSet<string> set;
                if (_capturesByMapEvent.TryGetValue(keys[i], out set) && set != null)
                {
                    foreach (string id in set)
                        merged.Add(id);
                }
            }
            return merged;
        }

        private void ClearMapEventSessionState(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return;

            ClearExactKeys(GetExactLookupKeysForMapEvent(mapEvent));
            _primaryKeyByMapEvent.Remove(mapEvent);
        }

        private void ClearExactKeys(string[] keys)
        {
            if (keys == null)
                return;
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i]))
                    continue;
                _capturesByMapEvent.Remove(keys[i]);
                RemoveStrengthSnapshot(keys[i]);
            }
            RemovePrimaryAliasesForKeys(keys);
        }

        private void ApplyBattleLossForClan(
            Settings settings,
            bool isNaval,
            Clan clan,
            ClanBattleAgg agg,
            int sideStartTroops,
            float ownStrength,
            float enemyStrength,
            string contextId,
            HashSet<string> battleCaptures)
        {
            if (clan == null || agg == null || agg.StartTroops <= 0)
                return;

            RenownLossClanGroup group = RenownLossHelper.ClassifyClan(clan);
            float groupScale = RenownLossHelper.GetGroupScale(settings, group);
            if (groupScale <= 0f)
                return;

            float casualtyFraction = (float)agg.Casualties / (float)Math.Max(1, agg.StartTroops);
            float contribution = sideStartTroops > 0
                ? (float)agg.StartTroops / (float)sideStartTroops
                : 1f;

            bool leaderCaptured;
            int otherCaptured;
            RenownLossHelper.CountBattleCaptures(clan, battleCaptures, out leaderCaptured, out otherCaptured);
            int destroyableCount = agg.DestroyableMobilePartyIds != null ? agg.DestroyableMobilePartyIds.Count : 0;
            bool partyDestroyed = destroyableCount > 0 && agg.DestroyedDestroyableParties >= destroyableCount;

            float lossPercent = RenownLossHelper.ComputeBattleLossPercent(
                settings,
                casualtyFraction,
                ownStrength,
                enemyStrength,
                contribution,
                partyDestroyed,
                leaderCaptured,
                otherCaptured,
                groupScale);

            ClanTierModel model = Campaign.Current.Models.ClanTierModel;
            long tierBand = RenownLossHelper.GetTierBand(model, clan.Tier);
            float renownLoss = RenownLossHelper.ComputeRenownLossFromPercent(tierBand, lossPercent);
            float maxLoss = RenownLossHelper.GetMaxLossForPercent(tierBand, settings.GetMaximumLossFraction(), groupScale);
            if (renownLoss > maxLoss)
                renownLoss = maxLoss;

            ApplyLoss(settings, clan, renownLoss, contextId, "battle", isNaval);
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Settings settings = Settings.Instance;
            if (!RenownLossHelper.FeatureEnabled(settings) || !settings.RenownLossFromLostFiefs)
                return;
            if (!RenownLossHelper.SettlementIsTownOrCastle(settlement))
                return;
            if (oldOwner == null)
                return;

            Clan previousClan = oldOwner.Clan;
            Clan newClan = newOwner != null ? newOwner.Clan : null;
            if (previousClan == null || previousClan == newClan)
                return;
            if (!RenownLossHelper.IsClanAffected(settings, previousClan))
                return;

            if (!ShouldCountOwnershipDetail(settings, detail))
            {
                RenownLossHelper.DebugLog(
                    "Skip fief loss " + settlement.StringId + " detail=" + detail + " clan=" + previousClan.StringId);
                return;
            }

            RenownLossClanGroup group = RenownLossHelper.ClassifyClan(previousClan);
            float groupScale = RenownLossHelper.GetGroupScale(settings, group);
            if (groupScale <= 0f)
                return;

            float basePercent = settlement.IsTown ? settings.GetTownLossFraction() : settings.GetCastleLossFraction();

            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)
                basePercent *= settings.GetRebellionMultiplier();

            float lossPercent = basePercent * groupScale;
            float maxPercent = settings.GetMaximumLossFraction();
            if (lossPercent > maxPercent * Math.Max(1f, groupScale))
                lossPercent = maxPercent * Math.Max(1f, groupScale);

            ClanTierModel model = Campaign.Current.Models.ClanTierModel;
            long tierBand = RenownLossHelper.GetTierBand(model, previousClan.Tier);
            float renownLoss = RenownLossHelper.ComputeRenownLossFromPercent(tierBand, lossPercent);
            float maxLoss = RenownLossHelper.GetMaxLossForPercent(tierBand, maxPercent, groupScale);
            if (renownLoss > maxLoss)
                renownLoss = maxLoss;

            string contextId = settlement.StringId;
            RenownLossHelper.DebugLog(
                "Fief " + settlement.StringId
                + " type=" + (settlement.IsTown ? "town" : "castle")
                + " detail=" + detail
                + " prev=" + previousClan.StringId
                + " new=" + (newClan != null ? newClan.StringId : "null")
                + " baseLoss=" + renownLoss.ToString("0.##", CultureInfo.InvariantCulture)
                + " group=" + group
                + " scale=" + groupScale.ToString("0.##", CultureInfo.InvariantCulture));

            ApplyLoss(settings, previousClan, renownLoss, contextId, "fief", false);
        }

        private static bool ShouldCountOwnershipDetail(Settings settings, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)
                return true;
            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)
                return settings.CountRebellionFiefLosses;
            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction)
                return settings.CountLeaveFactionFiefLosses;

            // Default, ByBarter, ByGift, ByKingDecision, ByClanDestruction: never.
            return false;
        }

        private void OnBeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            Settings settings = Settings.Instance;
            if (!RenownLossHelper.FeatureEnabled(settings) || !settings.RenownLossFromExecutions)
                return;
            if (victim == null || victim.Clan == null)
                return;
            if (detail != KillCharacterAction.KillCharacterActionDetail.Executed
                && detail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
            {
                return;
            }

            if (!victim.IsLord || victim.IsChild)
                return;

            Clan clan = victim.Clan;
            if (!RenownLossHelper.IsClanAffected(settings, clan) || clan.IsEliminated || clan.IsBanditFaction)
                return;

            // Clan survives only if another adult lord remains (vanilla destroys leaderless clans).
            bool wasLeader = clan.Leader == victim;
            if (wasLeader && !ClanHasOtherAdultLord(clan, victim))
                return;

            RenownLossClanGroup group = RenownLossHelper.ClassifyClan(clan);
            float groupScale = RenownLossHelper.GetGroupScale(settings, group);
            if (groupScale <= 0f)
                return;

            float bandPercent = wasLeader
                ? RenownLossHelper.ExecutionLeaderBandPercent
                : RenownLossHelper.ExecutionLordBandPercent;

            float lossPercent = bandPercent * groupScale;
            ClanTierModel model = Campaign.Current.Models.ClanTierModel;
            long tierBand = RenownLossHelper.GetTierBand(model, clan.Tier);
            float renownLoss = RenownLossHelper.ComputeRenownLossFromPercent(tierBand, lossPercent);

            // Daily/context caps are enforced inside ApplyLoss via the shared ledger only (no double count).
            string victimId = victim.StringId != null ? victim.StringId : "hero";
            ApplyLoss(settings, clan, renownLoss, "exec:" + victimId, "execution", false);
        }

        private static bool ClanHasOtherAdultLord(Clan clan, Hero exclude)
        {
            if (clan == null || clan.Heroes == null)
                return false;

            for (int i = 0; i < clan.Heroes.Count; i++)
            {
                Hero hero = clan.Heroes[i];
                if (hero == null || hero == exclude || !hero.IsAlive || hero.IsChild || !hero.IsLord)
                    continue;
                return true;
            }

            return false;
        }

        private void ApplyLoss(Settings settings, Clan clan, float requestedLoss, string contextId, string reason, bool naval)
        {
            if (clan == null || requestedLoss <= 0f || Campaign.Current == null)
                return;
            if (clan.IsEliminated)
                return;

            ClanTierModel model = Campaign.Current.Models.ClanTierModel;
            int oldTier = clan.Tier;
            float renownBefore = clan.Renown;
            int day = CurrentDay();

            string clanId = clan.StringId;
            float appliedFromLedger;
            float renownAnchor;
            int tierAnchor;
            GetOrCreateLedger(clanId, contextId, day, renownBefore, oldTier, out appliedFromLedger, out renownAnchor, out tierAnchor);

            RenownLossClanGroup group = RenownLossHelper.ClassifyClan(clan);
            float groupScale = RenownLossHelper.GetGroupScale(settings, group);
            float maxFraction = settings.GetMaximumLossFraction();
            long tierBand = RenownLossHelper.GetTierBand(model, tierAnchor);
            float contextMax = RenownLossHelper.GetMaxLossForPercent(tierBand, maxFraction, groupScale);
            float remainingCap = contextMax - appliedFromLedger;
            if (remainingCap < 0f)
                remainingCap = 0f;

            // Per-clan daily cap across all contexts (battles, fiefs, executions).
            int dayStartTier = GetDayStartTier(clanId, day, tierAnchor);
            long dayBand = RenownLossHelper.GetTierBand(model, dayStartTier);
            float dailyMax = RenownLossHelper.GetMaxLossForPercent(dayBand, maxFraction, groupScale);
            float dailyApplied = SumLedgerLossForClanDay(clanId, day);
            float dailyRemaining = dailyMax - dailyApplied;
            if (dailyRemaining < 0f)
                dailyRemaining = 0f;
            if (remainingCap > dailyRemaining)
                remainingCap = dailyRemaining;

            float loss = requestedLoss;
            if (loss > remainingCap)
                loss = remainingCap;
            if (loss < 1f && requestedLoss >= 1f && remainingCap >= 1f)
                loss = 1f;
            if (loss <= 0f)
            {
                RenownLossHelper.DebugLog(
                    "Cap blocked " + reason + " clan=" + clanId + " context=" + contextId
                    + " requested=" + requestedLoss.ToString("0.##", CultureInfo.InvariantCulture));
                return;
            }

            // At most one tier from this context's anchor, and at most one tier from the day's starting tier.
            float eventFloor = RenownLossHelper.GetMinimumRenownFloor(model, settings, tierAnchor);
            float dayFloor = RenownLossHelper.GetMinimumRenownFloor(model, settings, dayStartTier);
            float floor = eventFloor;
            if (dayFloor > floor)
                floor = dayFloor;

            float newRenown = renownBefore - loss;
            if (newRenown < floor)
            {
                loss = renownBefore - floor;
                if (loss < 0f)
                    loss = 0f;
                newRenown = floor;
            }
            if (newRenown < 0f)
                newRenown = 0f;
            if (loss <= 0f)
                return;

            int appliedOldTier;
            int appliedNewTier;
            if (!ClanTierReconcileBehavior.ApplyRenownAndRecalculateTier(clan, newRenown, out appliedOldTier, out appliedNewTier))
            {
                // Renown may still have been set even if tier set failed.
            }

            PutLedger(clanId, contextId, day, renownAnchor, appliedFromLedger + loss, tierAnchor);

            RenownLossHelper.DebugLog(
                reason + (naval ? "(naval)" : string.Empty)
                + " clan=" + clanId
                + " context=" + contextId
                + " loss=" + loss.ToString("0.##", CultureInfo.InvariantCulture)
                + " renown " + renownBefore.ToString("0.##", CultureInfo.InvariantCulture)
                + "->" + clan.Renown.ToString("0.##", CultureInfo.InvariantCulture)
                + " tier " + appliedOldTier + "->" + clan.Tier
                + " contextCap=" + (appliedFromLedger + loss).ToString("0.##", CultureInfo.InvariantCulture)
                + "/" + contextMax.ToString("0.##", CultureInfo.InvariantCulture)
                + " daily=" + (dailyApplied + loss).ToString("0.##", CultureInfo.InvariantCulture)
                + "/" + dailyMax.ToString("0.##", CultureInfo.InvariantCulture));

            bool isPlayer = clan == Clan.PlayerClan;
            if (isPlayer && settings.NotifyPlayerRenownLoss)
            {
                string message;
                if (clan.Tier < appliedOldTier)
                {
                    message = "Your clan lost " + loss.ToString("0.#", CultureInfo.InvariantCulture)
                        + " renown after " + reason + " (now "
                        + clan.Renown.ToString("0.#", CultureInfo.InvariantCulture)
                        + "). Clan tier " + appliedOldTier + " -> " + clan.Tier + ".";
                }
                else
                {
                    message = "Your clan lost " + loss.ToString("0.#", CultureInfo.InvariantCulture)
                        + " renown after " + reason + " (now "
                        + clan.Renown.ToString("0.#", CultureInfo.InvariantCulture) + ").";
                }
                InformationManager.DisplayMessage(new InformationMessage(message));
            }
            else if (!isPlayer && clan.Tier < appliedOldTier)
            {
                Debug.Print(LogPrefix + "AI demotion " + clan.Name + " tier " + appliedOldTier + " -> " + clan.Tier
                    + " renown=" + clan.Renown.ToString("0.##", CultureInfo.InvariantCulture)
                    + " reason=" + reason);
            }
        }

        private static string BuildBattleContextId(MapEvent mapEvent)
        {
            if (mapEvent == null)
                return "battle";

            if (RenownLossHelper.IsSiegeRelatedBattle(mapEvent) && mapEvent.MapEventSettlement != null)
                return mapEvent.MapEventSettlement.StringId;

            if (!string.IsNullOrEmpty(mapEvent.StringId))
                return mapEvent.StringId;

            return "battle:" + CurrentDay().ToString(CultureInfo.InvariantCulture);
        }

        private static int CurrentDay()
        {
            if (Campaign.Current == null)
                return 0;
            return (int)CampaignTime.Now.ToDays;
        }

        private void GetOrCreateLedger(
            string clanId,
            string contextId,
            int day,
            float renownBefore,
            int tierBefore,
            out float appliedLoss,
            out float renownAnchor,
            out int tierAnchor)
        {
            appliedLoss = 0f;
            renownAnchor = renownBefore;
            tierAnchor = tierBefore;

            List<LedgerEntry> entries = ParseLedger(_lossLedger);
            for (int i = 0; i < entries.Count; i++)
            {
                LedgerEntry e = entries[i];
                if (e.ClanId == clanId && e.ContextId == contextId && e.Day == day)
                {
                    appliedLoss = e.LossApplied;
                    renownAnchor = e.RenownBefore;
                    tierAnchor = e.TierBefore;
                    return;
                }
            }
        }

        private void PutLedger(string clanId, string contextId, int day, float renownBefore, float lossApplied, int tierBefore)
        {
            List<LedgerEntry> entries = ParseLedger(_lossLedger);
            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                LedgerEntry e = entries[i];
                if (e.ClanId == clanId && e.ContextId == contextId && e.Day == day)
                {
                    e.LossApplied = lossApplied;
                    e.RenownBefore = renownBefore;
                    e.TierBefore = tierBefore;
                    entries[i] = e;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                LedgerEntry e = new LedgerEntry();
                e.ClanId = clanId;
                e.ContextId = contextId;
                e.Day = day;
                e.RenownBefore = renownBefore;
                e.LossApplied = lossApplied;
                e.TierBefore = tierBefore;
                entries.Add(e);
            }

            _lossLedger = SerializeLedger(entries);
        }

        private float SumLedgerLossForClanDay(string clanId, int day)
        {
            float sum = 0f;
            List<LedgerEntry> entries = ParseLedger(_lossLedger);
            for (int i = 0; i < entries.Count; i++)
            {
                LedgerEntry e = entries[i];
                if (e.ClanId == clanId && e.Day == day)
                    sum += e.LossApplied;
            }
            return sum;
        }

        // Highest TierBefore seen for this clan today = tier at the day's first prestige loss.
        private int GetDayStartTier(string clanId, int day, int fallbackTier)
        {
            int best = -1;
            List<LedgerEntry> entries = ParseLedger(_lossLedger);
            for (int i = 0; i < entries.Count; i++)
            {
                LedgerEntry e = entries[i];
                if (e.ClanId != clanId || e.Day != day)
                    continue;
                if (e.TierBefore > best)
                    best = e.TierBefore;
            }
            if (best < 0)
                return fallbackTier;
            return best;
        }

        private void PruneLedger()
        {
            int day = CurrentDay();
            List<LedgerEntry> entries = ParseLedger(_lossLedger);
            List<LedgerEntry> kept = new List<LedgerEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (day - entries[i].Day <= LedgerRetentionDays)
                    kept.Add(entries[i]);
            }
            _lossLedger = SerializeLedger(kept);
        }

        private void PutPrimaryAlias(string alias, string primaryKey)
        {
            if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(primaryKey))
                return;

            Dictionary<string, string> map = ParseAliasCache(_primaryKeyAliasCache);
            map[alias] = primaryKey;
            _primaryKeyAliasCache = SerializeAliasCache(map);
        }

        private bool TryGetPrimaryAlias(string alias, out string primaryKey)
        {
            primaryKey = null;
            if (string.IsNullOrEmpty(alias))
                return false;
            Dictionary<string, string> map = ParseAliasCache(_primaryKeyAliasCache);
            return map.TryGetValue(alias, out primaryKey) && !string.IsNullOrEmpty(primaryKey);
        }

        private void RemovePrimaryAliasesForKeys(string[] primaryKeys)
        {
            if (primaryKeys == null || primaryKeys.Length == 0)
                return;

            HashSet<string> drop = new HashSet<string>();
            for (int i = 0; i < primaryKeys.Length; i++)
            {
                if (!string.IsNullOrEmpty(primaryKeys[i]))
                    drop.Add(primaryKeys[i]);
            }
            if (drop.Count == 0)
                return;

            Dictionary<string, string> map = ParseAliasCache(_primaryKeyAliasCache);
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, string> pair in map)
            {
                if (pair.Value != null && drop.Contains(pair.Value))
                    remove.Add(pair.Key);
            }
            if (remove.Count == 0)
                return;
            for (int i = 0; i < remove.Count; i++)
                map.Remove(remove[i]);
            _primaryKeyAliasCache = SerializeAliasCache(map);
        }

        // Reference-aware orphan cleanup. Never wipe caches solely for exceeding a count.
        private void ReconcileMapEventCaches()
        {
            string previousStrength = _initialStrengthCache;
            string previousAliases = _primaryKeyAliasCache;
            try
            {
                HashSet<string> protectedKeys = new HashSet<string>();
                HashSet<MapEvent> pendingMapEvents = new HashSet<MapEvent>();
                CollectProtectedMapEventKeys(protectedKeys, pendingMapEvents);

                Dictionary<string, StrengthEntry> strengthMap = ParseStrengthCache(_initialStrengthCache);
                List<string> strengthRemove = new List<string>();
                foreach (KeyValuePair<string, StrengthEntry> pair in strengthMap)
                {
                    if (pair.Key == null || !protectedKeys.Contains(pair.Key))
                        strengthRemove.Add(pair.Key);
                }
                for (int i = 0; i < strengthRemove.Count; i++)
                    strengthMap.Remove(strengthRemove[i]);
                _initialStrengthCache = SerializeStrengthCache(strengthMap);

                Dictionary<string, string> aliasMap = ParseAliasCache(_primaryKeyAliasCache);
                List<string> aliasRemove = new List<string>();
                foreach (KeyValuePair<string, string> pair in aliasMap)
                {
                    string target = pair.Value;
                    if (string.IsNullOrEmpty(target))
                    {
                        aliasRemove.Add(pair.Key);
                        continue;
                    }
                    if (protectedKeys.Contains(target))
                        continue;
                    if (strengthMap.ContainsKey(target))
                        continue;
                    aliasRemove.Add(pair.Key);
                }
                for (int i = 0; i < aliasRemove.Count; i++)
                    aliasMap.Remove(aliasRemove[i]);
                _primaryKeyAliasCache = SerializeAliasCache(aliasMap);

                List<MapEvent> sessionRemove = new List<MapEvent>();
                foreach (KeyValuePair<MapEvent, string> pair in _primaryKeyByMapEvent)
                {
                    MapEvent mapEvent = pair.Key;
                    if (mapEvent != null && pendingMapEvents.Contains(mapEvent))
                        continue;
                    if (IsLiveMapEvent(mapEvent))
                        continue;
                    sessionRemove.Add(mapEvent);
                }
                for (int i = 0; i < sessionRemove.Count; i++)
                    _primaryKeyByMapEvent.Remove(sessionRemove[i]);

                List<string> captureRemove = new List<string>();
                foreach (string key in _capturesByMapEvent.Keys)
                {
                    if (key == null || !protectedKeys.Contains(key))
                        captureRemove.Add(key);
                }
                for (int i = 0; i < captureRemove.Count; i++)
                    _capturesByMapEvent.Remove(captureRemove[i]);
            }
            catch (Exception ex)
            {
                _initialStrengthCache = previousStrength;
                _primaryKeyAliasCache = previousAliases;
                Debug.Print(LogPrefix + "ReconcileMapEventCaches failed; retained prior caches. " + ex.Message);
            }
        }

        private void CollectProtectedMapEventKeys(HashSet<string> protectedKeys, HashSet<MapEvent> pendingMapEvents)
        {
            if (protectedKeys == null)
                return;

            if (Campaign.Current != null && Campaign.Current.MapEventManager != null)
            {
                MBReadOnlyList<MapEvent> mapEvents = Campaign.Current.MapEventManager.MapEvents;
                if (mapEvents != null)
                {
                    for (int i = 0; i < mapEvents.Count; i++)
                    {
                        MapEvent mapEvent = mapEvents[i];
                        if (!IsLiveMapEvent(mapEvent))
                            continue;
                        AddProtectedKeysForMapEvent(mapEvent, protectedKeys);
                    }
                }
            }

            foreach (KeyValuePair<MapEvent, string> pair in _primaryKeyByMapEvent)
            {
                if (pair.Key == null || !IsLiveMapEvent(pair.Key))
                    continue;
                if (!string.IsNullOrEmpty(pair.Value))
                    protectedKeys.Add(pair.Value);
                AddProtectedKeysForMapEvent(pair.Key, protectedKeys);
            }

            for (int i = 0; i < _pendingBattles.Count; i++)
            {
                PendingBattleLoss pending = _pendingBattles[i];
                if (pending == null)
                    continue;
                if (pending.MapEvent != null && pendingMapEvents != null)
                    pendingMapEvents.Add(pending.MapEvent);
                if (pending.ExactKeys == null)
                    continue;
                for (int k = 0; k < pending.ExactKeys.Length; k++)
                {
                    if (!string.IsNullOrEmpty(pending.ExactKeys[k]))
                        protectedKeys.Add(pending.ExactKeys[k]);
                }
            }

            // Aliases that resolve live events (id:/party:) already point at protected primaries collected above.
            Dictionary<string, string> aliasMap = ParseAliasCache(_primaryKeyAliasCache);
            foreach (KeyValuePair<string, string> pair in aliasMap)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    continue;
                if (protectedKeys.Contains(pair.Value))
                    continue;
                // Keep alias target if its alias key matches a live event StringId currently in the field.
                if (pair.Key != null && pair.Key.StartsWith("id:", StringComparison.Ordinal))
                {
                    string stringId = pair.Key.Substring(3);
                    if (LiveMapEventHasStringId(stringId))
                        protectedKeys.Add(pair.Value);
                }
            }
        }

        private static bool IsLiveMapEvent(MapEvent mapEvent)
        {
            return mapEvent != null && !mapEvent.IsFinalized;
        }

        private static bool LiveMapEventHasStringId(string stringId)
        {
            if (string.IsNullOrEmpty(stringId) || Campaign.Current == null || Campaign.Current.MapEventManager == null)
                return false;
            MBReadOnlyList<MapEvent> mapEvents = Campaign.Current.MapEventManager.MapEvents;
            if (mapEvents == null)
                return false;
            for (int i = 0; i < mapEvents.Count; i++)
            {
                MapEvent mapEvent = mapEvents[i];
                if (!IsLiveMapEvent(mapEvent))
                    continue;
                if (mapEvent.StringId == stringId)
                    return true;
            }
            return false;
        }

        private void AddProtectedKeysForMapEvent(MapEvent mapEvent, HashSet<string> protectedKeys)
        {
            if (mapEvent == null || protectedKeys == null)
                return;

            string[] keys = GetExactLookupKeysForMapEvent(mapEvent);
            for (int i = 0; i < keys.Length; i++)
            {
                if (!string.IsNullOrEmpty(keys[i]))
                    protectedKeys.Add(keys[i]);
            }
        }

        private static Dictionary<string, string> ParseAliasCache(string raw)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(raw))
                return map;

            string[] parts = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] fields = parts[i].Split(new char[] { '|' });
                if (fields.Length < 2 || string.IsNullOrEmpty(fields[0]) || string.IsNullOrEmpty(fields[1]))
                    continue;
                map[fields[0]] = fields[1];
            }
            return map;
        }

        private static string SerializeAliasCache(Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (KeyValuePair<string, string> pair in map)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                    continue;
                if (!first)
                    sb.Append(';');
                first = false;
                sb.Append(Sanitize(pair.Key)).Append('|').Append(Sanitize(pair.Value));
            }
            return sb.ToString();
        }

        // v2|ticks|naval|context|own|enemy|side|keys~|captures|trackedDestroyable|destroyed|clans
        // Older (non-v2) pending rows fail closed on destruction: no tracked mobiles, no invented wipe.
        private static string SerializePendingBattles(List<PendingBattleLoss> pendingList)
        {
            if (pendingList == null || pendingList.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            bool first = true;
            for (int i = 0; i < pendingList.Count; i++)
            {
                PendingBattleLoss pending = pendingList[i];
                if (pending == null || pending.ByClan == null || pending.ByClan.Count == 0)
                    continue;

                if (!first)
                    sb.Append(';');
                first = false;

                sb.Append("v2").Append('|')
                    .Append(pending.TicksRemaining.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(pending.IsNaval ? "1" : "0").Append('|')
                    .Append(SanitizePending(pending.ContextId)).Append('|')
                    .Append(pending.OwnStrength.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(pending.EnemyStrength.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(pending.SideStartTroops.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(JoinSet(pending.ExactKeys, '~')).Append('|')
                    .Append(JoinSet(pending.BattleCaptures, ',')).Append('|')
                    .Append(JoinSet(pending.TrackedDestroyablePartyIds, ',')).Append('|')
                    .Append(JoinSet(pending.DestroyedPartyIds, ',')).Append('|')
                    .Append(SerializePendingClans(pending.ByClan));
            }
            return sb.ToString();
        }

        private static void DeserializePendingBattles(string raw, List<PendingBattleLoss> into)
        {
            if (into == null || string.IsNullOrEmpty(raw))
                return;

            string[] parts = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] fields = parts[i].Split(new char[] { '|' });
                bool isV2 = fields.Length > 0 && fields[0] == "v2";
                int baseIndex = isV2 ? 1 : 0;
                if (fields.Length < baseIndex + 11)
                    continue;

                float own;
                float enemy;
                int sideTroops;
                int savedTicks;
                if (!int.TryParse(fields[baseIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out savedTicks))
                    continue;
                if (!float.TryParse(fields[baseIndex + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out own))
                    continue;
                if (!float.TryParse(fields[baseIndex + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out enemy))
                    continue;
                if (!int.TryParse(fields[baseIndex + 5], NumberStyles.Integer, CultureInfo.InvariantCulture, out sideTroops))
                    continue;
                if (savedTicks < 0)
                    continue;

                Dictionary<Clan, ClanBattleAgg> byClan = DeserializePendingClans(fields[baseIndex + 10], isV2);
                if (byClan == null || byClan.Count == 0)
                    continue;

                PendingBattleLoss pending = new PendingBattleLoss();
                // MapEvent ref is gone after load; one campaign tick then flush.
                pending.TicksRemaining = 1;
                pending.IsNaval = fields[baseIndex + 1] == "1";
                pending.ContextId = UnsanitizePending(fields[baseIndex + 2]);
                pending.OwnStrength = own;
                pending.EnemyStrength = enemy;
                pending.SideStartTroops = sideTroops;
                pending.ExactKeys = UnsanitizeAll(SplitNonEmpty(fields[baseIndex + 6], '~'));
                pending.BattleCaptures = ToHashSet(UnsanitizeAll(SplitNonEmpty(fields[baseIndex + 7], ',')));
                if (isV2)
                {
                    pending.TrackedDestroyablePartyIds = ToHashSet(UnsanitizeAll(SplitNonEmpty(fields[baseIndex + 8], ',')));
                    pending.DestroyedPartyIds = ToHashSet(UnsanitizeAll(SplitNonEmpty(fields[baseIndex + 9], ',')));
                }
                else
                {
                    // Fail closed: legacy rows may have mixed settlement ids into the track list.
                    pending.TrackedDestroyablePartyIds = new HashSet<string>();
                    pending.DestroyedPartyIds = new HashSet<string>();
                }
                pending.ByClan = byClan;
                into.Add(pending);
            }
        }

        private static string SerializePendingClans(Dictionary<Clan, ClanBattleAgg> byClan)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (KeyValuePair<Clan, ClanBattleAgg> pair in byClan)
            {
                if (pair.Key == null || pair.Value == null || string.IsNullOrEmpty(pair.Key.StringId))
                    continue;
                if (!first)
                    sb.Append('#');
                first = false;
                sb.Append(SanitizePending(pair.Key.StringId)).Append(':')
                    .Append(pair.Value.StartTroops.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(pair.Value.Casualties.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(pair.Value.PartyCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(JoinList(pair.Value.DestroyableMobilePartyIds, '+'));
            }
            return sb.ToString();
        }

        private static Dictionary<Clan, ClanBattleAgg> DeserializePendingClans(string raw, bool allowDestroyableIds)
        {
            Dictionary<Clan, ClanBattleAgg> byClan = new Dictionary<Clan, ClanBattleAgg>();
            if (string.IsNullOrEmpty(raw))
                return byClan;

            string[] clans = raw.Split(new char[] { '#' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < clans.Length; i++)
            {
                string[] fields = clans[i].Split(new char[] { ':' });
                if (fields.Length < 5)
                    continue;

                Clan clan = FindClanById(UnsanitizePending(fields[0]));
                if (clan == null)
                    continue;

                int start;
                int casualties;
                int partyCount;
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
                    continue;
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out casualties))
                    continue;
                if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out partyCount))
                    continue;

                ClanBattleAgg agg = new ClanBattleAgg();
                agg.StartTroops = start;
                agg.Casualties = casualties;
                agg.PartyCount = partyCount;
                if (allowDestroyableIds)
                {
                    string[] partyIds = UnsanitizeAll(SplitNonEmpty(fields[4], '+'));
                    for (int p = 0; p < partyIds.Length; p++)
                        agg.DestroyableMobilePartyIds.Add(partyIds[p]);
                }
                byClan[clan] = agg;
            }
            return byClan;
        }

        private static Clan FindClanById(string clanId)
        {
            if (string.IsNullOrEmpty(clanId) || Campaign.Current == null)
                return null;
            MBReadOnlyList<Clan> all = Clan.All;
            if (all == null)
                return null;
            for (int i = 0; i < all.Count; i++)
            {
                Clan clan = all[i];
                if (clan != null && clan.StringId == clanId)
                    return clan;
            }
            return null;
        }

        private static string JoinSet(IEnumerable<string> values, char separator)
        {
            if (values == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                if (!first)
                    sb.Append(separator);
                first = false;
                sb.Append(SanitizePending(value));
            }
            return sb.ToString();
        }

        private static string JoinList(List<string> values, char separator)
        {
            if (values == null || values.Count == 0)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            bool first = true;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.IsNullOrEmpty(values[i]))
                    continue;
                if (!first)
                    sb.Append(separator);
                first = false;
                sb.Append(SanitizePending(values[i]));
            }
            return sb.ToString();
        }

        private static string[] SplitNonEmpty(string raw, char separator)
        {
            if (string.IsNullOrEmpty(raw))
                return new string[0];
            return raw.Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static HashSet<string> ToHashSet(string[] values)
        {
            HashSet<string> set = new HashSet<string>();
            if (values == null)
                return set;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    set.Add(values[i]);
            }
            return set;
        }

        // Reversible escapes so clan, hero, and party StringIds may contain the record separators.
        private static string SanitizePending(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";

            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '|':
                        sb.Append("\\p");
                        break;
                    case ';':
                        sb.Append("\\s");
                        break;
                    case '#':
                        sb.Append("\\h");
                        break;
                    case '~':
                        sb.Append("\\t");
                        break;
                    case '+':
                        sb.Append("\\a");
                        break;
                    case ',':
                        sb.Append("\\c");
                        break;
                    case ':':
                        sb.Append("\\o");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string UnsanitizePending(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return value;

            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char next = value[i + 1];
                i++;
                switch (next)
                {
                    case '\\':
                        sb.Append('\\');
                        break;
                    case 'p':
                        sb.Append('|');
                        break;
                    case 's':
                        sb.Append(';');
                        break;
                    case 'h':
                        sb.Append('#');
                        break;
                    case 't':
                        sb.Append('~');
                        break;
                    case 'a':
                        sb.Append('+');
                        break;
                    case 'c':
                        sb.Append(',');
                        break;
                    case 'o':
                        sb.Append(':');
                        break;
                    default:
                        sb.Append('\\');
                        sb.Append(next);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string[] UnsanitizeAll(string[] values)
        {
            if (values == null || values.Length == 0)
                return values ?? new string[0];
            for (int i = 0; i < values.Length; i++)
                values[i] = UnsanitizePending(values[i]);
            return values;
        }

        private void PutStrengthSnapshot(string key, float defenderStrength, float attackerStrength)
        {
            if (string.IsNullOrEmpty(key))
                return;

            Dictionary<string, StrengthEntry> map = ParseStrengthCache(_initialStrengthCache);
            StrengthEntry entry = new StrengthEntry();
            entry.Key = key;
            entry.Defender = defenderStrength;
            entry.Attacker = attackerStrength;
            map[key] = entry;
            _initialStrengthCache = SerializeStrengthCache(map);
        }

        private bool HasStrengthSnapshot(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            Dictionary<string, StrengthEntry> map = ParseStrengthCache(_initialStrengthCache);
            return map.ContainsKey(key);
        }

        private bool TryGetStrengthSnapshot(string key, out float defenderStrength, out float attackerStrength)
        {
            defenderStrength = 0f;
            attackerStrength = 0f;
            if (string.IsNullOrEmpty(key))
                return false;

            Dictionary<string, StrengthEntry> map = ParseStrengthCache(_initialStrengthCache);
            StrengthEntry entry;
            if (!map.TryGetValue(key, out entry))
                return false;
            defenderStrength = entry.Defender;
            attackerStrength = entry.Attacker;
            return true;
        }

        private void RemoveStrengthSnapshot(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            Dictionary<string, StrengthEntry> map = ParseStrengthCache(_initialStrengthCache);
            if (!map.Remove(key))
                return;
            _initialStrengthCache = SerializeStrengthCache(map);
        }

        private struct StrengthEntry
        {
            public string Key;
            public float Defender;
            public float Attacker;
        }

        private static Dictionary<string, StrengthEntry> ParseStrengthCache(string raw)
        {
            Dictionary<string, StrengthEntry> map = new Dictionary<string, StrengthEntry>();
            if (string.IsNullOrEmpty(raw))
                return map;

            string[] parts = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] fields = parts[i].Split(new char[] { '|' });
                if (fields.Length < 3)
                    continue;
                float def;
                float atk;
                if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out def))
                    continue;
                if (!float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out atk))
                    continue;
                StrengthEntry entry = new StrengthEntry();
                entry.Key = fields[0];
                entry.Defender = def;
                entry.Attacker = atk;
                map[entry.Key] = entry;
            }
            return map;
        }

        private static string SerializeStrengthCache(Dictionary<string, StrengthEntry> map)
        {
            if (map == null || map.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (KeyValuePair<string, StrengthEntry> pair in map)
            {
                StrengthEntry entry = pair.Value;
                if (string.IsNullOrEmpty(entry.Key))
                    continue;
                if (!first)
                    sb.Append(';');
                first = false;
                sb.Append(Sanitize(entry.Key)).Append('|')
                    .Append(entry.Defender.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.Attacker.ToString("R", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static List<LedgerEntry> ParseLedger(string raw)
        {
            List<LedgerEntry> list = new List<LedgerEntry>();
            if (string.IsNullOrEmpty(raw))
                return list;

            string[] parts = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] fields = parts[i].Split(new char[] { '|' });
                if (fields.Length < 6)
                    continue;

                LedgerEntry e = new LedgerEntry();
                e.ClanId = fields[0];
                e.ContextId = fields[1];
                int day;
                float renownBefore;
                float loss;
                int tierBefore;
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out day))
                    continue;
                if (!float.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out renownBefore))
                    continue;
                if (!float.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out loss))
                    continue;
                if (!int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out tierBefore))
                    continue;
                e.Day = day;
                e.RenownBefore = renownBefore;
                e.LossApplied = loss;
                e.TierBefore = tierBefore;
                list.Add(e);
            }

            return list;
        }

        private static string SerializeLedger(List<LedgerEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                LedgerEntry e = entries[i];
                if (i > 0)
                    sb.Append(';');
                sb.Append(Sanitize(e.ClanId)).Append('|')
                    .Append(Sanitize(e.ContextId)).Append('|')
                    .Append(e.Day.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.RenownBefore.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.LossApplied.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(e.TierBefore.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";
            return value.Replace("|", "_").Replace(";", "_");
        }

        private sealed class ClanBattleAgg
        {
            public int StartTroops;
            public int Casualties;
            public int DestroyedDestroyableParties;
            public int PartyCount;
            // MobileParty.StringId values only; settlements never enter this list.
            public readonly List<string> DestroyableMobilePartyIds = new List<string>();
        }

        private sealed class PendingBattleLoss
        {
            public MapEvent MapEvent;
            public string[] ExactKeys;
            public float OwnStrength;
            public float EnemyStrength;
            public int SideStartTroops;
            public string ContextId;
            public bool IsNaval;
            public HashSet<string> BattleCaptures;
            public Dictionary<Clan, ClanBattleAgg> ByClan;
            public HashSet<string> TrackedDestroyablePartyIds;
            public HashSet<string> DestroyedPartyIds;
            public int TicksRemaining;
        }

        private struct LedgerEntry
        {
            public string ClanId;
            public string ContextId;
            public int Day;
            public float RenownBefore;
            public float LossApplied;
            public int TierBefore;
        }
    }
}
