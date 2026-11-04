using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ClanTierExpanded
{
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        public const int MinConfigurableMaxTier = 6;
        public const int AbsoluteMaxTier = 50;

        // Friendly leaf names; McmVisibilityPatch matches these strings (no raw marker path segments).
        internal const string CaravanSupportGroupLeaf = "CaravanPartyLimitIncreasedFixed (Mod Support)";
        internal const string RenownFormulaGroupLeaf = "Formula";
        internal const string RenownManualGroupLeaf = "Manual values";

        private const string CaravanSupportGroupName = CaravanSupportGroupLeaf;
        private const string RenownFormulaGroupName = RenownFormulaGroupLeaf;
        private const string RenownManualGroupName = RenownManualGroupLeaf;

        public const int MinCaravanPartySizeCap = 50;
        public const int MaxCaravanPartySizeCap = 1000;
        public const int DefaultCaravanPartySizeCap = 500;

        private bool _useManualRenown;
        private int _uiVersion = 1;

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

        [SettingPropertyBool("Enabled", IsToggle = true, Order = 0, RequireRestart = false, HintText = "Use custom clan tiers and renown. Turn off and save before removing this mod so tiers return to vanilla.")]
        [SettingPropertyGroup("Enabled", GroupOrder = 0)]
        public bool Enabled { get; set; }

        [SettingPropertyInteger("Max clan tier", MinConfigurableMaxTier, AbsoluteMaxTier, "0", Order = 1, RequireRestart = false, HintText = "Highest clan tier. Vanilla is 6. Default 12.")]
        [SettingPropertyGroup("Enabled")]
        public int MaxClanTier { get; set; }

        [SettingPropertyBool("Extra parties at high tiers", Order = 2, RequireRestart = false, HintText = "+1 party slot at tiers 7, 9, and 11. Off while Companion & Party Limits Expanded is loaded.")]
        [SettingPropertyGroup("Enabled")]
        public bool ExtraPartiesAtHighTiers { get; set; }

        [SettingPropertyInteger("Max caravan party size", MinCaravanPartySizeCap, MaxCaravanPartySizeCap, "0", Order = 0, RequireRestart = false, HintText = "Hard cap for Caravan Party Limit Increased Fixed. Default 500. Shown only when that mod is enabled.")]
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

        [SettingPropertyFloatingInteger("Renown multiplier", 0.25f, 5f, "0.00", Order = 1, RequireRestart = false, HintText = "Scales formula renown. Default 1.00.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownFormulaGroupName)]
        public float RenownMultiplier { get; set; }

        [SettingPropertyFloatingInteger("Growth per tier", 1.5f, 4f, "0.00", Order = 2, RequireRestart = false, HintText = "How fast formula renown rises after tier 12. Default 2.41.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownFormulaGroupName)]
        public float GrowthPerTier { get; set; }

        [SettingPropertyInteger("Tier 0", 0, 2000000000, "0", Order = 0, RequireRestart = false, HintText = "Renown needed for tier 0. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier0 { get; set; }

        [SettingPropertyInteger("Tier 1", 0, 2000000000, "0", Order = 1, RequireRestart = false, HintText = "Renown needed for tier 1. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier1 { get; set; }

        [SettingPropertyInteger("Tier 2", 0, 2000000000, "0", Order = 2, RequireRestart = false, HintText = "Renown needed for tier 2. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier2 { get; set; }

        [SettingPropertyInteger("Tier 3", 0, 2000000000, "0", Order = 3, RequireRestart = false, HintText = "Renown needed for tier 3. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier3 { get; set; }

        [SettingPropertyInteger("Tier 4", 0, 2000000000, "0", Order = 4, RequireRestart = false, HintText = "Renown needed for tier 4. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier4 { get; set; }

        [SettingPropertyInteger("Tier 5", 0, 2000000000, "0", Order = 5, RequireRestart = false, HintText = "Renown needed for tier 5. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier5 { get; set; }

        [SettingPropertyInteger("Tier 6", 0, 2000000000, "0", Order = 6, RequireRestart = false, HintText = "Renown needed for tier 6. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier6 { get; set; }

        [SettingPropertyInteger("Tier 7", 0, 2000000000, "0", Order = 7, RequireRestart = false, HintText = "Renown needed for tier 7. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier7 { get; set; }

        [SettingPropertyInteger("Tier 8", 0, 2000000000, "0", Order = 8, RequireRestart = false, HintText = "Renown needed for tier 8. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier8 { get; set; }

        [SettingPropertyInteger("Tier 9", 0, 2000000000, "0", Order = 9, RequireRestart = false, HintText = "Renown needed for tier 9. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier9 { get; set; }

        [SettingPropertyInteger("Tier 10", 0, 2000000000, "0", Order = 10, RequireRestart = false, HintText = "Renown needed for tier 10. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier10 { get; set; }

        [SettingPropertyInteger("Tier 11", 0, 2000000000, "0", Order = 11, RequireRestart = false, HintText = "Renown needed for tier 11. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier11 { get; set; }

        [SettingPropertyInteger("Tier 12", 0, 2000000000, "0", Order = 12, RequireRestart = false, HintText = "Renown needed for tier 12. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier12 { get; set; }

        [SettingPropertyInteger("Tier 13", 0, 2000000000, "0", Order = 13, RequireRestart = false, HintText = "Renown needed for tier 13. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier13 { get; set; }

        [SettingPropertyInteger("Tier 14", 0, 2000000000, "0", Order = 14, RequireRestart = false, HintText = "Renown needed for tier 14. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier14 { get; set; }

        [SettingPropertyInteger("Tier 15", 0, 2000000000, "0", Order = 15, RequireRestart = false, HintText = "Renown needed for tier 15. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier15 { get; set; }

        [SettingPropertyInteger("Tier 16", 0, 2000000000, "0", Order = 16, RequireRestart = false, HintText = "Renown needed for tier 16. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier16 { get; set; }

        [SettingPropertyInteger("Tier 17", 0, 2000000000, "0", Order = 17, RequireRestart = false, HintText = "Renown needed for tier 17. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier17 { get; set; }

        [SettingPropertyInteger("Tier 18", 0, 2000000000, "0", Order = 18, RequireRestart = false, HintText = "Renown needed for tier 18. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier18 { get; set; }

        [SettingPropertyInteger("Tier 19", 0, 2000000000, "0", Order = 19, RequireRestart = false, HintText = "Renown needed for tier 19. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier19 { get; set; }

        [SettingPropertyInteger("Tier 20", 0, 2000000000, "0", Order = 20, RequireRestart = false, HintText = "Renown needed for tier 20. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier20 { get; set; }

        [SettingPropertyInteger("Tier 21", 0, 2000000000, "0", Order = 21, RequireRestart = false, HintText = "Renown needed for tier 21. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier21 { get; set; }

        [SettingPropertyInteger("Tier 22", 0, 2000000000, "0", Order = 22, RequireRestart = false, HintText = "Renown needed for tier 22. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier22 { get; set; }

        [SettingPropertyInteger("Tier 23", 0, 2000000000, "0", Order = 23, RequireRestart = false, HintText = "Renown needed for tier 23. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier23 { get; set; }

        [SettingPropertyInteger("Tier 24", 0, 2000000000, "0", Order = 24, RequireRestart = false, HintText = "Renown needed for tier 24. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier24 { get; set; }

        [SettingPropertyInteger("Tier 25", 0, 2000000000, "0", Order = 25, RequireRestart = false, HintText = "Renown needed for tier 25. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier25 { get; set; }

        [SettingPropertyInteger("Tier 26", 0, 2000000000, "0", Order = 26, RequireRestart = false, HintText = "Renown needed for tier 26. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier26 { get; set; }

        [SettingPropertyInteger("Tier 27", 0, 2000000000, "0", Order = 27, RequireRestart = false, HintText = "Renown needed for tier 27. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier27 { get; set; }

        [SettingPropertyInteger("Tier 28", 0, 2000000000, "0", Order = 28, RequireRestart = false, HintText = "Renown needed for tier 28. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier28 { get; set; }

        [SettingPropertyInteger("Tier 29", 0, 2000000000, "0", Order = 29, RequireRestart = false, HintText = "Renown needed for tier 29. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier29 { get; set; }

        [SettingPropertyInteger("Tier 30", 0, 2000000000, "0", Order = 30, RequireRestart = false, HintText = "Renown needed for tier 30. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier30 { get; set; }

        [SettingPropertyInteger("Tier 31", 0, 2000000000, "0", Order = 31, RequireRestart = false, HintText = "Renown needed for tier 31. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier31 { get; set; }

        [SettingPropertyInteger("Tier 32", 0, 2000000000, "0", Order = 32, RequireRestart = false, HintText = "Renown needed for tier 32. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier32 { get; set; }

        [SettingPropertyInteger("Tier 33", 0, 2000000000, "0", Order = 33, RequireRestart = false, HintText = "Renown needed for tier 33. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier33 { get; set; }

        [SettingPropertyInteger("Tier 34", 0, 2000000000, "0", Order = 34, RequireRestart = false, HintText = "Renown needed for tier 34. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier34 { get; set; }

        [SettingPropertyInteger("Tier 35", 0, 2000000000, "0", Order = 35, RequireRestart = false, HintText = "Renown needed for tier 35. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier35 { get; set; }

        [SettingPropertyInteger("Tier 36", 0, 2000000000, "0", Order = 36, RequireRestart = false, HintText = "Renown needed for tier 36. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier36 { get; set; }

        [SettingPropertyInteger("Tier 37", 0, 2000000000, "0", Order = 37, RequireRestart = false, HintText = "Renown needed for tier 37. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier37 { get; set; }

        [SettingPropertyInteger("Tier 38", 0, 2000000000, "0", Order = 38, RequireRestart = false, HintText = "Renown needed for tier 38. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier38 { get; set; }

        [SettingPropertyInteger("Tier 39", 0, 2000000000, "0", Order = 39, RequireRestart = false, HintText = "Renown needed for tier 39. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier39 { get; set; }

        [SettingPropertyInteger("Tier 40", 0, 2000000000, "0", Order = 40, RequireRestart = false, HintText = "Renown needed for tier 40. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier40 { get; set; }

        [SettingPropertyInteger("Tier 41", 0, 2000000000, "0", Order = 41, RequireRestart = false, HintText = "Renown needed for tier 41. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier41 { get; set; }

        [SettingPropertyInteger("Tier 42", 0, 2000000000, "0", Order = 42, RequireRestart = false, HintText = "Renown needed for tier 42. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier42 { get; set; }

        [SettingPropertyInteger("Tier 43", 0, 2000000000, "0", Order = 43, RequireRestart = false, HintText = "Renown needed for tier 43. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier43 { get; set; }

        [SettingPropertyInteger("Tier 44", 0, 2000000000, "0", Order = 44, RequireRestart = false, HintText = "Renown needed for tier 44. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier44 { get; set; }

        [SettingPropertyInteger("Tier 45", 0, 2000000000, "0", Order = 45, RequireRestart = false, HintText = "Renown needed for tier 45. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier45 { get; set; }

        [SettingPropertyInteger("Tier 46", 0, 2000000000, "0", Order = 46, RequireRestart = false, HintText = "Renown needed for tier 46. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier46 { get; set; }

        [SettingPropertyInteger("Tier 47", 0, 2000000000, "0", Order = 47, RequireRestart = false, HintText = "Renown needed for tier 47. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier47 { get; set; }

        [SettingPropertyInteger("Tier 48", 0, 2000000000, "0", Order = 48, RequireRestart = false, HintText = "Renown needed for tier 48. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier48 { get; set; }

        [SettingPropertyInteger("Tier 49", 0, 2000000000, "0", Order = 49, RequireRestart = false, HintText = "Renown needed for tier 49. Ignored above your max clan tier.")]
        [SettingPropertyGroup("Enabled/Renown/" + RenownManualGroupName)]
        public int Tier49 { get; set; }

        [SettingPropertyInteger("Tier 50", 0, 2000000000, "0", Order = 50, RequireRestart = false, HintText = "Renown needed for tier 50. Ignored above your max clan tier.")]
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
            Tier13 = 3703834;
            Tier14 = 8937351;
            Tier15 = 21565828;
            Tier16 = 52038343;
            Tier17 = 125568522;
            Tier18 = 302996844;
            Tier19 = 731131385;
            Tier20 = 1764220032;
            Tier21 = 2000000000;
            Tier22 = 2000000000;
            Tier23 = 2000000000;
            Tier24 = 2000000000;
            Tier25 = 2000000000;
            Tier26 = 2000000000;
            Tier27 = 2000000000;
            Tier28 = 2000000000;
            Tier29 = 2000000000;
            Tier30 = 2000000000;
            Tier31 = 2000000000;
            Tier32 = 2000000000;
            Tier33 = 2000000000;
            Tier34 = 2000000000;
            Tier35 = 2000000000;
            Tier36 = 2000000000;
            Tier37 = 2000000000;
            Tier38 = 2000000000;
            Tier39 = 2000000000;
            Tier40 = 2000000000;
            Tier41 = 2000000000;
            Tier42 = 2000000000;
            Tier43 = 2000000000;
            Tier44 = 2000000000;
            Tier45 = 2000000000;
            Tier46 = 2000000000;
            Tier47 = 2000000000;
            Tier48 = 2000000000;
            Tier49 = 2000000000;
            Tier50 = 2000000000;
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

            // Keep thresholds non-decreasing so higher tiers never require less renown.
            for (int i = 1; i < limits.Length; i++)
            {
                if (limits[i] < limits[i - 1])
                    limits[i] = limits[i - 1];
            }
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

            long previous = 0;
            for (int tier = 0; tier < limits.Length; tier++)
            {
                long raw;
                if (tier < FormulaSeed.Length)
                    raw = FormulaSeed[tier];
                else
                    raw = (long)Math.Round(previous * growth);

                if (raw < previous)
                    raw = previous;

                long scaled = tier == 0 ? 0L : (long)Math.Round(raw * multiplier);
                if (scaled > int.MaxValue)
                    scaled = int.MaxValue;
                if (scaled < 0)
                    scaled = 0;

                limits[tier] = (int)scaled;
                previous = raw;
            }
        }

        private int[] GetManualTier()
        {
            return new int[] { Tier0, Tier1, Tier2, Tier3, Tier4, Tier5, Tier6, Tier7, Tier8, Tier9, Tier10, Tier11, Tier12, Tier13, Tier14, Tier15, Tier16, Tier17, Tier18, Tier19, Tier20, Tier21, Tier22, Tier23, Tier24, Tier25, Tier26, Tier27, Tier28, Tier29, Tier30, Tier31, Tier32, Tier33, Tier34, Tier35, Tier36, Tier37, Tier38, Tier39, Tier40, Tier41, Tier42, Tier43, Tier44, Tier45, Tier46, Tier47, Tier48, Tier49, Tier50 };
        }
    }
}
