using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions;
using MCM.Abstractions.Base;
using TaleWorlds.Library;

namespace ClanTierExpanded
{
    // Attribute MCM cannot show/hide groups by mode on its own. Filter discovered
    // properties so Formula vs Manual (and Caravan support) only appear when relevant
    // in the options UI.
    //
    // MCM json2 save/load walks the same GetProperties list via
    // BaseSettingsJsonConverter. Filtering Manual tiers while UseManualRenown is still
    // the constructor default (false) dropped Tier0-Tier50 from deserialize, so manual
    // renown never persisted and hand-edited JSON was ignored. Skip filtering during
    // persistence/copy so every setting property always round-trips.
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

                // Save/load/presets must see every property. UI-only hiding comes after.
                if (IsSettingsPersistenceCall())
                    return;

                Settings cct = settings as Settings;
                if (cct == null)
                    cct = Settings.Instance;

                bool manual = cct != null && cct.IsManualRenown();
                bool showCaravanSupport = ModuleDetect.CaravanPartyLimitIncreasedSelected();
                bool dynastyOn = cct != null && cct.HighTierDynastyBonuses;

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
                    // Keep the master dynasty toggle visible; hide child groups while off.
                    if (!dynastyOn && IsDynastyChildGroup(group))
                        continue;

                    kept.Add(definition);
                }

                __result = kept;
            }
            catch (Exception e)
            {
                TaleWorlds.Library.Debug.Print("[ClanTierExpanded] MCM visibility filter skipped: " + e.Message);
            }
        }

        private static bool IsSettingsPersistenceCall()
        {
            try
            {
                StackTrace trace = new StackTrace();
                int limit = trace.FrameCount;
                if (limit > 28)
                    limit = 28;

                for (int i = 2; i < limit; i++)
                {
                    MethodBase method = trace.GetFrame(i).GetMethod();
                    if (method == null)
                        continue;

                    Type type = method.DeclaringType;
                    if (type == null)
                        continue;

                    string typeName = type.FullName;
                    if (string.IsNullOrEmpty(typeName))
                        typeName = type.Name;

                    if (typeName.IndexOf("BaseSettingsJsonConverter") >= 0)
                        return true;
                    if (typeName.IndexOf("JsonSettingsFormat") >= 0)
                        return true;
                    if (typeName.IndexOf("XmlSettingsFormat") >= 0)
                        return true;
                    if (typeName.IndexOf("SettingsUtils") >= 0)
                        return true;
                    if (typeName.IndexOf("SettingsPreset") >= 0)
                        return true;

                    string methodName = method.Name;
                    if (methodName == "ReadJson" || methodName == "WriteJson"
                        || methodName == "Save" || methodName == "Load"
                        || methodName == "SaveSettings" || methodName == "LoadSettings"
                        || methodName == "OverrideSettings")
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
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

        private static bool IsDynastyChildGroup(string group)
        {
            if (group.IndexOf(Settings.DynastyBonusesGroupLeaf) < 0)
                return false;
            // Child leaves under High-tier dynasty bonuses (not the master group alone).
            return group.IndexOf("Logistics") >= 0
                || group.IndexOf("Court") >= 0
                || group.IndexOf("Reputation") >= 0
                || group.IndexOf("Trade house") >= 0
                || group.IndexOf("Milestones") >= 0;
        }
    }
}
