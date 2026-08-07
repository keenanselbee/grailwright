using System;
using System.Collections.Generic;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Objectives;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps;
using Awaken.TG.Main.Templates;

namespace AvalonUntold
{
	internal static class SelfSetGates
	{
		private const int MaxState = 31;

		internal static string FlagKey(string flag)
		{
			return "F|" + flag;
		}

		internal static string ObjectiveKey(string quest, string obj)
		{
			return "O|" + quest + "|" + obj;
		}

		internal static string QuestKey(string quest)
		{
			return "Q|" + quest;
		}

		internal static bool CollectWrites(StoryStep step, Dictionary<string, int> into)
		{
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Expected I4, but got Unknown
			if (step == null || into == null)
			{
				return false;
			}
			try
			{
				SFlagChange val = (SFlagChange)(object)((step is SFlagChange) ? step : null);
				if (val != null)
				{
					return Add(into, FlagKey(val.flag), val.newState ? 1 : 0, val.flag);
				}
				SFlagChangeDelayed val2 = (SFlagChangeDelayed)(object)((step is SFlagChangeDelayed) ? step : null);
				if (val2 != null)
				{
					return Add(into, FlagKey(val2.flag), val2.newState ? 1 : 0, val2.flag);
				}
				SObjectiveChange val3 = (SObjectiveChange)(object)((step is SObjectiveChange) ? step : null);
				if (val3 != null)
				{
					if (val3.questRef == (TemplateReference)null || !val3.questRef.IsSet || string.IsNullOrEmpty(val3.objectiveGuid))
					{
						return false;
					}
					return Add(into, ObjectiveKey(val3.questRef.GUID, val3.objectiveGuid), (int)val3.newState, val3.objectiveGuid);
				}
				SQuestAdd val4 = (SQuestAdd)(object)((step is SQuestAdd) ? step : null);
				if (val4 != null)
				{
					if (val4.questRef == (TemplateReference)null || !val4.questRef.IsSet)
					{
						return false;
					}
					return Add(into, QuestKey(val4.questRef.GUID), 1, val4.questRef.GUID);
				}
				SQuestComplete val5 = (SQuestComplete)(object)((step is SQuestComplete) ? step : null);
				if (val5 != null)
				{
					if (val5.questTemplate == (TemplateReference)null || !val5.questTemplate.IsSet)
					{
						return false;
					}
					return Add(into, QuestKey(val5.questTemplate.GUID), 2, val5.questTemplate.GUID);
				}
				SQuestFail val6 = (SQuestFail)(object)((step is SQuestFail) ? step : null);
				if (val6 != null)
				{
					if (val6.questTemplate == (TemplateReference)null || !val6.questTemplate.IsSet)
					{
						return false;
					}
					return Add(into, QuestKey(val6.questTemplate.GUID), 3, val6.questTemplate.GUID);
				}
			}
			catch (Exception)
			{
				return false;
			}
			return false;
		}

		private static bool Add(Dictionary<string, int> into, string key, int state, string namePart)
		{
			if (string.IsNullOrEmpty(namePart) || state < 0 || state > 31)
			{
				return false;
			}
			into.TryGetValue(key, out var value);
			into[key] = value | (1 << state);
			return true;
		}

		internal static bool WriteFlipsAnswer(Dictionary<string, int> writes, string key, int required, int current)
		{
			if (writes == null || key == null)
			{
				return false;
			}
			if (!writes.TryGetValue(key, out var value) || value == 0)
			{
				return false;
			}
			if (required < 0 || required > 31)
			{
				return false;
			}
			int num = 1 << required;
			if (current == required)
			{
				return (value & ~num) != 0;
			}
			return (value & num) != 0;
		}
	}
}
