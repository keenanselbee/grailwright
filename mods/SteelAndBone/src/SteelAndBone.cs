using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyTitle("Steel and Bone")]
[assembly: AssemblyDescription("Enemy weakness and resistance proof of concept for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Steel and Bone")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
[assembly: AssemblyInformationalVersion("0.2.0")]

namespace SteelAndBone
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SteelAndBonePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.steel-and-bone";
        public const string PluginName = "Steel and Bone";
        public const string PluginVersion = "0.2.0";

        private const int ConfigSchemaVersion = 3;
        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";

        private static readonly ResistanceRule[] ResistanceRules =
        {
            new ResistanceRule(TargetFamily.Skeleton, DamageTag.BloodMagic | DamageTag.Bleed, "Skeleton", "Blood/Bleed", 0.45f, 0.25f, 0.0f),
            new ResistanceRule(TargetFamily.Skeleton, DamageTag.Slashing | DamageTag.Piercing, "Skeleton", "Slash/Pierce", 0.75f, 0.55f, 0.35f),
            new ResistanceRule(TargetFamily.Golem, DamageTag.BloodMagic | DamageTag.Poison, "Golem", "Blood/Poison", 0.45f, 0.25f, 0.0f),
            new ResistanceRule(TargetFamily.Wyrd, DamageTag.Wyrdness, "Wyrd", "Wyrdness", 0.45f, 0.25f, 0.0f)
        };

        private static readonly string[] BleedTerms = { "bleed" };
        private static readonly string[] PoisonTerms = { "poison", "toxic", "venom" };
        private static readonly string[] WyrdTerms = { "wyrd" };
        private static readonly string[] BloodMagicTerms = { "blood", "transfusion", "abhartach", "sanguine", "sanguis", "hematic" };

        internal static SteelAndBonePlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private MethodInfo _heroCurrentGetter;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<Preset> _preset;
        private ConfigEntry<bool> _resistanceTextEnabled;
        private ConfigEntry<float> _resistanceTextScale;
        private ConfigEntry<int> _resistanceTextFontSize;
        private ConfigEntry<float> _resistanceTextCenterX;
        private ConfigEntry<float> _resistanceTextCenterY;
        private ConfigEntry<float> _resistanceTextWidth;
        private ConfigEntry<float> _resistanceTextDurationSeconds;
        private ConfigEntry<float> _resistanceTextFadeSeconds;
        private ConfigEntry<float> _resistanceTextOpacity;
        private ConfigEntry<float> _resistanceTextCooldownSeconds;
        private ConfigEntry<string> _skeletonTerms;
        private ConfigEntry<string> _golemTerms;
        private ConfigEntry<string> _wyrdTerms;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _logPatchWarnings;

        private readonly Dictionary<int, TargetClassification> _targetClassifications =
            new Dictionary<int, TargetClassification>();

        private string _cachedSkeletonTermsRaw;
        private string[] _cachedSkeletonTerms = new string[0];
        private string _cachedGolemTermsRaw;
        private string[] _cachedGolemTerms = new string[0];
        private string _cachedWyrdTermsRaw;
        private string[] _cachedWyrdTerms = new string[0];
        private int _targetTermsRevision = 1;

        private GUIStyle _overlayTextStyle;
        private GUIStyle _overlayShadowStyle;
        private int _overlayStyleFontSize = -1;
        private ResistanceOverlayNotification _overlayNotification;
        private float _lastOverlayTime = -999.0f;
        private float _lastOverlayMultiplier = 1.0f;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();
                CacheGameAccessors();
                PatchGame();
                Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Preset=" + _preset.Value + ".");
            }
            catch (Exception ex)
            {
                Log.LogError(PluginName + " " + PluginVersion + " failed during startup: " + ex.GetBaseException().Message);
                Log.LogError(ex.ToString());
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            Instance = null;
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("1. Core", "Enabled", true, "Master switch.");
            Config.Bind("1. Core", "ConfigSchemaVersion", ConfigSchemaVersion, "Configuration layout version. It changes only when an update requires fresh defaults.");
            _preset = Config.Bind("1. Core", "Preset", Preset.Forsaken, "Resistance profile. Bloodied is light, Forsaken is the default, and Nightmare turns the strongest proof-of-concept counters into zero-damage counters.");

            _resistanceTextEnabled = Config.Bind("2. Feedback", "ResistanceTextEnabled", true, "Show a compact warning when a hit is resisted.");
            _resistanceTextScale = Config.Bind("2. Feedback", "ResistanceTextScale", 0.75f, "Scale multiplier for the compact resistance text.");
            _resistanceTextFontSize = Config.Bind("2. Feedback", "ResistanceTextFontSize", 20, "Base font size before scale is applied.");
            _resistanceTextCenterX = Config.Bind("2. Feedback", "ResistanceTextCenterX", 0.5f, "Horizontal center as a fraction of screen width. 0.5 is centered.");
            _resistanceTextCenterY = Config.Bind("2. Feedback", "ResistanceTextCenterY", 0.38f, "Vertical center as a fraction of screen height. This matches the default Killing Blow Mastery text position.");
            _resistanceTextWidth = Config.Bind("2. Feedback", "ResistanceTextWidth", 520.0f, "Text width before scale is applied.");
            _resistanceTextDurationSeconds = Config.Bind("2. Feedback", "ResistanceTextDurationSeconds", 1.25f, "How long the resistance text remains visible.");
            _resistanceTextFadeSeconds = Config.Bind("2. Feedback", "ResistanceTextFadeSeconds", 0.20f, "Fade-in and fade-out duration.");
            _resistanceTextOpacity = Config.Bind("2. Feedback", "ResistanceTextOpacity", 0.95f, "Maximum resistance text opacity.");
            _resistanceTextCooldownSeconds = Config.Bind("2. Feedback", "ResistanceTextCooldownSeconds", 0.18f, "Minimum real-time seconds between same-or-weaker resistance text refreshes.");

            _skeletonTerms = Config.Bind(
                "3. Target Families",
                "SkeletonTerms",
                "Skeleton;Skull;Bone;Animated Armor;JollySkeleton",
                "Semicolon, comma, pipe, or newline separated target terms for skeleton or bone enemies.");
            _golemTerms = Config.Bind(
                "3. Target Families",
                "GolemTerms",
                "Stone;Golem;Construct;Automaton;Statue;Crystal",
                "Semicolon, comma, pipe, or newline separated target terms for stone, golem, or construct enemies.");
            _wyrdTerms = Config.Bind(
                "3. Target Families",
                "WyrdTerms",
                "Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness",
                "Semicolon, comma, pipe, or newline separated target terms for Wyrd enemies.");

            _diagnostics = Config.Bind("4. Diagnostics", "Diagnostics", false, "Log resisted hit classification and multiplier decisions.");
            _logPatchWarnings = Config.Bind("4. Diagnostics", "LogPatchWarnings", true, "Log warnings when required game methods cannot be patched.");

            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((string.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || string.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    int.TryParse(
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
                File.WriteAllText(configPath, string.Empty);
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
                    Log.LogError("Failed to restore Steel and Bone config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset Steel and Bone config schema. Original config was left in place when possible.", ex);
            }
        }

        private void CacheGameAccessors()
        {
            Type heroType = AccessTools.TypeByName(HeroTypeName);
            if (heroType != null)
            {
                _heroCurrentGetter = AccessTools.PropertyGetter(heroType, "Current");
            }
        }

        private void PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
            if (healthElementType == null)
            {
                Warn("Could not find " + HealthElementTypeName + ". " + PluginName + " is inactive.");
                return;
            }

            MethodInfo original = AccessTools.Method(healthElementType, "ApplyDamageModifiers");
            MethodInfo postfix = AccessTools.Method(typeof(ApplyDamageModifiersPatch), "Postfix");
            if (original == null || postfix == null)
            {
                Warn("Could not patch HealthElement.ApplyDamageModifiers. " + PluginName + " is inactive.");
                return;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            LogDiagnostic("Patched " + healthElementType.FullName + ".ApplyDamageModifiers.");
        }

        internal void ApplyResistanceModifier(object healthElement, object damage, ref float damageModifier)
        {
            if (_enabled == null || !_enabled.Value || healthElement == null || damage == null)
            {
                return;
            }

            object hero = GetCurrentHero();
            if (hero == null || !IsHeroDamageSource(damage, hero))
            {
                return;
            }

            object heroHealthElement = GetOptionalPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return;
            }

            object target = ResolveDamageTargetOwner(healthElement, damage);
            if (target != null && IsSameModelOrOwner(target, hero))
            {
                return;
            }

            TargetClassification targetClass = GetTargetClassification(target, healthElement);
            DamageClassification damageClass = ClassifyDamage(damage);

            float resistanceMultiplier;
            string targetFamily;
            string damageFamily;
            if (!TryResolveResistance(targetClass, damageClass, out resistanceMultiplier, out targetFamily, out damageFamily))
            {
                return;
            }

            resistanceMultiplier = Clamp(resistanceMultiplier, 0.0f, 1.0f);
            if (resistanceMultiplier >= 0.999f)
            {
                return;
            }

            float before = damageModifier;
            damageModifier *= resistanceMultiplier;
            if (resistanceMultiplier <= 0.0001f)
            {
                damageModifier = 0.0f;
            }

            QueueResistanceText(resistanceMultiplier);
            LogDiagnostic(
                "Applied resistance: target="
                + DescribeObject(target)
                + ", family="
                + targetFamily
                + ", damage="
                + damageFamily
                + ", preset="
                + _preset.Value
                + ", multiplier="
                + resistanceMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + ", damageModifier "
                + before.ToString("0.###", CultureInfo.InvariantCulture)
                + " -> "
                + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
        }

        private bool TryResolveResistance(
            TargetClassification targetClass,
            DamageClassification damageClass,
            out float multiplier,
            out string targetFamily,
            out string damageFamily)
        {
            multiplier = 1.0f;
            targetFamily = "";
            damageFamily = "";

            if (targetClass == null || damageClass == null)
            {
                return false;
            }

            bool matched = false;
            Preset preset = _preset == null ? Preset.Forsaken : _preset.Value;
            for (int i = 0; i < ResistanceRules.Length; i++)
            {
                ResistanceRule rule = ResistanceRules[i];
                if (!TargetMatchesRule(targetClass, rule.TargetFamily) || !damageClass.HasAny(rule.DamageTags))
                {
                    continue;
                }

                float ruleMultiplier = rule.GetMultiplier(preset);
                if (!matched || ruleMultiplier < multiplier)
                {
                    matched = true;
                    multiplier = ruleMultiplier;
                    targetFamily = rule.TargetLabel;
                    damageFamily = rule.DamageLabel;
                }
            }

            return matched;
        }

        private bool TargetMatchesRule(TargetClassification targetClass, TargetFamily family)
        {
            switch (family)
            {
                case TargetFamily.Skeleton:
                    return targetClass.IsSkeleton;
                case TargetFamily.Golem:
                    return targetClass.IsGolem;
                case TargetFamily.Wyrd:
                    return targetClass.IsWyrd;
                default:
                    return false;
            }
        }

        private TargetClassification GetTargetClassification(object target, object healthElement)
        {
            object key = target ?? healthElement;
            if (key == null)
            {
                return TargetClassification.Empty;
            }

            int cacheKey = RuntimeHelpers.GetHashCode(key);
            TargetClassification cached;
            if (_targetClassifications.TryGetValue(cacheKey, out cached)
                && cached.Revision == _targetTermsRevision
                && ReferenceEquals(cached.Key, key))
            {
                return cached;
            }

            string text = BuildObjectSearchText(target);
            if (healthElement != null && !ReferenceEquals(healthElement, target))
            {
                text = text + " " + BuildObjectSearchText(healthElement);
            }

            TargetClassification classification = new TargetClassification
            {
                Key = key,
                Revision = _targetTermsRevision,
                IsSkeleton = ContainsAnyTerm(text, GetSkeletonTerms()),
                IsGolem = ContainsAnyTerm(text, GetGolemTerms()),
                IsWyrd = ContainsAnyTerm(text, GetWyrdTerms())
            };

            _targetClassifications[cacheKey] = classification;
            return classification;
        }

        private DamageClassification ClassifyDamage(object damage)
        {
            if (damage == null)
            {
                return DamageClassification.Empty;
            }

            DamageClassification classification = new DamageClassification();
            string damageSearchText = BuildDamageSearchText(damage).ToLowerInvariant();
            classification.IsBleed = ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Bleed")
                || TextContainsAny(damageSearchText, BleedTerms);
            classification.IsPoison = DamageHasSubtype(damage, "Poison")
                || ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Poison")
                || TextContainsAny(damageSearchText, PoisonTerms);
            classification.IsWyrdness = DamageHasSubtype(damage, "Wyrdness")
                || TextContainsAny(damageSearchText, WyrdTerms);
            classification.IsBloodMagic = TextContainsAny(damageSearchText, BloodMagicTerms);
            classification.IsSlashing = DamageHasSubtype(damage, "Slashing");
            classification.IsPiercing = DamageHasSubtype(damage, "Piercing");
            classification.IsBludgeoning = DamageHasSubtype(damage, "Bludgeoning");
            classification.IsGenericPhysical = DamageHasSubtype(damage, "GenericPhysical");

            if (classification.IsBloodMagic)
            {
                classification.Tags |= DamageTag.BloodMagic;
            }
            if (classification.IsBleed)
            {
                classification.Tags |= DamageTag.Bleed;
            }
            if (classification.IsPoison)
            {
                classification.Tags |= DamageTag.Poison;
            }
            if (classification.IsWyrdness)
            {
                classification.Tags |= DamageTag.Wyrdness;
            }
            if (classification.IsSlashing)
            {
                classification.Tags |= DamageTag.Slashing;
            }
            if (classification.IsPiercing)
            {
                classification.Tags |= DamageTag.Piercing;
            }
            if (classification.IsBludgeoning)
            {
                classification.Tags |= DamageTag.Bludgeoning;
            }
            if (classification.IsGenericPhysical)
            {
                classification.Tags |= DamageTag.GenericPhysical;
            }

            return classification;
        }

        private bool DamageHasSubtype(object damage, string subtypeName)
        {
            return EnumerablePartsContainName(GetOptionalPropertyValue(damage, "SubTypes"), "SubType", subtypeName)
                || EnumerablePartsContainName(GetOptionalPropertyValue(GetOptionalPropertyValue(damage, "DamageTypeData"), "OriginalParts"), "SubType", subtypeName);
        }

        private bool EnumerablePartsContainName(object parts, string propertyName, string expected)
        {
            IEnumerable enumerable = parts as IEnumerable;
            if (enumerable != null)
            {
                foreach (object part in enumerable)
                {
                    if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                    {
                        return true;
                    }
                }
            }

            int count = GetOptionalIntProperty(parts, "Count", -1);
            if (count > 0)
            {
                PropertyInfo itemProperty = GetIndexerProperty(parts.GetType());
                if (itemProperty != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        object part = GetIndexedValue(itemProperty, parts, i);
                        if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TextContainsAny(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                string term = terms[i];
                if (!string.IsNullOrEmpty(term) && text.Contains(term.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildDamageSearchText(object damage)
        {
            if (damage == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(DescribeObject(damage)).Append(' ');
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Item"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "BlockingItem"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Skill"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Type"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "StatusDamageType"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "DamageTypeData"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Parameters"));

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            AppendObjectSearchText(builder, projectile);
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceWeapon"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceProjectile"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "Skill"));

            return builder.ToString();
        }

        private void AppendObjectSearchText(StringBuilder builder, object obj)
        {
            if (builder == null || obj == null || IsDestroyedUnityObject(obj))
            {
                return;
            }

            builder.Append(BuildObjectSearchText(obj)).Append(' ');
            builder.Append(DescribeObject(obj)).Append(' ');
        }

        private string BuildObjectSearchText(object obj)
        {
            if (obj == null || IsDestroyedUnityObject(obj))
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            Type type = obj.GetType();
            builder.Append(type.FullName).Append(' ');
            builder.Append(type.Name).Append(' ');
            AppendStringProperty(builder, obj, "Name");
            AppendStringProperty(builder, obj, "DisplayName");
            AppendStringProperty(builder, obj, "DebugName");
            AppendStringProperty(builder, obj, "TechnicalName");
            AppendStringProperty(builder, obj, "Id");
            AppendStringProperty(builder, obj, "ID");

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj) && !IsDestroyedUnityObject(template))
            {
                Type templateType = template.GetType();
                builder.Append(templateType.FullName).Append(' ');
                builder.Append(templateType.Name).Append(' ');
                AppendStringProperty(builder, template, "Name");
                AppendStringProperty(builder, template, "DisplayName");
                AppendStringProperty(builder, template, "DebugName");
                AppendStringProperty(builder, template, "TechnicalName");
                AppendStringProperty(builder, template, "GUID");
                AppendStringProperty(builder, template, "Guid");
            }

            return builder.ToString();
        }

        private void AppendStringProperty(StringBuilder builder, object obj, string propertyName)
        {
            object raw = GetOptionalPropertyValue(obj, propertyName);
            if (raw == null)
            {
                return;
            }

            string value = raw as string;
            if (value == null)
            {
                value = raw.ToString();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append(value).Append(' ');
            }
        }

        private object ResolveDamageTargetOwner(object healthElement, object damage)
        {
            object target = GetOptionalPropertyValue(damage, "Target");
            if (target == null)
            {
                target = GetOptionalPropertyValue(damage, "TargetPure");
            }
            if (target == null)
            {
                target = ResolveHealthElementOwner(healthElement);
            }

            return target;
        }

        private object ResolveHealthElementOwner(object healthElement)
        {
            if (healthElement == null)
            {
                return null;
            }

            string[] ownerProperties = { "ParentModel", "GenericParentModel", "NpcElement", "Character", "CharacterView", "Owner", "Parent" };
            for (int i = 0; i < ownerProperties.Length; i++)
            {
                object value = GetOptionalPropertyValue(healthElement, ownerProperties[i]);
                if (value != null && !ReferenceEquals(value, healthElement))
                {
                    return value;
                }
            }

            return null;
        }

        private object GetCurrentHero()
        {
            if (_heroCurrentGetter == null)
            {
                return null;
            }

            try
            {
                return _heroCurrentGetter.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        private bool IsHeroDamageSource(object damage, object hero)
        {
            if (damage == null || hero == null)
            {
                return false;
            }

            object damageDealer = GetOptionalPropertyValue(damage, "DamageDealerPure");
            if (damageDealer == null)
            {
                damageDealer = GetOptionalPropertyValue(damage, "DamageDealer");
            }
            if (IsSameModelOrOwner(damageDealer, hero))
            {
                return true;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            object projectileOwner = GetOptionalPropertyValue(projectile, "Owner");
            return IsSameModelOrOwner(projectileOwner, hero);
        }

        private bool IsSameModelOrOwner(object candidate, object expected)
        {
            if (candidate == null || expected == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, expected))
            {
                return true;
            }

            string[] properties = { "ParentModel", "GenericParentModel", "Owner", "Character", "Hero" };
            for (int i = 0; i < properties.Length; i++)
            {
                object value = GetOptionalPropertyValue(candidate, properties[i]);
                if (ReferenceEquals(value, expected))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] GetSkeletonTerms()
        {
            return GetTerms(_skeletonTerms, ref _cachedSkeletonTermsRaw, ref _cachedSkeletonTerms);
        }

        private string[] GetGolemTerms()
        {
            return GetTerms(_golemTerms, ref _cachedGolemTermsRaw, ref _cachedGolemTerms);
        }

        private string[] GetWyrdTerms()
        {
            return GetTerms(_wyrdTerms, ref _cachedWyrdTermsRaw, ref _cachedWyrdTerms);
        }

        private string[] GetTerms(ConfigEntry<string> entry, ref string cachedRaw, ref string[] cachedTerms)
        {
            string raw = entry == null ? "" : (entry.Value ?? "");
            if (raw != cachedRaw)
            {
                cachedRaw = raw;
                cachedTerms = SplitTerms(raw);
                _targetTermsRevision++;
                _targetClassifications.Clear();
            }

            return cachedTerms;
        }

        private string[] SplitTerms(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }

            string[] pieces = raw.Split(new[] { ';', ',', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> terms = new List<string>();
            for (int i = 0; i < pieces.Length; i++)
            {
                string term = pieces[i].Trim();
                if (term.Length > 0)
                {
                    terms.Add(term);
                }
            }

            return terms.ToArray();
        }

        private bool ContainsAnyTerm(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null || terms.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void QueueResistanceText(float multiplier)
        {
            if (_resistanceTextEnabled == null || !_resistanceTextEnabled.Value)
            {
                return;
            }

            float now = Time.unscaledTime;
            float cooldown = _resistanceTextCooldownSeconds == null ? 0.0f : Math.Max(0.0f, _resistanceTextCooldownSeconds.Value);
            if (cooldown > 0.0f && now - _lastOverlayTime < cooldown && multiplier >= _lastOverlayMultiplier)
            {
                return;
            }

            _lastOverlayTime = now;
            _lastOverlayMultiplier = multiplier;
            _overlayNotification = new ResistanceOverlayNotification
            {
                Text = "Resistant",
                Multiplier = multiplier,
                StartTime = now
            };
        }

        private void OnGUI()
        {
            if (_overlayNotification == null || _enabled == null || !_enabled.Value || _resistanceTextEnabled == null || !_resistanceTextEnabled.Value)
            {
                return;
            }

            float duration = Math.Max(0.05f, _resistanceTextDurationSeconds.Value);
            float elapsed = Time.unscaledTime - _overlayNotification.StartTime;
            if (elapsed > duration)
            {
                _overlayNotification = null;
                return;
            }

            float scale = Math.Max(0.05f, _resistanceTextScale.Value);
            int fontSize = Math.Max(1, (int)Math.Round(Math.Max(1, _resistanceTextFontSize.Value) * scale));
            EnsureOverlayStyles(fontSize);

            float width = Math.Max(20.0f, _resistanceTextWidth.Value * scale);
            float height = Math.Max(fontSize + 10.0f, 32.0f * scale);
            float centerX = Screen.width * _resistanceTextCenterX.Value;
            float centerY = Screen.height * _resistanceTextCenterY.Value;
            Rect rect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
            Rect shadowRect = new Rect(rect.x + Math.Max(1.0f, 2.0f * scale), rect.y + Math.Max(1.0f, 2.0f * scale), rect.width, rect.height);

            float alpha = GetOverlayAlpha(elapsed, duration) * Math.Max(0.0f, _resistanceTextOpacity.Value);
            Color textColor = GetResistanceTextColor(_overlayNotification.Multiplier, alpha);
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            Color previousTextColor = _overlayTextStyle.normal.textColor;
            Color previousShadowColor = _overlayShadowStyle.normal.textColor;

            GUI.depth = -1000;
            _overlayTextStyle.normal.textColor = textColor;
            _overlayShadowStyle.normal.textColor = new Color(0.0f, 0.0f, 0.0f, alpha * 0.8f);

            GUI.Label(shadowRect, _overlayNotification.Text, _overlayShadowStyle);
            GUI.Label(rect, _overlayNotification.Text, _overlayTextStyle);

            _overlayTextStyle.normal.textColor = previousTextColor;
            _overlayShadowStyle.normal.textColor = previousShadowColor;
            GUI.depth = previousDepth;
            GUI.color = previousColor;
        }

        private Color GetResistanceTextColor(float multiplier, float alpha)
        {
            if (multiplier <= 0.0001f)
            {
                return new Color(1.0f, 0.04f, 0.02f, alpha);
            }

            float severity = 1.0f - Clamp(multiplier / 0.75f, 0.0f, 1.0f);
            Color start = new Color(1.0f, 0.72f, 0.18f, alpha);
            Color end = new Color(1.0f, 0.08f, 0.03f, alpha);
            return Color.Lerp(start, end, severity);
        }

        private float GetOverlayAlpha(float elapsed, float duration)
        {
            float fade = Math.Max(0.0f, _resistanceTextFadeSeconds.Value);
            if (fade <= 0.001f)
            {
                return 1.0f;
            }

            float alpha = 1.0f;
            if (elapsed < fade)
            {
                alpha = Math.Min(alpha, elapsed / fade);
            }

            float remaining = duration - elapsed;
            if (remaining < fade)
            {
                alpha = Math.Min(alpha, Math.Max(0.0f, remaining / fade));
            }

            return Clamp(alpha, 0.0f, 1.0f);
        }

        private void EnsureOverlayStyles(int fontSize)
        {
            if (_overlayTextStyle != null && _overlayStyleFontSize == fontSize)
            {
                return;
            }

            _overlayStyleFontSize = fontSize;
            _overlayTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow,
                wordWrap = false
            };
            _overlayShadowStyle = new GUIStyle(_overlayTextStyle);
        }

        private object GetOptionalPropertyValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private int GetOptionalIntProperty(object instance, string propertyName, int fallback)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            if (value is int)
            {
                return (int)value;
            }
            if (value is uint)
            {
                uint uintValue = (uint)value;
                return uintValue > int.MaxValue ? int.MaxValue : (int)uintValue;
            }

            return fallback;
        }

        private PropertyInfo GetIndexerProperty(Type type)
        {
            if (type == null)
            {
                return null;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; i++)
            {
                ParameterInfo[] parameters = properties[i].GetIndexParameters();
                if (properties[i].Name == "Item"
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(int))
                {
                    return properties[i];
                }
            }

            return null;
        }

        private object GetIndexedValue(PropertyInfo indexer, object instance, int index)
        {
            if (indexer == null || instance == null)
            {
                return null;
            }

            try
            {
                return indexer.GetValue(instance, new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private bool ValueNameContains(object value, string expected)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return value.ToString().IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
        }

        private string DescribeObject(object value)
        {
            if (value == null)
            {
                return "null";
            }

            string displayName = GetOptionalPropertyValue(value, "DisplayName") as string;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "Name") as string;
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "DebugName") as string;
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            return value.GetType().Name;
        }

        private float Clamp(float value, float min, float max)
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

        private void LogDiagnostic(string message)
        {
            if (_diagnostics != null && _diagnostics.Value)
            {
                Log.LogInfo(message);
            }
        }

        private void Warn(string message)
        {
            if (_logPatchWarnings == null || _logPatchWarnings.Value)
            {
                Log.LogWarning(message);
            }
        }

        private enum Preset
        {
            Bloodied,
            Forsaken,
            Nightmare
        }

        private enum TargetFamily
        {
            Skeleton,
            Golem,
            Wyrd
        }

        [Flags]
        private enum DamageTag
        {
            None = 0,
            BloodMagic = 1,
            Bleed = 2,
            Poison = 4,
            Wyrdness = 8,
            Slashing = 16,
            Piercing = 32,
            Bludgeoning = 64,
            GenericPhysical = 128
        }

        private sealed class ResistanceRule
        {
            public readonly TargetFamily TargetFamily;
            public readonly DamageTag DamageTags;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            private readonly float _bloodiedMultiplier;
            private readonly float _forsakenMultiplier;
            private readonly float _nightmareMultiplier;

            public ResistanceRule(
                TargetFamily targetFamily,
                DamageTag damageTags,
                string targetLabel,
                string damageLabel,
                float bloodiedMultiplier,
                float forsakenMultiplier,
                float nightmareMultiplier)
            {
                TargetFamily = targetFamily;
                DamageTags = damageTags;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                _bloodiedMultiplier = bloodiedMultiplier;
                _forsakenMultiplier = forsakenMultiplier;
                _nightmareMultiplier = nightmareMultiplier;
            }

            public float GetMultiplier(Preset preset)
            {
                switch (preset)
                {
                    case Preset.Bloodied:
                        return _bloodiedMultiplier;
                    case Preset.Nightmare:
                        return _nightmareMultiplier;
                    case Preset.Forsaken:
                    default:
                        return _forsakenMultiplier;
                }
            }
        }

        private sealed class TargetClassification
        {
            public static readonly TargetClassification Empty = new TargetClassification();

            public object Key;
            public int Revision;
            public bool IsSkeleton;
            public bool IsGolem;
            public bool IsWyrd;
        }

        private sealed class DamageClassification
        {
            public static readonly DamageClassification Empty = new DamageClassification();

            public bool IsBloodMagic;
            public bool IsBleed;
            public bool IsPoison;
            public bool IsWyrdness;
            public bool IsSlashing;
            public bool IsPiercing;
            public bool IsBludgeoning;
            public bool IsGenericPhysical;
            public DamageTag Tags;

            public bool HasAny(DamageTag tags)
            {
                return (Tags & tags) != DamageTag.None;
            }
        }

        private sealed class ResistanceOverlayNotification
        {
            public string Text;
            public float Multiplier;
            public float StartTime;
        }

        private static class ApplyDamageModifiersPatch
        {
            public static void Postfix(object __instance, object damage, ref float dmgModifier)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyResistanceModifier(__instance, damage, ref dmgModifier);
                }
            }
        }
    }
}
