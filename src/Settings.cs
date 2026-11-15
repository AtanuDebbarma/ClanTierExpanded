using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    public sealed partial class Settings : AttributeGlobalSettings<Settings>
    {
        public const int MinConfigurableMaxTier = 6;
        public const int AbsoluteMaxTier = 50;

        // Friendly leaf names; McmVisibilityPatch matches these strings (no raw marker path segments).
        internal const string CaravanSupportGroupLeaf = "CaravanPartyLimitIncreasedFixed (Mod Support)";
        internal const string RenownFormulaGroupLeaf = "Formula";
        internal const string RenownManualGroupLeaf = "Manual values";
        internal const string RenownDeclineGroupLeaf = "Renown decline";
        internal const string DynastyBonusesGroupLeaf = "High-tier dynasty bonuses";

        private const string CaravanSupportGroupName = CaravanSupportGroupLeaf;
        private const string RenownFormulaGroupName = RenownFormulaGroupLeaf;
        private const string RenownManualGroupName = RenownManualGroupLeaf;
        private const string RenownDeclineGroupName = RenownDeclineGroupLeaf;

        public const int MinCaravanPartySizeCap = 50;
        public const int MaxCaravanPartySizeCap = 1000;
        public const int DefaultCaravanPartySizeCap = 500;

        private bool _useManualRenown;
        private int _uiVersion = 1;
        private static string _lastThresholdAdjustmentWarning;

        // Seed curve from the original mod (tiers 0-12). Higher tiers grow from the last value.
        private static readonly int[] FormulaSeed = new int[]
        {
            0, 50, 150, 350, 900, 2350, 6150, 16100, 42150, 110350, 262850, 636050, 1534950
        };

        public override string DisplayName
        {
            get { return "Clan Tier Expanded"; }
        }

        public override string FolderName
        {
            get { return "ClanTierExpanded"; }
        }

        public override string Id
        {
            get { return "ClanTierExpanded_v1.0.0"; }
        }

        public override string FormatType
        {
            get { return "json2"; }
        }

        public override int UIVersion
        {
            get { return _uiVersion; }
        }

        [SettingPropertyBool("Enabled", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Turns on custom clan tiers. Turn this off and save before removing the mod.")]
        [SettingPropertyGroup("Enabled", GroupOrder = 0)]
        public bool Enabled { get; set; }

        [SettingPropertyInteger("Max clan tier", MinConfigurableMaxTier, AbsoluteMaxTier, "0", Order = 1, RequireRestart = false, HintText = "Highest clan tier allowed. Vanilla is 6. Default 12.")]
        [SettingPropertyGroup("Enabled")]
        public int MaxClanTier { get; set; }

        [SettingPropertyBool("Extra parties at high tiers", Order = 2, RequireRestart = false, HintText = "+1 party slot at tiers 7, 9, and 11. Turns itself off if Companion & Party Limits Expanded is loaded.")]
        [SettingPropertyGroup("Enabled")]
        public bool ExtraPartiesAtHighTiers { get; set; }

        [SettingPropertyInteger("Max caravan party size", MinCaravanPartySizeCap, MaxCaravanPartySizeCap, "0", Order = 0, RequireRestart = false, HintText = "Troop limit for caravan parties when Caravan Party Limit Increased Fixed is also loaded. Default 500.")]
        [SettingPropertyGroup("Enabled/" + CaravanSupportGroupName, GroupOrder = 2)]
        public int MaxCaravanPartySize { get; set; }

        [SettingPropertyBool("Manual renown", Order = 0, RequireRestart = false, HintText = "Off uses the formula. On lets you set each tier yourself.")]
        [SettingPropertyGroup("Enabled/Renown", GroupOrder = 1)]
        public bool UseManualRenown
        {
            get { return _useManualRenown; }
            set
            {
                if (_useManualRenown == value)
                    return;
                _useManualRenown = value;
                _uiVersion++;
                OnPropertyChanged("UseManualRenown");
                OnPropertyChanged("UIVersion");
            }
        }

        [SettingPropertyFloatingInteger("Renown multiplier", 0.25f, 5f, "0.00", Order = 1, RequireRestart = false, HintText = "Makes formula Renown requirements higher or lower. Default 1.00.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownFormulaGroupName)]
        public float RenownMultiplier { get; set; }

        [SettingPropertyFloatingInteger("Growth per tier", 1.5f, 4f, "0.00", Order = 2, RequireRestart = false, HintText = "How steeply Renown needs rise after tier 12. Default 2.41.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownFormulaGroupName)]
        public float GrowthPerTier { get; set; }

        // Percent MCM values are whole percents (2 = 2% of the current tier Renown band). Use Get*Fraction helpers in code.
        [SettingPropertyBool("Renown loss from defeats", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Lose Renown after defeats, and optionally from lost fiefs or executions. On by default.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName, GroupOrder = 2)]
        public bool RenownLossFromDefeats { get; set; }

        [SettingPropertyBool("Affect player clan", Order = 1, RequireRestart = false, HintText = "Apply Renown loss to your clan.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool AffectPlayerClan { get; set; }

        [SettingPropertyBool("Affect AI clans", Order = 2, RequireRestart = false, HintText = "Apply Renown loss to AI clans.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool AffectAiClans { get; set; }

        [SettingPropertyFloatingInteger("Your clan loss scale", 0f, 300f, "0\\%", Order = 3, RequireRestart = false, HintText = "How strong Renown loss is for your clan. 100% is normal. 0% turns it off.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float PlayerClanLossScale { get; set; }

        [SettingPropertyFloatingInteger("Your kingdom clan loss scale", 0f, 300f, "0\\%", Order = 4, RequireRestart = false, HintText = "How strong Renown loss is for other clans in your kingdom. 0% turns it off.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float PlayerKingdomClanLossScale { get; set; }

        [SettingPropertyFloatingInteger("Other AI clan loss scale", 0f, 300f, "0\\%", Order = 5, RequireRestart = false, HintText = "How strong Renown loss is for foreign and independent AI clans. 0% turns it off.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float OtherAiClanLossScale { get; set; }

        [SettingPropertyFloatingInteger("Base loss percent", 0.25f, 10f, "0.00\\%", Order = 6, RequireRestart = false, HintText = "How much of your current tier's Renown span you lose after a defeat. Default 2%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float BaseLossPercent { get; set; }

        [SettingPropertyFloatingInteger("Maximum loss percent per battle", 1f, 50f, "0\\%", Order = 7, RequireRestart = false, HintText = "Cap on Renown loss from one battle, or across a day from battles, fiefs, and executions. Default 10%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float MaximumLossPercentPerBattle { get; set; }

        [SettingPropertyInteger("Minimum clan tier", 0, 50, "0", Order = 8, RequireRestart = false, HintText = "Clans will not drop below this tier from Renown loss. Default 1.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public int MinimumClanTierForDecline { get; set; }

        [SettingPropertyFloatingInteger("Clan leader capture multiplier", 100f, 200f, "0\\%", Order = 9, RequireRestart = false, HintText = "Extra Renown loss if the clan leader is captured. Default 125%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float ClanLeaderCaptureMultiplier { get; set; }

        [SettingPropertyBool("Notify player", Order = 10, RequireRestart = false, HintText = "Show messages when you lose Renown or face a throne challenge.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool NotifyPlayerRenownLoss { get; set; }

        [SettingPropertyBool("Renown loss from lost fiefs", Order = 11, RequireRestart = false, HintText = "Lose Renown when a town or castle is taken.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool RenownLossFromLostFiefs { get; set; }

        [SettingPropertyFloatingInteger("Castle loss percent", 0f, 25f, "0.00\\%", Order = 12, RequireRestart = false, HintText = "How much of your current tier's Renown span you lose when a castle falls. Default 2%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float CastleLossPercent { get; set; }

        [SettingPropertyFloatingInteger("Town loss percent", 0f, 25f, "0.00\\%", Order = 13, RequireRestart = false, HintText = "How much of your current tier's Renown span you lose when a town falls. Default 4%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float TownLossPercent { get; set; }

        [SettingPropertyBool("Count rebellion losses", Order = 14, RequireRestart = false, HintText = "Also lose Renown when a fief falls to rebellion.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool CountRebellionFiefLosses { get; set; }

        [SettingPropertyFloatingInteger("Rebellion multiplier", 0f, 300f, "0\\%", Order = 15, RequireRestart = false, HintText = "How strong rebellion Renown losses are. 100% is normal.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float RebellionLossMultiplier { get; set; }

        [SettingPropertyBool("Count fiefs surrendered when leaving a kingdom", Order = 16, RequireRestart = false, HintText = "Lose Renown for fiefs you give up when leaving a kingdom. Off by default.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool CountLeaveFactionFiefLosses { get; set; }

        [SettingPropertyBool("Renown loss from executions", Order = 17, RequireRestart = false, HintText = "Lose Renown when an adult lord of the clan is executed. Off by default.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool RenownLossFromExecutions { get; set; }

        [SettingPropertyBool("Low prestige ruler challenge", Order = 18, RequireRestart = false, HintText = "Start a throne vote if the ruling clan stays too low in tier and Renown.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public bool LowPrestigeRulerChallenge { get; set; }

        [SettingPropertyInteger("Ruler challenge tier threshold", 1, 6, "0", Order = 19, RequireRestart = false, HintText = "Ruling clan must stay at this tier or higher, with enough Renown, to avoid a vote. Default 2.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public int RulerChallengeTierThreshold { get; set; }

        [SettingPropertyInteger("Days below threshold before vote", 1, 30, "0", Order = 20, RequireRestart = false, HintText = "How many days in a row they must stay low before the vote. Default 3.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public int RulerChallengeDaysBelowThreshold { get; set; }

        [SettingPropertyInteger("Ruler challenge cooldown days", 7, 365, "0", Order = 21, RequireRestart = false, HintText = "Wait this many days between prestige votes in the same kingdom. Default 60.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public int RulerChallengeCooldownDays { get; set; }

        [SettingPropertyFloatingInteger("Incumbent merit percent", 5f, 100f, "0\\%", Order = 22, RequireRestart = false, HintText = "How much support the current ruler keeps in the vote. Lower makes it harder to keep the crown. Default 45%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float IncumbentMeritMultiplier { get; set; }

        [SettingPropertyFloatingInteger("Challenge support influence percent", 100f, 500f, "0\\%", Order = 23, RequireRestart = false, HintText = "How expensive it is to back someone in this prestige vote. Default 150%.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownDeclineGroupName)]
        public float RulerChallengeSupportInfluenceMultiplier { get; set; }

        // Converts MCM whole-percent values to fractions used by combat/fief math.
        public float GetBaseLossFraction()
        {
            return ClampPercentToFraction(BaseLossPercent, 0.25f, 10f);
        }

        public float GetMaximumLossFraction()
        {
            return ClampPercentToFraction(MaximumLossPercentPerBattle, 1f, 50f);
        }

        public float GetCastleLossFraction()
        {
            return ClampPercentToFraction(CastleLossPercent, 0f, 25f);
        }

        public float GetTownLossFraction()
        {
            return ClampPercentToFraction(TownLossPercent, 0f, 25f);
        }

        public float GetGroupScaleFraction(float percentPoints)
        {
            return ClampPercentToFraction(percentPoints, 0f, 300f);
        }

        public float GetCaptureMultiplier()
        {
            float fraction = ClampPercentToFraction(ClanLeaderCaptureMultiplier, 100f, 200f);
            if (fraction < 1f)
                return 1f;
            return fraction;
        }

        public float GetRebellionMultiplier()
        {
            return ClampPercentToFraction(RebellionLossMultiplier, 0f, 300f);
        }

        public float GetIncumbentMeritFraction()
        {
            return ClampPercentToFraction(IncumbentMeritMultiplier, 5f, 100f);
        }

        public float GetChallengeSupportInfluenceMultiplier()
        {
            float fraction = ClampPercentToFraction(RulerChallengeSupportInfluenceMultiplier, 100f, 500f);
            if (fraction < 1f)
                return 1f;
            return fraction;
        }

        private static float ClampPercentToFraction(float percentPoints, float minPercent, float maxPercent)
        {
            float p = percentPoints;
            if (p < minPercent)
                p = minPercent;
            if (p > maxPercent)
                p = maxPercent;
            return p / 100f;
        }

        [SettingPropertyInteger("Tier 0", 0, 2147483647, "0", Order = 0, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier0 { get; set; }

        [SettingPropertyInteger("Tier 1", 0, 2147483647, "0", Order = 1, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier1 { get; set; }

        [SettingPropertyInteger("Tier 2", 0, 2147483647, "0", Order = 2, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier2 { get; set; }

        [SettingPropertyInteger("Tier 3", 0, 2147483647, "0", Order = 3, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier3 { get; set; }

        [SettingPropertyInteger("Tier 4", 0, 2147483647, "0", Order = 4, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier4 { get; set; }

        [SettingPropertyInteger("Tier 5", 0, 2147483647, "0", Order = 5, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier5 { get; set; }

        [SettingPropertyInteger("Tier 6", 0, 2147483647, "0", Order = 6, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier6 { get; set; }

        [SettingPropertyInteger("Tier 7", 0, 2147483647, "0", Order = 7, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier7 { get; set; }

        [SettingPropertyInteger("Tier 8", 0, 2147483647, "0", Order = 8, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier8 { get; set; }

        [SettingPropertyInteger("Tier 9", 0, 2147483647, "0", Order = 9, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier9 { get; set; }

        [SettingPropertyInteger("Tier 10", 0, 2147483647, "0", Order = 10, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier10 { get; set; }

        [SettingPropertyInteger("Tier 11", 0, 2147483647, "0", Order = 11, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier11 { get; set; }

        [SettingPropertyInteger("Tier 12", 0, 2147483647, "0", Order = 12, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier12 { get; set; }

        [SettingPropertyInteger("Tier 13", 0, 2147483647, "0", Order = 13, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier13 { get; set; }

        [SettingPropertyInteger("Tier 14", 0, 2147483647, "0", Order = 14, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier14 { get; set; }

        [SettingPropertyInteger("Tier 15", 0, 2147483647, "0", Order = 15, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier15 { get; set; }

        [SettingPropertyInteger("Tier 16", 0, 2147483647, "0", Order = 16, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier16 { get; set; }

        [SettingPropertyInteger("Tier 17", 0, 2147483647, "0", Order = 17, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier17 { get; set; }

        [SettingPropertyInteger("Tier 18", 0, 2147483647, "0", Order = 18, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier18 { get; set; }

        [SettingPropertyInteger("Tier 19", 0, 2147483647, "0", Order = 19, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier19 { get; set; }

        [SettingPropertyInteger("Tier 20", 0, 2147483647, "0", Order = 20, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier20 { get; set; }

        [SettingPropertyInteger("Tier 21", 0, 2147483647, "0", Order = 21, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier21 { get; set; }

        [SettingPropertyInteger("Tier 22", 0, 2147483647, "0", Order = 22, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier22 { get; set; }

        [SettingPropertyInteger("Tier 23", 0, 2147483647, "0", Order = 23, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier23 { get; set; }

        [SettingPropertyInteger("Tier 24", 0, 2147483647, "0", Order = 24, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier24 { get; set; }

        [SettingPropertyInteger("Tier 25", 0, 2147483647, "0", Order = 25, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier25 { get; set; }

        [SettingPropertyInteger("Tier 26", 0, 2147483647, "0", Order = 26, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier26 { get; set; }

        [SettingPropertyInteger("Tier 27", 0, 2147483647, "0", Order = 27, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier27 { get; set; }

        [SettingPropertyInteger("Tier 28", 0, 2147483647, "0", Order = 28, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier28 { get; set; }

        [SettingPropertyInteger("Tier 29", 0, 2147483647, "0", Order = 29, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier29 { get; set; }

        [SettingPropertyInteger("Tier 30", 0, 2147483647, "0", Order = 30, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier30 { get; set; }

        [SettingPropertyInteger("Tier 31", 0, 2147483647, "0", Order = 31, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier31 { get; set; }

        [SettingPropertyInteger("Tier 32", 0, 2147483647, "0", Order = 32, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier32 { get; set; }

        [SettingPropertyInteger("Tier 33", 0, 2147483647, "0", Order = 33, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier33 { get; set; }

        [SettingPropertyInteger("Tier 34", 0, 2147483647, "0", Order = 34, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier34 { get; set; }

        [SettingPropertyInteger("Tier 35", 0, 2147483647, "0", Order = 35, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier35 { get; set; }

        [SettingPropertyInteger("Tier 36", 0, 2147483647, "0", Order = 36, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier36 { get; set; }

        [SettingPropertyInteger("Tier 37", 0, 2147483647, "0", Order = 37, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier37 { get; set; }

        [SettingPropertyInteger("Tier 38", 0, 2147483647, "0", Order = 38, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier38 { get; set; }

        [SettingPropertyInteger("Tier 39", 0, 2147483647, "0", Order = 39, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier39 { get; set; }

        [SettingPropertyInteger("Tier 40", 0, 2147483647, "0", Order = 40, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier40 { get; set; }

        [SettingPropertyInteger("Tier 41", 0, 2147483647, "0", Order = 41, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier41 { get; set; }

        [SettingPropertyInteger("Tier 42", 0, 2147483647, "0", Order = 42, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier42 { get; set; }

        [SettingPropertyInteger("Tier 43", 0, 2147483647, "0", Order = 43, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier43 { get; set; }

        [SettingPropertyInteger("Tier 44", 0, 2147483647, "0", Order = 44, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier44 { get; set; }

        [SettingPropertyInteger("Tier 45", 0, 2147483647, "0", Order = 45, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier45 { get; set; }

        [SettingPropertyInteger("Tier 46", 0, 2147483647, "0", Order = 46, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier46 { get; set; }

        [SettingPropertyInteger("Tier 47", 0, 2147483647, "0", Order = 47, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier47 { get; set; }

        [SettingPropertyInteger("Tier 48", 0, 2147483647, "0", Order = 48, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier48 { get; set; }

        [SettingPropertyInteger("Tier 49", 0, 2147483647, "0", Order = 49, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier49 { get; set; }

        [SettingPropertyInteger("Tier 50", 0, 2147483647, "0", Order = 50, RequireRestart = false, HintText = "Renown needed for this tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier50 { get; set; }

        public Settings()
        {
            Enabled = true;
            MaxClanTier = 12;
            ExtraPartiesAtHighTiers = true;
            MaxCaravanPartySize = DefaultCaravanPartySizeCap;
            _useManualRenown = false;
            RenownMultiplier = 1f;
            GrowthPerTier = 2.41f;
            RenownLossFromDefeats = true;
            AffectPlayerClan = true;
            AffectAiClans = true;
            PlayerClanLossScale = 100f;
            PlayerKingdomClanLossScale = 100f;
            OtherAiClanLossScale = 100f;
            BaseLossPercent = 2f;
            MaximumLossPercentPerBattle = 10f;
            MinimumClanTierForDecline = 1;
            ClanLeaderCaptureMultiplier = 125f;
            NotifyPlayerRenownLoss = true;
            RenownLossFromLostFiefs = true;
            CastleLossPercent = 2f;
            TownLossPercent = 4f;
            CountRebellionFiefLosses = true;
            RebellionLossMultiplier = 100f;
            CountLeaveFactionFiefLosses = false;
            RenownLossFromExecutions = false;
            LowPrestigeRulerChallenge = true;
            RulerChallengeTierThreshold = 2;
            RulerChallengeDaysBelowThreshold = 3;
            RulerChallengeCooldownDays = 60;
            IncumbentMeritMultiplier = 45f;
            RulerChallengeSupportInfluenceMultiplier = 150f;
            Tier0 = 0;
            Tier1 = 50;
            Tier2 = 150;
            Tier3 = 350;
            Tier4 = 900;
            Tier5 = 2350;
            Tier6 = 6150;
            Tier7 = 16100;
            Tier8 = 42150;
            Tier9 = 110350;
            Tier10 = 262850;
            Tier11 = 636050;
            Tier12 = 1534950;
            // Strictly increasing geometric fill from tier 12 through int.MaxValue at tier 50.
            Tier13 = 1857289;
            Tier14 = 2247318;
            Tier15 = 2719253;
            Tier16 = 3290295;
            Tier17 = 3981255;
            Tier18 = 4817316;
            Tier19 = 5828949;
            Tier20 = 7053025;
            Tier21 = 8534156;
            Tier22 = 10326323;
            Tier23 = 12494845;
            Tier24 = 15118754;
            Tier25 = 18293683;
            Tier26 = 22135346;
            Tier27 = 26783755;
            Tier28 = 32408326;
            Tier29 = 39214055;
            Tier30 = 47448982;
            Tier31 = 57413239;
            Tier32 = 69469984;
            Tier33 = 84058637;
            Tier34 = 101710899;
            Tier35 = 123070125;
            Tier36 = 148914775;
            Tier37 = 180186786;
            Tier38 = 218025899;
            Tier39 = 263811203;
            Tier40 = 319211393;
            Tier41 = 386245588;
            Tier42 = 467356922;
            Tier43 = 565501587;
            Tier44 = 684256570;
            Tier45 = 827950026;
            Tier46 = 1001819020;
            Tier47 = 1212200394;
            Tier48 = 1466761726;
            Tier49 = 1774780781;
            Tier50 = 2147483647;
            InitDynastyBonusDefaults();
        }

        public int GetConfiguredMaxTier()
        {
            int max = MaxClanTier;
            if (max < MinConfigurableMaxTier)
                return MinConfigurableMaxTier;
            if (max > AbsoluteMaxTier)
                return AbsoluteMaxTier;
            return max;
        }

        // Read by CaravanPartyLimitIncreasedFixed (soft link). Clamped hard cap for caravan troop size.
        // Returns 0 when this mod is disabled so CPI does not apply a stale cap.
        public int GetCaravanPartySizeCap()
        {
            if (!Enabled)
                return 0;

            int cap = MaxCaravanPartySize;
            if (cap < MinCaravanPartySizeCap)
                return MinCaravanPartySizeCap;
            if (cap > MaxCaravanPartySizeCap)
                return MaxCaravanPartySizeCap;
            return cap;
        }

        public bool IsManualRenown()
        {
            return _useManualRenown;
        }

        public int[] GetRenownLimits()
        {
            int max = GetConfiguredMaxTier();
            int[] limits = new int[max + 1];
            if (IsManualRenown())
            {
                int[] manual = GetManualTier();
                for (int i = 0; i <= max; i++)
                    limits[i] = i < manual.Length ? Math.Max(0, manual[i]) : limits[i - 1];
            }
            else
            {
                BuildFormulaLimits(limits);
            }

            // Keep thresholds strictly increasing when possible so every tier has a real renown band.
            EnforceStrictlyIncreasingLimits(limits);
            return limits;
        }

        private void BuildFormulaLimits(int[] limits)
        {
            float multiplier = RenownMultiplier;
            if (multiplier < 0.01f)
                multiplier = 0.01f;

            float growth = GrowthPerTier;
            if (growth < 1.01f)
                growth = 1.01f;

            // Build the full 0..50 curve first so raising max tier later keeps the same path.
            int fullLength = 50 + 1;
            int[] full = new int[fullLength];
            BuildStrictFormulaCurve(full, multiplier, growth);
            for (int tier = 0; tier < limits.Length; tier++)
                limits[tier] = full[tier];
        }

        // Seed tiers 0-12 keep the classic curve. After that, prefer GrowthPerTier but never consume
        // headroom needed for unique thresholds through AbsoluteMaxTiers (50) inside int.MaxValue.
        private static void BuildStrictFormulaCurve(int[] limits, float multiplier, float growth)
        {
            long previousScaled = 0;
            for (int tier = 0; tier < limits.Length; tier++)
            {
                long scaled;
                if (tier == 0)
                {
                    scaled = 0L;
                }
                else if (tier < FormulaSeed.Length)
                {
                    scaled = (long)Math.Round((double)FormulaSeed[tier] * (double)multiplier);
                }
                else
                {
                    int remainingIncluding = limits.Length - tier;
                    int reserveGap = 1;
                    if (previousScaled > 16777216L)
                        reserveGap = Math.Max(1, (int)Math.Ceiling((double)previousScaled / 8388608.0));
                    long maxAllowed = (long)int.MaxValue - (long)(remainingIncluding - 1) * (long)reserveGap;
                    long minNextAllowed = GetMinFloatDistinguishableThreshold((int)Math.Min(previousScaled, (long)int.MaxValue));
                    if (maxAllowed < minNextAllowed)
                        maxAllowed = minNextAllowed;
                    if (maxAllowed > int.MaxValue)
                        maxAllowed = int.MaxValue;

                    long grown = (long)Math.Round((double)previousScaled * (double)growth);
                    long geometric = GetMinFloatDistinguishableThreshold((int)Math.Min(previousScaled, (long)int.MaxValue));
                    if (previousScaled > 0 && remainingIncluding > 0)
                    {
                        double ratio = Math.Pow((double)int.MaxValue / (double)previousScaled, 1.0 / (double)remainingIncluding);
                        geometric = (long)Math.Round((double)previousScaled * ratio);
                    }

                    // Never outrun the geometric fair share that keeps later tiers unique.
                    scaled = grown;
                    if (scaled > geometric)
                        scaled = geometric;
                    long minNext = GetMinFloatDistinguishableThreshold((int)Math.Min(previousScaled, (long)int.MaxValue));
                    if (scaled < minNext)
                        scaled = minNext;
                    if (scaled > maxAllowed)
                        scaled = maxAllowed;
                }

                if (scaled < 0)
                    scaled = 0;
                if (scaled > int.MaxValue)
                    scaled = int.MaxValue;
                if (tier > 0)
                {
                    long minNext = GetMinFloatDistinguishableThreshold((int)Math.Min(previousScaled, (long)int.MaxValue));
                    if (scaled < minNext)
                        scaled = minNext;
                }

                limits[tier] = (int)scaled;
                previousScaled = scaled;
            }
        }

        private static void EnforceStrictlyIncreasingLimits(int[] limits)
        {
            if (limits == null || limits.Length == 0)
                return;

            for (int i = 1; i < limits.Length; i++)
            {
                int minNext = GetMinFloatDistinguishableThreshold(limits[i - 1]);
                if ((float)limits[i] > (float)limits[i - 1] && limits[i] >= minNext)
                    continue;
                if ((float)minNext > (float)limits[i - 1])
                    limits[i] = minNext;
            }

            // Forward bumps cannot invent a float band once the previous tier already sits on the last
            // representable value. Pull earlier thresholds down, or flatten the ladder at the last real band.
            bool adjusted = false;
            for (int i = limits.Length - 1; i >= 1; i--)
            {
                if ((float)limits[i] > (float)limits[i - 1])
                    continue;

                int lowered = GreatestIntWithSmallerFloat(limits[i]);
                if (lowered < limits[i] && (float)lowered < (float)limits[i])
                {
                    limits[i - 1] = lowered;
                    adjusted = true;
                    continue;
                }

                for (int j = i; j < limits.Length; j++)
                    limits[j] = limits[i - 1];
                adjusted = true;
                break;
            }

            if (!adjusted)
                return;

            const string warning = "[ClanTierExpanded] Renown thresholds were adjusted so each tier stays float-distinguishable. Tiers that cannot fit below int.MaxValue share the last representable threshold.";
            if (_lastThresholdAdjustmentWarning == warning)
                return;
            _lastThresholdAdjustmentWarning = warning;
            Debug.Print(warning);
        }

        // Clan.Renown is float; near 2e9 consecutive ints are not distinguishable. Require at least one ULP.
        // Returns previous when no greater int converts to a strictly greater float.
        internal static int GetMinFloatDistinguishableThreshold(int previous)
        {
            if (previous >= int.MaxValue)
                return int.MaxValue;
            if (previous <= 0)
                return 1;

            // Single-precision ULP around |previous| is about previous / 2^23.
            double ulp = (double)previous / 8388608.0;
            int gap = (int)Math.Ceiling(ulp);
            if (gap < 1)
                gap = 1;

            long next = (long)previous + (long)gap;
            if (next > int.MaxValue)
                next = int.MaxValue;
            int candidate = (int)next;
            if ((float)candidate > (float)previous)
                return candidate;

            int low = previous < int.MaxValue ? previous + 1 : int.MaxValue;
            int high = int.MaxValue;
            int best = previous;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if ((float)mid > (float)previous)
                {
                    best = mid;
                    if (mid == low)
                        break;
                    high = mid - 1;
                }
                else if (mid == int.MaxValue)
                {
                    break;
                }
                else
                {
                    low = mid + 1;
                }
            }
            return best;
        }

        // Largest int that converts to a float strictly below successor's float. 0 when none exists.
        private static int GreatestIntWithSmallerFloat(int successor)
        {
            float ceiling = (float)successor;
            if (successor <= 0 || ceiling <= 0f)
                return 0;

            int low = 0;
            int high = successor - 1;
            int best = 0;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if ((float)mid < ceiling)
                {
                    best = mid;
                    if (mid == int.MaxValue)
                        break;
                    low = mid + 1;
                }
                else if (mid == 0)
                {
                    break;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return best;
        }

        private int[] GetManualTier()
        {
            return new int[] { Tier0, Tier1, Tier2, Tier3, Tier4, Tier5, Tier6, Tier7, Tier8, Tier9, Tier10, Tier11, Tier12, Tier13, Tier14, Tier15, Tier16, Tier17, Tier18, Tier19, Tier20, Tier21, Tier22, Tier23, Tier24, Tier25, Tier26, Tier27, Tier28, Tier29, Tier30, Tier31, Tier32, Tier33, Tier34, Tier35, Tier36, Tier37, Tier38, Tier39, Tier40, Tier41, Tier42, Tier43, Tier44, Tier45, Tier46, Tier47, Tier48, Tier49, Tier50 };
        }
    }
}
