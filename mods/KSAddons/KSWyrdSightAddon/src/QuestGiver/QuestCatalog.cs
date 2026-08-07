using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Awaken.TG.Main.Localization;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Templates;
using Awaken.TG.Main.Stories.Quests.UI;
using Awaken.TG.Main.Templates;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public sealed class QuestCatalog
	{
		public readonly List<QuestRow> Quests = new List<QuestRow>();

		public readonly List<QuestRow> Achievements = new List<QuestRow>();

		public readonly Dictionary<string, QuestRow> ByGuid = new Dictionary<string, QuestRow>(StringComparer.OrdinalIgnoreCase);

		public static bool IsGrantableQuest(QuestTemplateBase t)
		{
			return t is QuestTemplate;
		}

		internal IEnumerator BuildJob(TemplatesProvider tp, GameplayMemory memory, ManualLogSourceShim log, ScanReport report, int maxMillisPerFrame)
		{
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			IEnumerable<ITemplate> allTemplates;
			try
			{
				allTemplates = tp.AllTemplates;
			}
			catch (Exception ex)
			{
				log.Error("template enumeration failed: " + ex);
				report.SetupFailures.Add("quest catalogue: AllTemplates threw " + ex.GetType().Name);
				yield break;
			}
			Stopwatch slice = Stopwatch.StartNew();
			foreach (ITemplate item in allTemplates)
			{
				QuestTemplateBase val = (QuestTemplateBase)(object)((item is QuestTemplateBase) ? item : null);
				if ((Object)(object)val == (Object)null)
				{
					if (slice.ElapsedMilliseconds >= maxMillisPerFrame)
					{
						yield return null;
						slice.Restart();
					}
					continue;
				}
				QuestRow questRow = new QuestRow();
				questRow.Guid = ((Template)val).GUID;
				questRow.Name = SafeQuestName(val);
				questRow.IsAchievement = val is AchievementTemplate;
				try
				{
					questRow.Type = val.TypeOfQuest;
				}
				catch (Exception)
				{
					questRow.Type = (QuestType)4;
				}
				try
				{
					questRow.TemplateType = ((Template)val).TemplateType;
				}
				catch (Exception)
				{
					questRow.TemplateType = (TemplateType)0;
				}
				QuestTemplate val2 = (QuestTemplate)(object)((val is QuestTemplate) ? val : null);
				if ((Object)(object)val2 != (Object)null)
				{
					try
					{
						questRow.Category = val2.QuestCategory;
					}
					catch (Exception)
					{
						questRow.Category = (QuestCategory)0;
					}
				}
				questRow.State = SafeState(memory, questRow.Guid);
				if (!string.IsNullOrEmpty(questRow.Guid) && !ByGuid.ContainsKey(questRow.Guid))
				{
					ByGuid.Add(questRow.Guid, questRow);
				}
				if (IsGrantableQuest(val))
				{
					Quests.Add(questRow);
				}
				else
				{
					Achievements.Add(questRow);
				}
				if (slice.ElapsedMilliseconds >= maxMillisPerFrame)
				{
					yield return null;
					slice.Restart();
				}
			}
		}

		private static QuestState SafeState(GameplayMemory memory, string guid)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected O, but got Unknown
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			if (memory == null || string.IsNullOrEmpty(guid))
			{
				return (QuestState)0;
			}
			try
			{
				return QuestUtils.StateOfQuestWithId((IMemory)(object)memory, new TemplateReference(guid));
			}
			catch (Exception)
			{
				return (QuestState)0;
			}
		}

		public static string SafeQuestName(QuestTemplateBase t)
		{
			if ((Object)(object)t == (Object)null)
			{
				return "<null>";
			}
			try
			{
				LocString displayName = t.displayName;
				if (displayName != null)
				{
					string text = displayName.Translate();
					if (!string.IsNullOrWhiteSpace(text))
					{
						return text;
					}
					if (!string.IsNullOrWhiteSpace(displayName.FinalId))
					{
						return displayName.FinalId;
					}
				}
			}
			catch (Exception)
			{
			}
			try
			{
				return ((Template)t).DebugName;
			}
			catch (Exception)
			{
				return "<unnamed>";
			}
		}
	}
}
