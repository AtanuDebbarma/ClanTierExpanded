using TaleWorlds.SaveSystem;

namespace ClanTierExpanded
{
    // Required so unresolved LowPrestigeKingSelectionDecision instances survive save/load.
    public sealed class CteSaveableTypeDefiner : SaveableTypeDefiner
    {
        // Unique base id for Clan Tier Expanded saveable types.
        public CteSaveableTypeDefiner()
            : base(215480000)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(LowPrestigeKingSelectionDecision), 1);
        }
    }
}
