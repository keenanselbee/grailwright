using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

[assembly: AssemblyTitle("Full Enemy XP")]
[assembly: AssemblyDescription("Removes the enemy overlevel kill XP penalty in Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("Full Enemy XP")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

namespace FullEnemyXP
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FullEnemyXPPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.full-enemy-xp";
        public const string PluginName = "Full Enemy XP";
        public const string PluginVersion = "1.0.0";
        private const int ConfigSchemaVersion = 1;

        private const string NpcElementTypeName = "Awaken.TG.Main.Fights.NPCs.NpcElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const float Epsilon = 0.0001f;

        internal static FullEnemyXPPlugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private Type _heroType;
        private PropertyInfo _heroCurrentGetter;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _minimumOverlevelXpMultiplier;
        private ConfigEntry<bool> _dryRun;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _logAdjustedKills;
        private ConfigEntry<bool> _logUnchangedEligibleKills;
        private ConfigEntry<bool> _logSkippedDeathChecks;
        private ConfigEntry<int> _summaryEveryAdjustedKills;

        private long _eligibleKillXpAwardsSeen;
        private long _adjustedKillXpAwards;
        private long _dryRunAdjustments;
        private long _unchangedEligibleKillXpAwards;
        private long _skippedAdjustmentChecks;
        private double _estimatedExtraXpBeforeGlobal;
        private double _dryRunEstimatedExtraXpBeforeGlobal;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            CacheGameAccessors();
            PatchGame();

            Log.LogInfo(
                PluginName
                + " "
                + PluginVersion
                + " loaded. Enabled="
                + _enabled.Value.ToString(CultureInfo.InvariantCulture)
                + "; MinimumOverlevelXPMultiplier="
                + FormatFloat(_minimumOverlevelXpMultiplier.Value)
                + "; DryRun="
                + _dryRun.Value.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private void OnDestroy()
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                LogSummary("unload");
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. Do not edit manually; the plugin backs up stale configs and regenerates defaults when this changes.");
            _minimumOverlevelXpMultiplier = Config.Bind(
                "1. Core",
                "MinimumOverlevelXPMultiplier",
                1.0f,
                new ConfigDescription(
                    "Minimum enemy-level XP multiplier when the player is above the enemy XP level. 1 means full enemy XP; lower values allow partial vanilla falloff.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));
            _dryRun = Config.Bind("2. Diagnostics", "DryRun", false, "Log overlevel XP adjustments without changing the vanilla XP multiplier.");
            _diagnostics = Config.Bind("2. Diagnostics", "Diagnostics", false, "Log patch setup, adjusted kill XP, optional unchanged checks, and session summaries.");
            _logAdjustedKills = Config.Bind("2. Diagnostics", "LogAdjustedKills", true, "When Diagnostics is enabled, log each kill whose overlevel XP multiplier is raised or would be raised in DryRun.");
            _logUnchangedEligibleKills = Config.Bind("2. Diagnostics", "LogUnchangedEligibleKills", false, "When Diagnostics is enabled, log eligible kill XP awards that do not need adjustment.");
            _logSkippedDeathChecks = Config.Bind("2. Diagnostics", "LogSkippedDeathChecks", false, "When Diagnostics is enabled, log adjustment checks skipped because the mod is disabled or context was incomplete.");
            _summaryEveryAdjustedKills = Config.Bind(
                "2. Diagnostics",
                "SummaryEveryAdjustedKills",
                10,
                new ConfigDescription(
                    "When Diagnostics is enabled, log a session summary after this many adjusted or dry-run adjusted kills. Zero disables periodic summaries.",
                    new AcceptableValueRange<int>(0, 1000)));
            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (String.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = String.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((String.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || String.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    Int32.TryParse(
                        line.Substring(schemaPrefix.Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out storedSchemaVersion);
                    break;
                }
            }

            if (storedSchemaVersion == ConfigSchemaVersion)
            {
                return;
            }

            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, String.Empty);
                Config.Clear();
                Config.Reload();
                Log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, configPath, true);
                        Config.Clear();
                        Config.Reload();
                    }
                }
                catch (Exception restoreEx)
                {
                    Log.LogError("Failed to restore Full Enemy XP config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset Full Enemy XP config schema. Original config was left in place when possible.", ex);
            }
        }

        private void CacheGameAccessors()
        {
            _heroType = AccessTools.TypeByName(HeroTypeName);
            if (_heroType != null)
            {
                _heroCurrentGetter = AccessTools.Property(_heroType, "Current");
            }
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type npcElementType = AccessTools.TypeByName(NpcElementTypeName);
            if (npcElementType == null)
            {
                Log.LogError("Could not find " + NpcElementTypeName + ". Full Enemy XP is inactive.");
                return;
            }

            MethodInfo original = AccessTools.Method(npcElementType, "DeathNonCriticalFunctions");
            MethodInfo transpiler = AccessTools.Method(typeof(NpcDeathXpPatch), "Transpiler");
            if (original == null || transpiler == null)
            {
                Log.LogError("Could not patch NPC death XP handling. Full Enemy XP is inactive.");
                return;
            }

            _harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
            LogDiagnostic("Patched " + npcElementType.FullName + ".DeathNonCriticalFunctions.");
        }

        internal static float AdjustLevelMultiplier(object npc, float vanillaLevelMultiplier)
        {
            FullEnemyXPPlugin plugin = Instance;
            if (plugin == null)
            {
                return vanillaLevelMultiplier;
            }

            return plugin.AdjustLevelMultiplierInternal(npc, vanillaLevelMultiplier);
        }

        private float AdjustLevelMultiplierInternal(object npc, float vanillaLevelMultiplier)
        {
            if (_enabled == null || !_enabled.Value)
            {
                _skippedAdjustmentChecks++;
                if (_diagnostics.Value && _logSkippedDeathChecks.Value)
                {
                    Log.LogInfo("Skipped kill XP adjustment because Full Enemy XP is disabled.");
                }
                return vanillaLevelMultiplier;
            }

            _eligibleKillXpAwardsSeen++;

            float minimumMultiplier = Clamp(_minimumOverlevelXpMultiplier.Value, 0.0f, 1.0f);
            float appliedLevelMultiplier = vanillaLevelMultiplier;
            bool wouldAdjust = vanillaLevelMultiplier + Epsilon < minimumMultiplier;
            bool dryRun = _dryRun.Value;
            XpRewardContext context = BuildXpRewardContext(npc);

            if (wouldAdjust)
            {
                appliedLevelMultiplier = minimumMultiplier;
                float vanillaAward = context.BaseXp * context.KillExpMultiplier * Math.Max(0.0f, vanillaLevelMultiplier);
                float appliedAward = context.BaseXp * context.KillExpMultiplier * Math.Max(0.0f, appliedLevelMultiplier);
                float extraAward = Math.Max(0.0f, appliedAward - vanillaAward);

                if (dryRun)
                {
                    _dryRunAdjustments++;
                    _dryRunEstimatedExtraXpBeforeGlobal += extraAward;
                    LogAdjustedKill(context, vanillaLevelMultiplier, appliedLevelMultiplier, vanillaAward, appliedAward, true);
                    LogPeriodicSummaryIfNeeded(_dryRunAdjustments);
                    return vanillaLevelMultiplier;
                }

                _adjustedKillXpAwards++;
                _estimatedExtraXpBeforeGlobal += extraAward;
                LogAdjustedKill(context, vanillaLevelMultiplier, appliedLevelMultiplier, vanillaAward, appliedAward, false);
                LogPeriodicSummaryIfNeeded(_adjustedKillXpAwards);
                return appliedLevelMultiplier;
            }

            _unchangedEligibleKillXpAwards++;
            if (_diagnostics.Value && _logUnchangedEligibleKills.Value)
            {
                float estimatedAward = context.BaseXp * context.KillExpMultiplier * Math.Max(0.0f, vanillaLevelMultiplier);
                Log.LogInfo(
                    "Unchanged kill XP: enemy="
                    + context.EnemyName
                    + "; heroLevel="
                    + FormatOptionalFloat(context.HeroLevel)
                    + "; enemyExpLevel="
                    + FormatOptionalFloat(context.EnemyExpLevel)
                    + "; baseXP="
                    + FormatFloat(context.BaseXp)
                    + "; killMultiplier="
                    + FormatFloat(context.KillExpMultiplier)
                    + "; vanillaLevelMultiplier="
                    + FormatFloat(vanillaLevelMultiplier)
                    + "; estimatedAwardBeforeGlobal="
                    + FormatFloat(estimatedAward)
                    + "; generalExpMultiplier="
                    + FormatFloat(context.GeneralExpMultiplier)
                    + ".");
            }

            return vanillaLevelMultiplier;
        }

        private void LogAdjustedKill(
            XpRewardContext context,
            float vanillaLevelMultiplier,
            float appliedLevelMultiplier,
            float vanillaAward,
            float appliedAward,
            bool dryRun)
        {
            if (!_diagnostics.Value || !_logAdjustedKills.Value)
            {
                return;
            }

            Log.LogInfo(
                (dryRun ? "Dry-run kill XP adjustment" : "Adjusted kill XP")
                + ": enemy="
                + context.EnemyName
                + "; heroLevel="
                + FormatOptionalFloat(context.HeroLevel)
                + "; enemyExpLevel="
                + FormatOptionalFloat(context.EnemyExpLevel)
                + "; baseXP="
                + FormatFloat(context.BaseXp)
                + "; killMultiplier="
                + FormatFloat(context.KillExpMultiplier)
                + "; vanillaLevelMultiplier="
                + FormatFloat(vanillaLevelMultiplier)
                + "; appliedLevelMultiplier="
                + FormatFloat(appliedLevelMultiplier)
                + "; estimatedAwardBeforeGlobal="
                + FormatFloat(vanillaAward)
                + " -> "
                + FormatFloat(appliedAward)
                + "; generalExpMultiplier="
                + FormatFloat(context.GeneralExpMultiplier)
                + ".");

            if (!context.Complete && _logSkippedDeathChecks.Value)
            {
                Log.LogInfo("Kill XP diagnostic context was incomplete for " + context.EnemyName + ": " + context.IncompleteReason + ".");
            }
        }

        private void LogPeriodicSummaryIfNeeded(long adjustedCount)
        {
            if (!_diagnostics.Value)
            {
                return;
            }

            int interval = Math.Max(0, _summaryEveryAdjustedKills.Value);
            if (interval <= 0 || adjustedCount <= 0 || adjustedCount % interval != 0)
            {
                return;
            }

            LogSummary("periodic");
        }

        private void LogSummary(string reason)
        {
            Log.LogInfo(
                "Full Enemy XP summary ("
                + reason
                + "): eligibleKillXpAwards="
                + _eligibleKillXpAwardsSeen.ToString(CultureInfo.InvariantCulture)
                + "; adjusted="
                + _adjustedKillXpAwards.ToString(CultureInfo.InvariantCulture)
                + "; dryRunAdjustments="
                + _dryRunAdjustments.ToString(CultureInfo.InvariantCulture)
                + "; unchanged="
                + _unchangedEligibleKillXpAwards.ToString(CultureInfo.InvariantCulture)
                + "; skippedChecks="
                + _skippedAdjustmentChecks.ToString(CultureInfo.InvariantCulture)
                + "; estimatedExtraXPBeforeGlobal="
                + FormatDouble(_estimatedExtraXpBeforeGlobal)
                + "; dryRunEstimatedExtraXPBeforeGlobal="
                + FormatDouble(_dryRunEstimatedExtraXpBeforeGlobal)
                + ".");
        }

        private XpRewardContext BuildXpRewardContext(object npc)
        {
            XpRewardContext context = new XpRewardContext();
            object template = GetOptionalPropertyValue(npc, "Template");
            object hero = GetCurrentHero();

            context.EnemyName = DescribeEnemy(npc, template);
            context.EnemyExpLevel = GetOptionalFloatProperty(template, "ExpLevel", -1.0f);
            context.BaseXp = Math.Max(0.0f, TryInvokeFloatMethod(template, "GetExpReward", 0.0f));

            object heroLevel = GetOptionalPropertyValue(hero, "Level");
            context.HeroLevel = ReadStatValue(heroLevel, -1.0f);

            object heroMultStats = GetOptionalPropertyValue(hero, "HeroMultStats");
            object killExpMultiplier = GetOptionalPropertyValue(heroMultStats, "KillExpMultiplier");
            object generalExpMultiplier = GetOptionalPropertyValue(heroMultStats, "ExpMultiplier");
            context.KillExpMultiplier = Math.Max(0.0f, ReadStatValue(killExpMultiplier, 1.0f));
            context.GeneralExpMultiplier = Math.Max(0.0f, ReadStatValue(generalExpMultiplier, 1.0f));

            List<string> missing = new List<string>();
            if (template == null)
            {
                missing.Add("Template");
            }
            if (hero == null)
            {
                missing.Add("Hero.Current");
            }
            if (context.EnemyExpLevel < 0.0f)
            {
                missing.Add("enemy ExpLevel");
            }
            if (context.HeroLevel < 0.0f)
            {
                missing.Add("hero Level");
            }
            if (context.BaseXp <= 0.0f)
            {
                missing.Add("base XP");
            }

            context.Complete = missing.Count == 0;
            context.IncompleteReason = missing.Count == 0 ? "" : String.Join(", ", missing.ToArray());
            return context;
        }

        private object GetCurrentHero()
        {
            try
            {
                if (_heroCurrentGetter == null)
                {
                    _heroType = _heroType ?? AccessTools.TypeByName(HeroTypeName);
                    _heroCurrentGetter = _heroType == null ? null : AccessTools.Property(_heroType, "Current");
                }

                return _heroCurrentGetter == null ? null : _heroCurrentGetter.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetOptionalPropertyValue(object owner, string propertyName)
        {
            if (owner == null || String.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                PropertyInfo property = AccessTools.Property(owner.GetType(), propertyName);
                if (property != null)
                {
                    return property.GetValue(owner, null);
                }

                FieldInfo field = AccessTools.Field(owner.GetType(), propertyName);
                return field == null ? null : field.GetValue(owner);
            }
            catch
            {
                return null;
            }
        }

        private static float GetOptionalFloatProperty(object owner, string propertyName, float fallback)
        {
            float value;
            return TryConvertToFloat(GetOptionalPropertyValue(owner, propertyName), out value) ? value : fallback;
        }

        private static float TryInvokeFloatMethod(object owner, string methodName, float fallback)
        {
            if (owner == null || String.IsNullOrEmpty(methodName))
            {
                return fallback;
            }

            try
            {
                MethodInfo method = AccessTools.Method(owner.GetType(), methodName, new Type[0]);
                if (method == null)
                {
                    return fallback;
                }

                float value;
                return TryConvertToFloat(method.Invoke(owner, null), out value) ? value : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadStatValue(object stat, float fallback)
        {
            if (stat == null)
            {
                return fallback;
            }

            float value;
            if (TryConvertToFloat(stat, out value))
            {
                return value;
            }

            string[] properties =
            {
                "ModifiedValue",
                "ModifiedFloat",
                "Value",
                "BaseValue",
                "ModifiedInt",
                "BaseInt"
            };

            for (int i = 0; i < properties.Length; i++)
            {
                object propertyValue = GetOptionalPropertyValue(stat, properties[i]);
                if (TryConvertToFloat(propertyValue, out value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0.0f;
            if (value == null)
            {
                return false;
            }

            try
            {
                if (value is float)
                {
                    result = (float)value;
                    return true;
                }
                if (value is double)
                {
                    result = (float)(double)value;
                    return true;
                }
                if (value is int)
                {
                    result = (int)value;
                    return true;
                }
                if (value is long)
                {
                    result = (long)value;
                    return true;
                }
                if (value is short)
                {
                    result = (short)value;
                    return true;
                }
                if (value is decimal)
                {
                    result = (float)(decimal)value;
                    return true;
                }

                IConvertible convertible = value as IConvertible;
                if (convertible == null)
                {
                    return false;
                }

                result = convertible.ToSingle(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0.0f;
                return false;
            }
        }

        private static string DescribeEnemy(object npc, object template)
        {
            string name = ReadStringProperty(npc, "DisplayName");
            if (String.IsNullOrWhiteSpace(name))
            {
                name = ReadStringProperty(template, "DisplayName");
            }
            if (String.IsNullOrWhiteSpace(name))
            {
                name = ReadStringProperty(npc, "Name");
            }
            if (String.IsNullOrWhiteSpace(name))
            {
                name = ReadStringProperty(template, "Name");
            }
            if (String.IsNullOrWhiteSpace(name) && template != null)
            {
                name = template.GetType().Name;
            }
            if (String.IsNullOrWhiteSpace(name) && npc != null)
            {
                name = npc.GetType().Name;
            }

            return String.IsNullOrWhiteSpace(name) ? "unknown" : name;
        }

        private static string ReadStringProperty(object owner, string propertyName)
        {
            object value = GetOptionalPropertyValue(owner, propertyName);
            if (value == null)
            {
                return "";
            }

            try
            {
                return value.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static string FormatOptionalFloat(float value)
        {
            return value < 0.0f ? "unknown" : FormatFloat(value);
        }

        private static string FormatFloat(float value)
        {
            if (Single.IsNaN(value) || Single.IsInfinity(value))
            {
                return "unknown";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatDouble(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return "unknown";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Log.LogInfo(message);
            }
        }

        private void Warn(string message)
        {
            Log.LogWarning(message);
        }

        private sealed class XpRewardContext
        {
            public string EnemyName = "unknown";
            public float HeroLevel = -1.0f;
            public float EnemyExpLevel = -1.0f;
            public float BaseXp;
            public float KillExpMultiplier = 1.0f;
            public float GeneralExpMultiplier = 1.0f;
            public bool Complete;
            public string IncompleteReason = "";
        }

        private static class NpcDeathXpPatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                MethodInfo adjustMethod = AccessTools.Method(typeof(FullEnemyXPPlugin), "AdjustLevelMultiplier");
                bool patched = false;

                for (int i = 3; i < codes.Count; i++)
                {
                    if (!IsIncreaseByCall(codes[i].operand as MethodBase) ||
                        codes[i - 1].opcode != OpCodes.Ldnull ||
                        codes[i - 2].opcode != OpCodes.Mul ||
                        !IsLoadLocal(codes[i - 3]))
                    {
                        continue;
                    }

                    int loadMultiplierIndex = i - 3;
                    CodeInstruction originalLoadMultiplier = codes[loadMultiplierIndex];
                    CodeInstruction loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                    if (originalLoadMultiplier.labels != null)
                    {
                        loadThis.labels.AddRange(originalLoadMultiplier.labels);
                        originalLoadMultiplier.labels.Clear();
                    }
                    if (originalLoadMultiplier.blocks != null)
                    {
                        loadThis.blocks.AddRange(originalLoadMultiplier.blocks);
                        originalLoadMultiplier.blocks.Clear();
                    }

                    codes[loadMultiplierIndex] = loadThis;
                    codes.Insert(loadMultiplierIndex + 1, new CodeInstruction(originalLoadMultiplier.opcode, originalLoadMultiplier.operand));
                    codes.Insert(loadMultiplierIndex + 2, new CodeInstruction(OpCodes.Call, adjustMethod));
                    patched = true;
                    break;
                }

                if (!patched && Instance != null)
                {
                    Instance.Warn("Could not insert Full Enemy XP level multiplier adjustment. Vanilla enemy overlevel XP falloff is unchanged.");
                }

                return codes;
            }

            private static bool IsIncreaseByCall(MethodBase method)
            {
                MethodInfo methodInfo = method as MethodInfo;
                if (methodInfo == null || methodInfo.Name != "IncreaseBy")
                {
                    return false;
                }

                try
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType == typeof(float);
                }
                catch
                {
                    return false;
                }
            }

            private static bool IsLoadLocal(CodeInstruction instruction)
            {
                return instruction.opcode == OpCodes.Ldloc_0 ||
                    instruction.opcode == OpCodes.Ldloc_1 ||
                    instruction.opcode == OpCodes.Ldloc_2 ||
                    instruction.opcode == OpCodes.Ldloc_3 ||
                    instruction.opcode == OpCodes.Ldloc_S ||
                    instruction.opcode == OpCodes.Ldloc;
            }
        }
    }
}
