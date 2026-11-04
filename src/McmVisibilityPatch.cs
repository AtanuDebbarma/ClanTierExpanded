using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions;
using MCM.Abstractions.Base;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Attribute MCM cannot show/hide groups by mode on its own. Filter discovered
    // properties so Formula vs Manual (and Caravan support) only appear when relevant.
    [HarmonyPatch]
    internal static class McmVisibilityPatch
    {
        static bool Prepare()
        {
            return AccessTools.TypeByName("MCM.Implementation.AttributeSettingsPropertyDiscoverer") != null;
        }

        static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("MCM.Implementation.AttributeSettingsPropertyDiscoverer");
            if (type == null)
                return null;

            MethodInfo method = AccessTools.Method(type, "GetProperties", new Type[] { typeof(BaseSettings) });
            if (method == null)
                method = AccessTools.Method(type, "GetProperties");
            return method;
        }

        static void Postfix(BaseSettings settings, ref IEnumerable<ISettingsPropertyDefinition> __result)
        {
            try
            {
                if (settings == null || settings.Id == null || settings.Id.IndexOf("ClanTierExpanded") < 0)
                    return;
                if (__result == null)
                    return;

                Settings cct = settings as Settings;
                if (cct == null)
                    cct = Settings.Instance;

                bool manual = cct != null && cct.IsManualRenown();
                bool showCaravanSupport = ModuleDetect.CaravanPartyLimitIncreasedSelected();

                List<ISettingsPropertyDefinition> kept = new List<ISettingsPropertyDefinition>();
                foreach (ISettingsPropertyDefinition definition in __result)
                {
                    if (definition == null)
                        continue;

                    string group = GetGroupName(definition);

                    if (!showCaravanSupport && IsCaravanSupportGroup(group))
                        continue;
                    if (manual && IsFormulaGroup(group))
                        continue;
                    if (!manual && IsManualValuesGroup(group))
                        continue;

                    kept.Add(definition);
                }

                __result = kept;
            }
            catch (Exception e)
            {
                Debug.Print("[ClanTierExpanded] MCM visibility filter skipped: " + e.Message);
            }
        }

        private static string GetGroupName(ISettingsPropertyDefinition definition)
        {
            string groupName = null;
            try
            {
                groupName = definition.GroupName;
            }
            catch
            {
            }

            try
            {
                PropertyInfo raw = definition.GetType().GetProperty(
                    "GroupNameRaw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (raw != null)
                {
                    string rawName = raw.GetValue(definition, null) as string;
                    if (!string.IsNullOrEmpty(rawName))
                    {
                        if (string.IsNullOrEmpty(groupName))
                            groupName = rawName;
                        else
                            groupName = groupName + "|" + rawName;
                    }
                }
            }
            catch
            {
            }

            return groupName ?? string.Empty;
        }

        private static bool IsCaravanSupportGroup(string group)
        {
            return group.IndexOf(Settings.CaravanSupportGroupLeaf) >= 0;
        }

        private static bool IsFormulaGroup(string group)
        {
            return group.IndexOf("Renown/" + Settings.RenownFormulaGroupLeaf) >= 0
                || group.IndexOf("/" + Settings.RenownFormulaGroupLeaf) >= 0;
        }

        private static bool IsManualValuesGroup(string group)
        {
            return group.IndexOf(Settings.RenownManualGroupLeaf) >= 0;
        }

    }
}
