using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;

namespace ClanTierExpanded
{
    // High-tier dynasty bonuses (tiers above vanilla 6). On by default; turn off in MCM if unwanted.
    public sealed partial class Settings
    {
        private const string DynastyRoot = "Enabled/" + DynastyBonusesGroupLeaf;
        private const string DynastyLogistics = DynastyRoot + "/Logistics";
        private const string DynastyCourt = DynastyRoot + "/Court";
        private const string DynastyReputation = DynastyRoot + "/Reputation";
        private const string DynastyTrade = DynastyRoot + "/Trade house";
        private const string DynastyMilestones = DynastyRoot + "/Milestones";

        internal const int DynastyBaseTier = 6;
        internal const int MilestoneTier7 = 7;
        internal const int MilestoneTier9 = 9;
        internal const int MilestoneTier11 = 11;
        internal const int MilestoneTier13 = 13;

        private bool _highTierDynastyBonuses;

        [SettingPropertyBool("High-tier dynasty bonuses", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Small bonuses for clans above tier 6: travel, wages, Influence, towns, and trade. On by default; turn off here if you do not want them. Does not add parties or combat power.")]
        [SettingPropertyGroup(DynastyRoot, GroupOrder = 3)]
        public bool HighTierDynastyBonuses
        {
            get { return _highTierDynastyBonuses; }
            set
            {
                if (_highTierDynastyBonuses == value)
                    return;
                _highTierDynastyBonuses = value;
                _uiVersion++;
                OnPropertyChanged("HighTierDynastyBonuses");
                OnPropertyChanged("UIVersion");
            }
        }

        [SettingPropertyBool("Affect player clan", Order = 1, RequireRestart = false, HintText = "Give these bonuses to your clan.")]
        [SettingPropertyGroup(DynastyRoot)]
        public bool DynastyAffectPlayerClan { get; set; }

        [SettingPropertyBool("Affect AI clans", Order = 2, RequireRestart = false, HintText = "Give these bonuses to other noble clans.")]
        [SettingPropertyGroup(DynastyRoot)]
        public bool DynastyAffectAiClans { get; set; }

        [SettingPropertyInteger("Bonus tier depth", 1, 12, "0", Order = 3, RequireRestart = false, HintText = "How many tiers above 6 count toward the bonuses. With 6, you reach full strength at tier 12.")]
        [SettingPropertyGroup(DynastyRoot)]
        public int DynastyBonusTierDepth { get; set; }

        // --- Logistics ---
        [SettingPropertyBool("Logistics", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Faster travel, cheaper wages, less food use, and better healing for clan parties.")]
        [SettingPropertyGroup(DynastyLogistics, GroupOrder = 0)]
        public bool DynastyLogisticsEnabled { get; set; }

        [SettingPropertyFloatingInteger("Party speed per excess tier", 0f, 2f, "0.00\\%", Order = 1, RequireRestart = false, HintText = "Extra map speed for each tier above 6. Default 0.5%.")]
        [SettingPropertyGroup(DynastyLogistics)]
        public float DynastySpeedPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Wage discount per excess tier", 0f, 2f, "0.00\\%", Order = 2, RequireRestart = false, HintText = "Cheaper troop wages for each tier above 6. Default 0.4%.")]
        [SettingPropertyGroup(DynastyLogistics)]
        public float DynastyWageDiscountPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Food saving per excess tier", 0f, 2f, "0.00\\%", Order = 3, RequireRestart = false, HintText = "Less party food use for each tier above 6. Default 0.3%.")]
        [SettingPropertyGroup(DynastyLogistics)]
        public float DynastyFoodSavingPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Healing per excess tier", 0f, 2f, "0.00\\%", Order = 4, RequireRestart = false, HintText = "Better daily healing for each tier above 6. Default 0.5%.")]
        [SettingPropertyGroup(DynastyLogistics)]
        public float DynastyHealingPercentPerTier { get; set; }

        // --- Court ---
        [SettingPropertyBool("Court", IsToggle = true, Order = 0, RequireRestart = false, HintText = "More Influence, stronger friendship gains, and slow companion recovery with the clan leader.")]
        [SettingPropertyGroup(DynastyCourt, GroupOrder = 1)]
        public bool DynastyCourtEnabled { get; set; }

        [SettingPropertyFloatingInteger("Influence gain per excess tier", 0f, 3f, "0.00\\%", Order = 1, RequireRestart = false, HintText = "Extra Influence income for each tier above 6. Only helps when Influence is rising. Default 0.75%.")]
        [SettingPropertyGroup(DynastyCourt)]
        public float DynastyInfluencePercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Positive relation boost per excess tier", 0f, 5f, "0.00\\%", Order = 2, RequireRestart = false, HintText = "Chance of a bit more on positive relation gains, for each tier above 6. Default 1%.")]
        [SettingPropertyGroup(DynastyCourt)]
        public float DynastyRelationBoostPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Companion relation recovery chance", 0f, 25f, "0.0\\%", Order = 3, RequireRestart = false, HintText = "Daily chance a companion under 50 relation gains +1 with the clan leader. Scales with high tiers. Default 4%.")]
        [SettingPropertyGroup(DynastyCourt)]
        public float DynastyCompanionRelationRecoveryChance { get; set; }

        // --- Reputation ---
        [SettingPropertyBool("Reputation", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Better loyalty, volunteers, and militia in your clan's towns and castles.")]
        [SettingPropertyGroup(DynastyReputation, GroupOrder = 2)]
        public bool DynastyReputationEnabled { get; set; }

        [SettingPropertyFloatingInteger("Loyalty per excess tier", 0f, 0.25f, "0.00", Order = 1, RequireRestart = false, HintText = "Extra daily loyalty in owned towns for each tier above 6. Default 0.05.")]
        [SettingPropertyGroup(DynastyReputation)]
        public float DynastyLoyaltyPerExcessTier { get; set; }

        [SettingPropertyFloatingInteger("Volunteer production per excess tier", 0f, 3f, "0.00\\%", Order = 2, RequireRestart = false, HintText = "More volunteers in owned settlements for each tier above 6. Default 0.75%.")]
        [SettingPropertyGroup(DynastyReputation)]
        public float DynastyVolunteerPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Militia growth per excess tier", 0f, 3f, "0.00\\%", Order = 3, RequireRestart = false, HintText = "Faster militia growth when militia is rising, for each tier above 6. Default 0.5%.")]
        [SettingPropertyGroup(DynastyReputation)]
        public float DynastyMilitiaPercentPerTier { get; set; }

        // --- Trade ---
        [SettingPropertyBool("Trade house", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Extra income from clan caravans and workshops.")]
        [SettingPropertyGroup(DynastyTrade, GroupOrder = 3)]
        public bool DynastyTradeEnabled { get; set; }

        [SettingPropertyFloatingInteger("Caravan income per excess tier", 0f, 3f, "0.00\\%", Order = 1, RequireRestart = false, HintText = "Extra caravan income for each tier above 6. Default 1%.")]
        [SettingPropertyGroup(DynastyTrade)]
        public float DynastyCaravanPercentPerTier { get; set; }

        [SettingPropertyFloatingInteger("Workshop income per excess tier", 0f, 3f, "0.00\\%", Order = 2, RequireRestart = false, HintText = "Extra workshop income for each tier above 6. Default 1%.")]
        [SettingPropertyGroup(DynastyTrade)]
        public float DynastyWorkshopPercentPerTier { get; set; }

        // --- Milestones (pick one of two at each gate) ---
        [SettingPropertyBool("Milestone perks", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Pick one bonus at tiers 7, 9, 11, and 13. Set each choice to 0 or 1.")]
        [SettingPropertyGroup(DynastyMilestones, GroupOrder = 4)]
        public bool DynastyMilestonesEnabled { get; set; }

        [SettingPropertyInteger("Tier 7 choice", 0, 1, "0", Order = 1, RequireRestart = false, HintText = "0: +2% party speed. 1: +2% cheaper wages. Needs tier 7+.")]
        [SettingPropertyGroup(DynastyMilestones)]
        public int DynastyMilestoneTier7Choice { get; set; }

        [SettingPropertyInteger("Tier 9 choice", 0, 1, "0", Order = 2, RequireRestart = false, HintText = "0: +3% Influence income. 1: +0.15 loyalty per day in owned towns. Needs tier 9+.")]
        [SettingPropertyGroup(DynastyMilestones)]
        public int DynastyMilestoneTier9Choice { get; set; }

        [SettingPropertyInteger("Tier 11 choice", 0, 1, "0", Order = 3, RequireRestart = false, HintText = "0: +5% caravan income. 1: +5% workshop income. Needs tier 11+.")]
        [SettingPropertyGroup(DynastyMilestones)]
        public int DynastyMilestoneTier11Choice { get; set; }

        [SettingPropertyInteger("Tier 13 choice", 0, 1, "0", Order = 4, RequireRestart = false, HintText = "0: +3% healing. 1: +3% food saving. Needs tier 13+.")]
        [SettingPropertyGroup(DynastyMilestones)]
        public int DynastyMilestoneTier13Choice { get; set; }

        private void InitDynastyBonusDefaults()
        {
            _highTierDynastyBonuses = true;
            DynastyAffectPlayerClan = true;
            DynastyAffectAiClans = true;
            DynastyBonusTierDepth = 6;

            DynastyLogisticsEnabled = true;
            DynastySpeedPercentPerTier = 0.5f;
            DynastyWageDiscountPercentPerTier = 0.4f;
            DynastyFoodSavingPercentPerTier = 0.3f;
            DynastyHealingPercentPerTier = 0.5f;

            DynastyCourtEnabled = true;
            DynastyInfluencePercentPerTier = 0.75f;
            DynastyRelationBoostPercentPerTier = 1f;
            DynastyCompanionRelationRecoveryChance = 4f;

            DynastyReputationEnabled = true;
            DynastyLoyaltyPerExcessTier = 0.05f;
            DynastyVolunteerPercentPerTier = 0.75f;
            DynastyMilitiaPercentPerTier = 0.5f;

            DynastyTradeEnabled = true;
            DynastyCaravanPercentPerTier = 1f;
            DynastyWorkshopPercentPerTier = 1f;

            DynastyMilestonesEnabled = true;
            DynastyMilestoneTier7Choice = 0;
            DynastyMilestoneTier9Choice = 0;
            DynastyMilestoneTier11Choice = 0;
            DynastyMilestoneTier13Choice = 0;
        }

        internal int GetDynastyBonusTierDepth()
        {
            int depth = DynastyBonusTierDepth;
            if (depth < 1)
                return 1;
            if (depth > 12)
                return 12;
            return depth;
        }
    }
}
