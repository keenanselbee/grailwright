using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace Grailwright.Shared
{
    internal sealed class ConfigRecoveryUiMetadata
    {
        public string DisplaySection { get; set; }
        public string DisplayName { get; set; }
        public int SectionOrder { get; set; }
        public int Order { get; set; }
    }

    internal sealed class ConfigRecoveryKeepCurrentDefaultRule
    {
        internal ConfigRecoveryKeepCurrentDefaultRule(
            int changedInSchema,
            string section,
            string key,
            string reason)
        {
            if (changedInSchema <= 0)
            {
                throw new ArgumentOutOfRangeException("changedInSchema");
            }

            ChangedInSchema = changedInSchema;
            Definition = new ConfigDefinition(section, key);
            Reason = reason ?? string.Empty;
        }

        internal int ChangedInSchema { get; private set; }
        internal ConfigDefinition Definition { get; private set; }
        internal string Reason { get; private set; }

        internal bool AppliesTo(int backupSchema, int currentSchema)
        {
            return backupSchema < ChangedInSchema
                && ChangedInSchema <= currentSchema;
        }
    }

    internal enum ConfigRecoveryValueStatus
    {
        Customized,
        IncompatibleType,
        MissingDefault,
        Invalid,
        UntouchedDefault
    }

    internal sealed class ConfigRecoveryStoredSetting
    {
        internal ConfigRecoveryStoredSetting(
            ConfigDefinition definition,
            string typeName,
            bool hasType,
            string defaultValue,
            bool hasDefault,
            string value)
        {
            Definition = definition;
            TypeName = typeName;
            HasType = hasType;
            DefaultValue = defaultValue;
            HasDefault = hasDefault;
            Value = value;
        }

        internal ConfigDefinition Definition { get; private set; }
        internal string TypeName { get; private set; }
        internal bool HasType { get; private set; }
        internal string DefaultValue { get; private set; }
        internal bool HasDefault { get; private set; }
        internal string Value { get; private set; }
    }

    internal sealed class ConfigRecoveryCustomizationProfile
    {
        private readonly Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>
            _settings;
        private readonly int _backupSchema;
        private readonly int _currentSchema;
        private readonly ConfigRecoveryKeepCurrentDefaultRule[] _rules;
        private readonly HashSet<ConfigDefinition> _permanentExclusions;

        internal ConfigRecoveryCustomizationProfile(
            IDictionary<ConfigDefinition, ConfigRecoveryStoredSetting> settings,
            int backupSchema,
            int currentSchema,
            IEnumerable<ConfigRecoveryKeepCurrentDefaultRule> rules,
            IEnumerable<ConfigDefinition> permanentExclusions)
        {
            _settings = settings == null
                ? new Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>()
                : new Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>(
                    settings);
            _backupSchema = backupSchema;
            _currentSchema = currentSchema;
            _rules = rules == null
                ? new ConfigRecoveryKeepCurrentDefaultRule[0]
                : rules.Where(rule => rule != null).ToArray();
            _permanentExclusions = permanentExclusions == null
                ? new HashSet<ConfigDefinition>()
                : new HashSet<ConfigDefinition>(permanentExclusions);
        }

        internal bool ShouldRecover<T>(string section, string key)
        {
            T previousValue;
            return TryGetCustomizedValue(section, key, out previousValue);
        }

        internal bool TryGetCustomizedValue<T>(
            string section,
            string key,
            out T previousValue)
        {
            previousValue = default(T);
            ConfigDefinition definition =
                new ConfigDefinition(section, key);
            ConfigRecoveryStoredSetting setting;
            object parsedValue;
            if (ConfigPreviousSettingsRecovery.IsAlwaysExcluded(
                    definition,
                    _permanentExclusions)
                || ConfigPreviousSettingsRecovery.ShouldKeepCurrentDefault(
                     _backupSchema,
                     _currentSchema,
                     definition,
                     _rules)
                || !_settings.TryGetValue(definition, out setting)
                || ConfigPreviousSettingsRecovery.GetValueStatus(
                    setting,
                    typeof(T),
                    out parsedValue) != ConfigRecoveryValueStatus.Customized
                || !(parsedValue is T))
            {
                return false;
            }

            previousValue = (T)parsedValue;
            return true;
        }
    }

    internal sealed class ConfigPreviousSettingsRecovery
    {
        internal const string RecoverySection = "99. Import Previous Settings";
        internal const string CurrentSchemaKey = "CurrentSchema";
        internal const string AvailableBackupSchemaKey = "AvailableBackupSchema";
        internal const string RecoveryKey = "ImportPreviousSettingsNow";

        private readonly ConfigFile _config;
        private readonly ManualLogSource _log;
        private readonly string _pluginName;
        private readonly int _currentSchema;
        private readonly int _minimumSupportedBackupSchema;
        private readonly ConfigRecoveryKeepCurrentDefaultRule[] _keepCurrentDefaultRules;
        private readonly HashSet<ConfigDefinition> _permanentExclusions;
        private ConfigEntry<string> _currentSchemaEntry;
        private ConfigEntry<string> _availableBackupSchemaEntry;
        private ConfigEntry<bool> _importPreviousSettingsNow;
        private bool _resettingAction;

        private ConfigPreviousSettingsRecovery(
            ConfigFile config,
            ManualLogSource log,
            string pluginName,
            int currentSchema,
            int minimumSupportedBackupSchema,
            IEnumerable<ConfigRecoveryKeepCurrentDefaultRule> keepCurrentDefaultRules,
            IEnumerable<ConfigDefinition> permanentExclusions)
        {
            _config = config;
            _log = log;
            _pluginName = pluginName;
            _currentSchema = currentSchema;
            _minimumSupportedBackupSchema = minimumSupportedBackupSchema;
            _keepCurrentDefaultRules = keepCurrentDefaultRules == null
                ? new ConfigRecoveryKeepCurrentDefaultRule[0]
                : keepCurrentDefaultRules.Where(rule => rule != null).ToArray();
            _permanentExclusions = permanentExclusions == null
                ? new HashSet<ConfigDefinition>()
                : new HashSet<ConfigDefinition>(permanentExclusions);
        }

        internal static bool Bind(
            ConfigFile config,
            ManualLogSource log,
            string pluginName,
            int currentSchema,
            int minimumSupportedBackupSchema,
            IEnumerable<ConfigRecoveryKeepCurrentDefaultRule> keepCurrentDefaultRules = null,
            IEnumerable<ConfigDefinition> permanentExclusions = null)
        {
            if (config == null
                || log == null
                || string.IsNullOrWhiteSpace(config.ConfigFilePath)
                || currentSchema <= 0
                || minimumSupportedBackupSchema <= 0)
            {
                return false;
            }

            ConfigPreviousSettingsRecovery recovery =
                new ConfigPreviousSettingsRecovery(
                    config,
                    log,
                    pluginName,
                    currentSchema,
                    minimumSupportedBackupSchema,
                    keepCurrentDefaultRules,
                    permanentExclusions);
            recovery.BindRecoveryTab(recovery.FindLatestSupportedBackup());
            return true;
        }

        internal static bool ShouldKeepCurrentDefault(
            int backupSchema,
            int currentSchema,
            ConfigDefinition definition,
            IEnumerable<ConfigRecoveryKeepCurrentDefaultRule> rules)
        {
            if (definition == null || rules == null)
            {
                return false;
            }

            foreach (ConfigRecoveryKeepCurrentDefaultRule rule in rules)
            {
                if (rule != null
                    && rule.AppliesTo(backupSchema, currentSchema)
                    && rule.Definition.Equals(definition))
                {
                    return true;
                }
            }

            return false;
        }

        internal static ConfigRecoveryCustomizationProfile
            ReadCustomizationProfile(
                string configPath,
                int backupSchema,
                int currentSchema,
                IEnumerable<ConfigRecoveryKeepCurrentDefaultRule> rules,
                IEnumerable<ConfigDefinition> permanentExclusions = null)
        {
            BackupConfig backup;
            if (string.IsNullOrWhiteSpace(configPath)
                || !TryReadBackup(configPath, out backup))
            {
                return new ConfigRecoveryCustomizationProfile(
                    null,
                    backupSchema,
                    currentSchema,
                    rules,
                    permanentExclusions);
            }

            return new ConfigRecoveryCustomizationProfile(
                backup.Settings,
                backupSchema,
                currentSchema,
                rules,
                permanentExclusions);
        }

        private void BindRecoveryTab(BackupConfig backup)
        {
            string currentSchema =
                _currentSchema.ToString(CultureInfo.InvariantCulture);
            string availableBackupSchema = backup == null
                ? "None"
                : backup.Schema.ToString(CultureInfo.InvariantCulture);

            _currentSchemaEntry = _config.Bind(
                RecoverySection,
                CurrentSchemaKey,
                currentSchema,
                new ConfigDescription(
                    "Current config schema.",
                    new AcceptableValueList<string>(currentSchema),
                    new ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Import Previous Settings",
                        DisplayName = "Current Schema",
                        SectionOrder = Int32.MaxValue,
                        Order = 0
                    }));
            _availableBackupSchemaEntry = _config.Bind(
                RecoverySection,
                AvailableBackupSchemaKey,
                availableBackupSchema,
                new ConfigDescription(
                    backup == null
                        ? "No compatible previous config backup is available."
                        : "Newest compatible config backup available for import.",
                    new AcceptableValueList<string>(availableBackupSchema),
                    new ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Import Previous Settings",
                        DisplayName = "Available Backup Schema",
                        SectionOrder = Int32.MaxValue,
                        Order = 1
                    }));

            bool statusSaveOnConfigSet = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
            try
            {
                _currentSchemaEntry.Value = currentSchema;
                _availableBackupSchemaEntry.Value = availableBackupSchema;
            }
            finally
            {
                _config.SaveOnConfigSet = statusSaveOnConfigSet;
            }

            _importPreviousSettingsNow = _config.Bind(
                RecoverySection,
                RecoveryKey,
                false,
                new ConfigDescription(
                    backup == null
                        ? "No compatible backup is available. Turning this on will make no changes, and it will automatically turn back off."
                        : "Turn this on to import compatible settings you customized from the available backup. The current config is backed up first, and this will automatically turn back off when finished. Restart the game after importing.",
                    null,
                    new ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Import Previous Settings",
                        DisplayName = "Import Previous Settings Now",
                        SectionOrder = Int32.MaxValue,
                        Order = 2
                    }));

            if (_importPreviousSettingsNow.Value)
            {
                bool saveOnConfigSet = _config.SaveOnConfigSet;
                _config.SaveOnConfigSet = false;
                try
                {
                    _importPreviousSettingsNow.Value = false;
                    _config.Save();
                }
                finally
                {
                    _config.SaveOnConfigSet = saveOnConfigSet;
                }
            }

            _importPreviousSettingsNow.SettingChanged +=
                OnImportPreviousSettingsNowChanged;
        }

        private void OnImportPreviousSettingsNowChanged(
            object sender,
            EventArgs eventArgs)
        {
            if (_resettingAction
                || _importPreviousSettingsNow == null
                || !_importPreviousSettingsNow.Value)
            {
                return;
            }

            _resettingAction = true;
            bool saveOnConfigSet = _config.SaveOnConfigSet;
            _config.SaveOnConfigSet = false;
            try
            {
                _importPreviousSettingsNow.Value = false;
                ImportPreviousSettings();
                _config.Save();
            }
            catch (Exception exception)
            {
                _log.LogError(
                    "Could not import previous "
                    + _pluginName
                    + " settings: "
                    + exception.GetBaseException().Message);
                try
                {
                    _config.Save();
                }
                catch
                {
                }
            }
            finally
            {
                _config.SaveOnConfigSet = saveOnConfigSet;
                _resettingAction = false;
                TryRefreshFoAModManager();
            }
        }

        private void ImportPreviousSettings()
        {
            BackupConfig backup = FindLatestSupportedBackup();
            if (backup == null)
            {
                _log.LogWarning(
                    "No supported previous "
                    + _pluginName
                    + " config backup is available to import.");
                return;
            }

            ImportSummary summary = BuildImportPlan(backup);
            if (summary.Changes.Count == 0)
            {
                _log.LogInfo(
                    "Previous "
                    + _pluginName
                    + " settings import found no customized compatible values to restore. "
                    + summary.DescribeSkips()
                    + ".");
                return;
            }

            string preImportBackupPath = CreatePreImportBackup();
            List<AppliedChange> applied = new List<AppliedChange>();
            try
            {
                for (int i = 0; i < summary.Changes.Count; i++)
                {
                    PendingChange change = summary.Changes[i];
                    applied.Add(
                        new AppliedChange(change.Entry, change.Entry.BoxedValue));
                    change.Entry.BoxedValue = change.Value;
                }

                _config.Save();
            }
            catch
            {
                for (int i = applied.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        applied[i].Entry.BoxedValue = applied[i].PreviousValue;
                    }
                    catch
                    {
                    }
                }

                try
                {
                    File.Copy(preImportBackupPath, _config.ConfigFilePath, true);
                    _config.Reload();
                }
                catch
                {
                }

                throw;
            }

            _log.LogInfo(
                "Imported "
                + summary.Changes.Count.ToString(CultureInfo.InvariantCulture)
                + " customized compatible "
                + _pluginName
                + " setting(s) from "
                + Path.GetFileName(backup.Path)
                + "; clamped="
                + summary.Clamped.ToString(CultureInfo.InvariantCulture)
                + "; "
                + summary.DescribeSkips()
                + ". Backed up the pre-import config to "
                + preImportBackupPath
                + ". Restart the game so every imported setting is applied consistently.");
        }

        private ImportSummary BuildImportPlan(BackupConfig backup)
        {
            ImportSummary summary = new ImportSummary();
            foreach (ConfigRecoveryStoredSetting backupSetting
                in backup.Settings.Values)
            {
                ConfigDefinition definition = backupSetting.Definition;
                if (IsAlwaysExcluded(definition, _permanentExclusions))
                {
                    summary.InternalOrAction++;
                    continue;
                }

                ConfigEntryBase currentEntry;
                if (!_config.ContainsKey(definition))
                {
                    summary.RemovedOrRenamed++;
                    continue;
                }
                currentEntry = _config[definition];
                if (currentEntry == null)
                {
                    summary.RemovedOrRenamed++;
                    continue;
                }

                if (ShouldKeepCurrentDefault(
                    backup.Schema,
                    _currentSchema,
                    definition,
                    _keepCurrentDefaultRules))
                {
                    summary.TransitionBlocked++;
                    continue;
                }

                object previousValue;
                ConfigRecoveryValueStatus valueStatus = GetValueStatus(
                    backupSetting,
                    currentEntry.SettingType,
                    out previousValue);
                if (valueStatus == ConfigRecoveryValueStatus.IncompatibleType)
                {
                    summary.IncompatibleType++;
                    continue;
                }

                if (valueStatus == ConfigRecoveryValueStatus.MissingDefault)
                {
                    summary.UnknownPreviousDefault++;
                    continue;
                }

                if (valueStatus == ConfigRecoveryValueStatus.Invalid)
                {
                    summary.Invalid++;
                    continue;
                }

                if (valueStatus == ConfigRecoveryValueStatus.UntouchedDefault)
                {
                    summary.UntouchedPreviousDefault++;
                    continue;
                }

                object currentValue = previousValue;
                if (currentEntry.Description != null
                    && currentEntry.Description.AcceptableValues != null)
                {
                    currentValue =
                        currentEntry.Description.AcceptableValues.Clamp(
                            previousValue);
                    if (!SerializedValuesEqual(
                        previousValue,
                        currentValue,
                        currentEntry.SettingType))
                    {
                        summary.Clamped++;
                    }
                }

                if (SerializedValuesEqual(
                    currentEntry.BoxedValue,
                    currentValue,
                    currentEntry.SettingType))
                {
                    summary.AlreadyCurrent++;
                    continue;
                }

                summary.Changes.Add(
                    new PendingChange(currentEntry, currentValue));
            }

            return summary;
        }

        internal static bool TryRestore<T>(
            ConfigEntry<T> entry,
            T previousValue,
            out bool clamped)
        {
            clamped = false;
            if (entry == null)
            {
                return false;
            }

            object restoredValue = previousValue;
            try
            {
                if (entry.Description != null
                    && entry.Description.AcceptableValues != null)
                {
                    restoredValue =
                        entry.Description.AcceptableValues.Clamp(
                            previousValue);
                }

                if (!(restoredValue is T))
                {
                    return false;
                }

                clamped = !SerializedValuesEqual(
                    previousValue,
                    restoredValue,
                    typeof(T));
                entry.Value = (T)restoredValue;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsAlwaysExcluded(
            ConfigDefinition definition,
            ICollection<ConfigDefinition> permanentExclusions)
        {
            return definition == null
                || string.Equals(
                    definition.Key,
                    "ConfigSchemaVersion",
                    StringComparison.Ordinal)
                || string.Equals(
                    definition.Section,
                    RecoverySection,
                    StringComparison.Ordinal)
                || (permanentExclusions != null
                    && permanentExclusions.Contains(definition));
        }

        private BackupConfig FindLatestSupportedBackup()
        {
            string configPath = _config.ConfigFilePath;
            string directory = Path.GetDirectoryName(configPath);
            string fileName = Path.GetFileName(configPath);
            if (string.IsNullOrEmpty(directory)
                || string.IsNullOrEmpty(fileName)
                || !Directory.Exists(directory))
            {
                return null;
            }

            FileInfo[] files = new DirectoryInfo(directory)
                .GetFiles(fileName + ".pre-schema-*.bak")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                BackupConfig backup;
                if (TryReadBackup(files[i].FullName, out backup)
                    && backup.Schema >= _minimumSupportedBackupSchema
                    && backup.Schema < _currentSchema)
                {
                    return backup;
                }
            }

            return null;
        }

        private static bool TryReadBackup(
            string path,
            out BackupConfig backup)
        {
            backup = null;
            try
            {
                Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>
                    settings =
                        new Dictionary<
                            ConfigDefinition,
                            ConfigRecoveryStoredSetting>();
                string currentSection = string.Empty;
                string pendingType = null;
                string pendingDefault = null;
                bool hasPendingType = false;
                bool hasPendingDefault = false;
                int schema = 0;

                foreach (string rawLine in File.ReadLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith(
                        "# Setting type:",
                        StringComparison.Ordinal))
                    {
                        pendingType = line.Substring(
                            "# Setting type:".Length).Trim();
                        hasPendingType = true;
                        continue;
                    }

                    if (line.StartsWith(
                        "# Default value:",
                        StringComparison.Ordinal))
                    {
                        pendingDefault = line.Substring(
                            "# Default value:".Length).Trim();
                        hasPendingDefault = true;
                        continue;
                    }

                    if (line.Length > 1
                        && line[0] == '['
                        && line[line.Length - 1] == ']')
                    {
                        currentSection = line.Substring(
                            1,
                            line.Length - 2);
                        pendingType = null;
                        pendingDefault = null;
                        hasPendingType = false;
                        hasPendingDefault = false;
                        continue;
                    }

                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();
                    ConfigDefinition definition =
                        new ConfigDefinition(currentSection, key);
                    settings[definition] =
                        new ConfigRecoveryStoredSetting(
                            definition,
                            pendingType,
                            hasPendingType,
                            pendingDefault,
                            hasPendingDefault,
                            value);
                    if (string.Equals(
                        key,
                        "ConfigSchemaVersion",
                        StringComparison.Ordinal))
                    {
                        Int32.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out schema);
                    }

                    pendingType = null;
                    pendingDefault = null;
                    hasPendingType = false;
                    hasPendingDefault = false;
                }

                backup = new BackupConfig(path, schema, settings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string CreatePreImportBackup()
        {
            string basePath = _config.ConfigFilePath
                + ".pre-import-"
                + DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss-fff",
                    CultureInfo.InvariantCulture)
                + ".bak";
            string backupPath = basePath;
            int suffix = 1;
            while (File.Exists(backupPath))
            {
                backupPath = basePath
                    + "."
                    + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            File.Copy(_config.ConfigFilePath, backupPath, false);
            return backupPath;
        }

        internal static ConfigRecoveryValueStatus GetValueStatus(
            ConfigRecoveryStoredSetting setting,
            Type expectedType,
            out object previousValue)
        {
            previousValue = null;
            if (setting == null
                || expectedType == null
                || !setting.HasType
                || !string.Equals(
                    setting.TypeName,
                    expectedType.Name,
                    StringComparison.Ordinal))
            {
                return ConfigRecoveryValueStatus.IncompatibleType;
            }

            if (!setting.HasDefault)
            {
                return ConfigRecoveryValueStatus.MissingDefault;
            }

            object previousDefault;
            if (!TryConvert(
                    setting.DefaultValue,
                    expectedType,
                    out previousDefault)
                || !TryConvert(
                    setting.Value,
                    expectedType,
                    out previousValue))
            {
                previousValue = null;
                return ConfigRecoveryValueStatus.Invalid;
            }

            return SerializedValuesEqual(
                    previousDefault,
                    previousValue,
                    expectedType)
                ? ConfigRecoveryValueStatus.UntouchedDefault
                : ConfigRecoveryValueStatus.Customized;
        }

        private static bool TryConvert(
            string serializedValue,
            Type settingType,
            out object value)
        {
            value = null;
            try
            {
                object parsed =
                    TomlTypeConverter.ConvertToValue(
                        serializedValue,
                        settingType);
                float floatValue;
                double doubleValue;
                if ((parsed is float
                        && (Single.IsNaN(floatValue = (float)parsed)
                            || Single.IsInfinity(floatValue)))
                    || (parsed is double
                        && (Double.IsNaN(doubleValue = (double)parsed)
                            || Double.IsInfinity(doubleValue))))
                {
                    return false;
                }

                value = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SerializedValuesEqual(
            object left,
            object right,
            Type settingType)
        {
            try
            {
                return string.Equals(
                    TomlTypeConverter.ConvertToString(left, settingType),
                    TomlTypeConverter.ConvertToString(right, settingType),
                    StringComparison.Ordinal);
            }
            catch
            {
                return Equals(left, right);
            }
        }

        private static void TryRefreshFoAModManager()
        {
            try
            {
                Assembly[] assemblies =
                    AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type apiType = assemblies[i].GetType(
                        "FoAModManager.FoAModManagerApi",
                        false);
                    if (apiType == null)
                    {
                        continue;
                    }

                    MethodInfo refreshMethod = apiType.GetMethod(
                        "Refresh",
                        BindingFlags.Public | BindingFlags.Static);
                    if (refreshMethod != null)
                    {
                        refreshMethod.Invoke(null, null);
                    }
                    return;
                }
            }
            catch
            {
            }
        }

        private sealed class BackupConfig
        {
            internal BackupConfig(
                string path,
                int schema,
                Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>
                    settings)
            {
                Path = path;
                Schema = schema;
                Settings = settings;
            }

            internal string Path { get; private set; }
            internal int Schema { get; private set; }
            internal Dictionary<ConfigDefinition, ConfigRecoveryStoredSetting>
                Settings { get; private set; }
        }

        private sealed class PendingChange
        {
            internal PendingChange(
                ConfigEntryBase entry,
                object value)
            {
                Entry = entry;
                Value = value;
            }

            internal ConfigEntryBase Entry { get; private set; }
            internal object Value { get; private set; }
        }

        private sealed class AppliedChange
        {
            internal AppliedChange(
                ConfigEntryBase entry,
                object previousValue)
            {
                Entry = entry;
                PreviousValue = previousValue;
            }

            internal ConfigEntryBase Entry { get; private set; }
            internal object PreviousValue { get; private set; }
        }

        private sealed class ImportSummary
        {
            internal readonly List<PendingChange> Changes =
                new List<PendingChange>();
            internal int Clamped;
            internal int InternalOrAction;
            internal int RemovedOrRenamed;
            internal int TransitionBlocked;
            internal int IncompatibleType;
            internal int UnknownPreviousDefault;
            internal int Invalid;
            internal int UntouchedPreviousDefault;
            internal int AlreadyCurrent;

            internal string DescribeSkips()
            {
                return "keptNewDefault="
                    + UntouchedPreviousDefault.ToString(
                        CultureInfo.InvariantCulture)
                    + "; transitionBlocked="
                    + TransitionBlocked.ToString(
                        CultureInfo.InvariantCulture)
                    + "; removedOrRenamed="
                    + RemovedOrRenamed.ToString(
                        CultureInfo.InvariantCulture)
                    + "; incompatibleType="
                    + IncompatibleType.ToString(
                        CultureInfo.InvariantCulture)
                    + "; missingPreviousDefault="
                    + UnknownPreviousDefault.ToString(
                        CultureInfo.InvariantCulture)
                    + "; invalid="
                    + Invalid.ToString(CultureInfo.InvariantCulture)
                    + "; internalOrAction="
                    + InternalOrAction.ToString(
                        CultureInfo.InvariantCulture)
                    + "; alreadyCurrent="
                    + AlreadyCurrent.ToString(
                        CultureInfo.InvariantCulture);
            }
        }
    }
}
