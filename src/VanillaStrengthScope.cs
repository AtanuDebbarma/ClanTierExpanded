using System;
using System.Runtime.CompilerServices;

namespace ClanTierExpanded
{
    // While AI scores war/peace, clan join/leave, mercs, or packs conspiracy clans, party/companion
    // limits must follow vanilla tier caps so Extra Parties / high CTE tiers do not inflate those scores.
    // Actual fielded parties, armies, and MCM Extra Parties outside these scores are unchanged.
    internal static class VanillaStrengthScope
    {
        [ThreadStatic]
        private static int _depth;

        internal static bool IsActive
        {
            get { return _depth > 0; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Enter()
        {
            _depth++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Exit()
        {
            if (_depth > 0)
                _depth--;
        }
    }
}
