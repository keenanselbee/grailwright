using System;
using System.Collections.Generic;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Fights.NPCs.Presences;
using Awaken.TG.Main.General.StatTypes;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Actors;
using Awaken.TG.Main.Stories.Conditions;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.Objectives;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.Timing;
using Awaken.Utility.Times;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public sealed class ConditionEvaluator
	{
		private static readonly bool[] Safe;

		private static readonly bool[] Volatile;

		public const string VolatileSuffix = "(volatile)";

		private readonly GameplayMemory _memory;

		private readonly ScanCounters _counters;

		private readonly Dictionary<string, bool> _locationMatchNonEmpty = new Dictionary<string, bool>();

		private string _graphGuid;

		private bool _graphShared;

		private string _ownerContextId;

		public const string PartialLocationSetSuffix = "(partialLocationSet)";

		public int LocationCacheEntries => _locationMatchNonEmpty.Count;

		static ConditionEvaluator()
		{
			Safe = new bool[256];
			Volatile = new bool[256];
			byte[] array = new byte[20]
			{
				1, 2, 3, 4, 5, 6, 7, 8, 9, 11,
				12, 13, 17, 18, 20, 22, 23, 26, 28, 30
			};
			for (int i = 0; i < array.Length; i++)
			{
				Safe[array[i]] = true;
			}
			byte[] array2 = new byte[13]
			{
				1, 7, 8, 9, 10, 11, 12, 13, 18, 19,
				20, 21, 28
			};
			for (int j = 0; j < array2.Length; j++)
			{
				Volatile[array2[j]] = true;
			}
		}

		public static bool MixesVolatileAndPermanent(List<string> causes)
		{
			if (causes == null || causes.Count < 2)
			{
				return false;
			}
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < causes.Count; i++)
			{
				string text = causes[i];
				if (text != null)
				{
					if (text.EndsWith("(volatile)", StringComparison.Ordinal))
					{
						flag = true;
					}
					else
					{
						flag2 = true;
					}
					if (flag && flag2)
					{
						return true;
					}
				}
			}
			return false;
		}

		public void InvalidateLocationCache()
		{
			_locationMatchNonEmpty.Clear();
		}

		public ConditionEvaluator(GameplayMemory memory, ScanCounters counters)
		{
			_memory = memory;
			_counters = counters;
		}

		public void BeginGraph(string graphGuid, bool shared, string ownerContextId)
		{
			_graphGuid = graphGuid;
			_graphShared = shared;
			_ownerContextId = ownerContextId;
		}

		public TriResult EvaluateStepConditions(StoryStep step)
		{
			if (step == null)
			{
				return TriResult.Unknown("NullStep");
			}
			return EvaluateInputs(step.conditions, step);
		}

		public TriResult EvaluateInputs(StoryConditionInput[] inputs, StoryStep step)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			TriResult triResult = TriResult.True();
			if (inputs == null)
			{
				return triResult;
			}
			for (int i = 0; i < inputs.Length; i++)
			{
				triResult = TriResult.And(triResult, EvaluateInput(inputs[i], step));
			}
			return triResult;
		}

		public TriResult EvaluateInput(StoryConditionInput input, StoryStep step)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			if (input.conditions == null)
			{
				return TriResult.Unknown("NullConditionsNode");
			}
			TriResult triResult = EvaluateGroup(input.conditions, step);
			if (!input.negate)
			{
				return triResult;
			}
			TriResult triResult2 = TriResult.Not(triResult);
			if (triResult.Value == Tri.True)
			{
				triResult2 = TriResult.WithDecisiveFalses(triResult2, MarkAllVolatile(NegatedOrigins(input.conditions, 0)));
			}
			return triResult2;
		}

		public static List<string> StepConditionOrigins(StoryStep step, string prefix)
		{
			if (step == null)
			{
				return null;
			}
			StoryConditionInput[] conditions;
			try
			{
				conditions = step.conditions;
			}
			catch (Exception)
			{
				return null;
			}
			if (conditions == null || conditions.Length == 0)
			{
				return null;
			}
			List<string> list = null;
			for (int i = 0; i < conditions.Length; i++)
			{
				List<string> list2 = LeafNames(conditions[i].conditions, 0);
				if (list2 == null)
				{
					continue;
				}
				if (list == null)
				{
					list = new List<string>(2);
				}
				for (int j = 0; j < list2.Count; j++)
				{
					string item = prefix + list2[j];
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
			return list;
		}

		public static List<string> MarkAllVolatile(List<string> names)
		{
			if (names == null || names.Count == 0)
			{
				return names;
			}
			bool flag = false;
			for (int i = 0; i < names.Count; i++)
			{
				if (names[i] != null && names[i].EndsWith("(volatile)", StringComparison.Ordinal))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return names;
			}
			for (int j = 0; j < names.Count; j++)
			{
				string text = names[j];
				if (text != null && !text.EndsWith("(volatile)", StringComparison.Ordinal))
				{
					names[j] = text + "(volatile)";
				}
			}
			return names;
		}

		public bool StepGateSelfSettable(StoryStep step, Dictionary<string, int> writes)
		{
			if (step == null || writes == null || writes.Count == 0)
			{
				return false;
			}
			StoryConditionInput[] conditions;
			try
			{
				conditions = step.conditions;
			}
			catch (Exception)
			{
				return true;
			}
			if (conditions == null || conditions.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < conditions.Length; i++)
			{
				if (GroupSelfSettable(conditions[i].conditions, writes, 0))
				{
					return true;
				}
			}
			return false;
		}

		private bool GroupSelfSettable(StoryConditions group, Dictionary<string, int> writes, int depth)
		{
			if (group == null || depth > 1)
			{
				return false;
			}
			try
			{
				if (group.conditions != null)
				{
					for (int i = 0; i < group.conditions.Length; i++)
					{
						if (LeafSelfSettable(group.conditions[i], writes))
						{
							return true;
						}
					}
				}
				if (group.inputs != null)
				{
					for (int j = 0; j < group.inputs.Length; j++)
					{
						if (GroupSelfSettable(group.inputs[j].conditions, writes, depth + 1))
						{
							return true;
						}
					}
				}
			}
			catch (Exception)
			{
				return true;
			}
			return false;
		}

		private bool LeafSelfSettable(StoryCondition cond, Dictionary<string, int> writes)
		{
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_0135: Unknown result type (might be due to invalid IL or missing references)
			//IL_013f: Expected I4, but got Unknown
			//IL_013f: Expected I4, but got Unknown
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Expected I4, but got Unknown
			//IL_00d7: Expected I4, but got Unknown
			if (cond == null)
			{
				return false;
			}
			byte type;
			try
			{
				type = cond.Type;
			}
			catch (Exception)
			{
				return true;
			}
			try
			{
				switch (type)
				{
				case 4:
				{
					CFlag val2 = (CFlag)(object)((cond is CFlag) ? cond : null);
					if (val2 == null || string.IsNullOrEmpty(val2.flag))
					{
						return false;
					}
					return SelfSetGates.WriteFlipsAnswer(writes, SelfSetGates.FlagKey(val2.flag), 1, StoryFlags.Get(val2.flag) ? 1 : 0);
				}
				case 31:
				{
					CQuestObjective val3 = (CQuestObjective)(object)((cond is CQuestObjective) ? cond : null);
					if (val3 == null || val3.questRef == (TemplateReference)null || !val3.questRef.IsSet || string.IsNullOrEmpty(val3.objectiveGuid))
					{
						return false;
					}
					return SelfSetGates.WriteFlipsAnswer(writes, SelfSetGates.ObjectiveKey(val3.questRef.GUID, val3.objectiveGuid), (int)val3.requiredState, (int)QuestUtils.StateOfObjective((IMemory)(object)_memory, val3.questRef, val3.objectiveGuid));
				}
				case 32:
				{
					CQuestState val = (CQuestState)(object)((cond is CQuestState) ? cond : null);
					if (val == null || val.questRef == (TemplateReference)null || !val.questRef.IsSet)
					{
						return false;
					}
					return SelfSetGates.WriteFlipsAnswer(writes, SelfSetGates.QuestKey(val.questRef.GUID), (int)val.requiredState, (int)QuestUtils.StateOfQuestWithId((IMemory)(object)_memory, val.questRef));
				}
				}
			}
			catch (Exception)
			{
				_counters.ConditionThrows++;
				return true;
			}
			return false;
		}

		private static List<string> LeafNames(StoryConditions group, int depth)
		{
			if (group == null || depth > 1)
			{
				return null;
			}
			List<string> list = null;
			if (group.conditions != null)
			{
				for (int i = 0; i < group.conditions.Length; i++)
				{
					StoryCondition val = group.conditions[i];
					if (val != null)
					{
						string item;
						try
						{
							item = ((object)val).GetType().Name + (Volatile[val.Type] ? "(volatile)" : "");
						}
						catch (Exception)
						{
							continue;
						}
						if (list == null)
						{
							list = new List<string>(2);
						}
						if (!list.Contains(item))
						{
							list.Add(item);
						}
					}
				}
			}
			if (group.inputs != null)
			{
				for (int j = 0; j < group.inputs.Length; j++)
				{
					List<string> list2 = LeafNames(group.inputs[j].conditions, depth + 1);
					if (list2 == null)
					{
						continue;
					}
					if (list == null)
					{
						list = new List<string>(2);
					}
					for (int k = 0; k < list2.Count; k++)
					{
						if (!list.Contains(list2[k]))
						{
							list.Add(list2[k]);
						}
					}
				}
			}
			return list;
		}

		private static List<string> NegatedOrigins(StoryConditions group, int depth)
		{
			if (group == null || depth > 1)
			{
				return null;
			}
			List<string> list = null;
			if (group.conditions != null)
			{
				for (int i = 0; i < group.conditions.Length; i++)
				{
					StoryCondition val = group.conditions[i];
					if (val != null)
					{
						string item;
						try
						{
							item = "negated:" + ((object)val).GetType().Name + (Volatile[val.Type] ? "(volatile)" : "");
						}
						catch (Exception)
						{
							continue;
						}
						if (list == null)
						{
							list = new List<string>(2);
						}
						if (!list.Contains(item))
						{
							list.Add(item);
						}
					}
				}
			}
			if (group.inputs != null)
			{
				for (int j = 0; j < group.inputs.Length; j++)
				{
					List<string> list2 = NegatedOrigins(group.inputs[j].conditions, depth + 1);
					if (list2 == null)
					{
						continue;
					}
					if (list == null)
					{
						list = new List<string>(2);
					}
					for (int k = 0; k < list2.Count; k++)
					{
						if (!list.Contains(list2[k]))
						{
							list.Add(list2[k]);
						}
					}
				}
			}
			return list;
		}

		public TriResult EvaluateGroup(StoryConditions group, StoryStep step)
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			if (group == null)
			{
				return TriResult.Unknown("NullConditionsNode");
			}
			byte type;
			try
			{
				type = group.Type;
			}
			catch (Exception ex)
			{
				return TriResult.Unknown("GroupType(threw:" + ex.GetType().Name + ")");
			}
			switch (type)
			{
			case 0:
			{
				TriResult triResult4 = TriResult.True();
				if (group.inputs != null)
				{
					for (int k = 0; k < group.inputs.Length; k++)
					{
						triResult4 = TriResult.And(triResult4, EvaluateInput(group.inputs[k], step));
					}
				}
				if (group.conditions != null)
				{
					for (int l = 0; l < group.conditions.Length; l++)
					{
						triResult4 = TriResult.And(triResult4, EvaluateLeaf(group.conditions[l], step));
					}
				}
				return triResult4;
			}
			case 1:
			{
				if (((group.inputs != null) ? group.inputs.Length : 0) + ((group.conditions != null) ? group.conditions.Length : 0) == 0)
				{
					return TriResult.False("EmptyOrGroup");
				}
				TriResult triResult = default(TriResult);
				bool flag = true;
				if (group.inputs != null)
				{
					for (int i = 0; i < group.inputs.Length; i++)
					{
						TriResult triResult2 = EvaluateInput(group.inputs[i], step);
						triResult = (flag ? triResult2 : TriResult.Or(triResult, triResult2));
						flag = false;
					}
				}
				if (group.conditions != null)
				{
					for (int j = 0; j < group.conditions.Length; j++)
					{
						TriResult triResult3 = EvaluateLeaf(group.conditions[j], step);
						triResult = (flag ? triResult3 : TriResult.Or(triResult, triResult3));
						flag = false;
					}
				}
				return triResult;
			}
			default:
				ScanCounters.Bump(_counters.UnrecognisedGroupTypes, type);
				return TriResult.Unknown("UnknownGroupType(" + type + ")");
			}
		}

		public TriResult EvaluateLeaf(StoryCondition cond, StoryStep step)
		{
			if (cond == null)
			{
				return TriResult.Unknown("NullCondition");
			}
			_counters.ConditionEvaluations++;
			byte type;
			try
			{
				type = cond.Type;
			}
			catch (Exception ex)
			{
				return TriResult.Unknown("ConditionType(threw:" + ex.GetType().Name + ")");
			}
			switch (type)
			{
			case 0:
				return TriResult.Unknown("CCanPayBounty");
			case 29:
				return TriResult.Unknown("CVariable");
			case 27:
				return EvalRandom((CRandom)(object)((cond is CRandom) ? cond : null));
			case 24:
				return EvalOncePerCondition((COncePer)(object)((cond is COncePer) ? cond : null));
			case 10:
				return EvalHasStats((CHasStats)(object)((cond is CHasStats) ? cond : null), step);
			case 21:
				return EvalIsUnconscious((CIsUnconscious)(object)((cond is CIsUnconscious) ? cond : null), step);
			case 16:
				return EvalIsDead((CIsDead)(object)((cond is CIsDead) ? cond : null), step);
			case 14:
				return EvalLocationBased(cond, step, Ref((CHeroMountOwner)(object)((cond is CHeroMountOwner) ? cond : null)), "CHeroMountOwner", anyShaped: true, 14);
			case 15:
				return EvalLocationBased(cond, step, Ref((CIsAnyLocation)(object)((cond is CIsAnyLocation) ? cond : null)), "CIsAnyLocation", anyShaped: true, 15);
			case 19:
				return EvalLocationBased(cond, step, Ref((CIsLocationBusy)(object)((cond is CIsLocationBusy) ? cond : null)), "CIsLocationBusy", anyShaped: false, 19);
			case 25:
				return EvalLocationBased(cond, step, Ref((CPetStatus)(object)((cond is CPetStatus) ? cond : null)), "CPetStatus", anyShaped: true, 25);
			case 31:
				return EvalQuestObjective((CQuestObjective)(object)((cond is CQuestObjective) ? cond : null));
			case 32:
				return EvalQuestState((CQuestState)(object)((cond is CQuestState) ? cond : null));
			default:
				if (Safe[type])
				{
					return Delegate(cond, step, Volatile[type]);
				}
				ScanCounters.Bump(_counters.UnrecognisedConditionTypes, type);
				return TriResult.Unknown("UnknownConditionType(" + type + ")");
			}
		}

		private TriResult Delegate(StoryCondition cond, StoryStep step, bool isVolatile)
		{
			string name = ((object)cond).GetType().Name;
			try
			{
				if (cond.Fulfilled((Story)null, step))
				{
					return TriResult.True();
				}
				return TriResult.False(isVolatile ? (name + "(volatile)") : name);
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown(name + "(threw:" + ex.GetType().Name + ")");
			}
		}

		private static LocationReference Ref(CHeroMountOwner c)
		{
			return c?.locationRef;
		}

		private static LocationReference Ref(CIsAnyLocation c)
		{
			return c?.locationReference;
		}

		private static LocationReference Ref(CIsLocationBusy c)
		{
			return c?.locationReference;
		}

		private static LocationReference Ref(CPetStatus c)
		{
			return c?.locationRef;
		}

		private TriResult EvalLocationBased(StoryCondition cond, StoryStep step, LocationReference lr, string name, bool anyShaped, byte type)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			if (lr == null)
			{
				return TriResult.Unknown(name + "(nullLocationReference)");
			}
			TargetType targetTypes;
			try
			{
				targetTypes = lr.targetTypes;
			}
			catch (Exception ex)
			{
				return TriResult.Unknown(name + "(targetTypeThrew:" + ex.GetType().Name + ")");
			}
			if ((int)targetTypes == 0)
			{
				return TriResult.Unknown(name + "(Self)");
			}
			bool flag;
			try
			{
				flag = MatchSetNonEmpty(lr);
			}
			catch (Exception ex2)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown(name + "(matchThrew:" + ex2.GetType().Name + ")");
			}
			if (!flag)
			{
				return TriResult.Unknown(name + "(noMatchingLocationsLoaded)");
			}
			TriResult result = Delegate(cond, step, Volatile[type]);
			if (anyShaped)
			{
				if (result.Value == Tri.False)
				{
					return TriResult.Unknown(name + "(partialLocationSet)");
				}
			}
			else if (result.Value == Tri.True)
			{
				return TriResult.Unknown(name + "(partialLocationSet)");
			}
			return result;
		}

		private bool MatchSetNonEmpty(LocationReference lr)
		{
			string text = LocationKey(lr);
			if (text != null && _locationMatchNonEmpty.TryGetValue(text, out var value))
			{
				return value;
			}
			_counters.LocationRefMatchScans++;
			bool flag = false;
			using (IEnumerator<Location> enumerator = lr.MatchingLocations((Story)null).GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					_ = enumerator.Current;
					flag = true;
				}
			}
			if (text != null)
			{
				_locationMatchNonEmpty[text] = flag;
			}
			return flag;
		}

		private static string LocationKey(LocationReference lr)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Expected I4, but got Unknown
			try
			{
				StringBuilder stringBuilder = new StringBuilder(64);
				stringBuilder.Append((int)lr.targetTypes);
				stringBuilder.Append('|');
				if (lr.tags != null)
				{
					for (int i = 0; i < lr.tags.Length; i++)
					{
						stringBuilder.Append(lr.tags[i]).Append(',');
					}
				}
				stringBuilder.Append('|');
				if (lr.locationRefs != null)
				{
					for (int j = 0; j < lr.locationRefs.Length; j++)
					{
						stringBuilder.Append((lr.locationRefs[j] == (TemplateReference)null) ? "" : lr.locationRefs[j].GUID).Append(',');
					}
				}
				stringBuilder.Append('|');
				if (lr.actors != null)
				{
					for (int k = 0; k < lr.actors.Length; k++)
					{
						stringBuilder.Append(lr.actors[k].guid).Append(',');
					}
				}
				return stringBuilder.ToString();
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static TriResult EvalRandom(CRandom c)
		{
			if (c == null)
			{
				return TriResult.Unknown("CRandom(cast)");
			}
			if (c.chancePercentage >= 100)
			{
				return TriResult.True();
			}
			if (c.chancePercentage <= 0)
			{
				return TriResult.False("CRandom(0%)");
			}
			return TriResult.Unknown("CRandom(" + c.chancePercentage + "%)");
		}

		private TriResult EvalIsUnconscious(CIsUnconscious c, StoryStep step)
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			if (c == null)
			{
				return TriResult.Unknown("CIsUnconscious(cast)");
			}
			NpcRegistry val = default(NpcRegistry);
			try
			{
				if (World.Services == null || !World.Services.TryGet<NpcRegistry>(out val))
				{
					return TriResult.Unknown("CIsUnconscious(noRegistry)");
				}
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("CIsUnconscious(registryThrew:" + ex.GetType().Name + ")");
			}
			try
			{
				NpcElement val2 = default(NpcElement);
				if (!val.TryGetNpc(c.actorRef, out val2) || val2 == null)
				{
					return TriResult.Unknown("CIsUnconscious(actorNotLoaded)");
				}
			}
			catch (Exception ex2)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("CIsUnconscious(lookupThrew:" + ex2.GetType().Name + ")");
			}
			return Delegate((StoryCondition)(object)c, step, isVolatile: true);
		}

		private TriResult EvalIsDead(CIsDead c, StoryStep step)
		{
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			if (c == null)
			{
				return TriResult.Unknown("CIsDead(cast)");
			}
			try
			{
				if (World.Services == null)
				{
					return TriResult.Unknown("CIsDead(noServices)");
				}
				NpcRegistry val = default(NpcRegistry);
				if (!World.Services.TryGet<NpcRegistry>(out val) || val == null)
				{
					return TriResult.Unknown("CIsDead(noRegistry)");
				}
				ActorsRegister val2 = default(ActorsRegister);
				if (!World.Services.TryGet<ActorsRegister>(out val2) || (Object)(object)val2 == (Object)null)
				{
					return TriResult.Unknown("CIsDead(noActorsRegister)");
				}
				Actor actor = val2.GetActor((string)c.actorRef);
				if (string.IsNullOrEmpty(actor.TemplateGuid) || actor.TemplateGuid.Trim().Length == 0)
				{
					return TriResult.Unknown("CIsDead(actorNotResolvable)");
				}
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("CIsDead(lookupThrew:" + ex.GetType().Name + ")");
			}
			return Delegate((StoryCondition)(object)c, step, isVolatile: false);
		}

		private TriResult EvalHasStats(CHasStats c, StoryStep step)
		{
			if (c == null)
			{
				return TriResult.Unknown("CHasStats(cast)");
			}
			StatType statType;
			try
			{
				statType = c.StatType;
			}
			catch (Exception ex)
			{
				return TriResult.Unknown("CHasStats(statTypeThrew:" + ex.GetType().Name + ")");
			}
			if (statType == null)
			{
				return TriResult.Unknown("CHasStats(nullStatType)");
			}
			if (statType is NpcStatType)
			{
				return TriResult.Unknown("CHasStats(NpcStatType)");
			}
			return Delegate((StoryCondition)(object)c, step, isVolatile: true);
		}

		private TriResult EvalQuestState(CQuestState c)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			if (c == null)
			{
				return TriResult.Unknown("CQuestState(cast)");
			}
			if (c.questRef == (TemplateReference)null || !c.questRef.IsSet)
			{
				return TriResult.Unknown("CQuestState(unsetRef)");
			}
			try
			{
				return (QuestUtils.StateOfQuestWithId((IMemory)(object)_memory, c.questRef) == c.requiredState) ? TriResult.True() : TriResult.False("CQuestState");
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("CQuestState(threw:" + ex.GetType().Name + ")");
			}
		}

		private TriResult EvalQuestObjective(CQuestObjective c)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			if (c == null)
			{
				return TriResult.Unknown("CQuestObjective(cast)");
			}
			if (c.questRef == (TemplateReference)null || !c.questRef.IsSet)
			{
				return TriResult.Unknown("CQuestObjective(unsetRef)");
			}
			try
			{
				return (QuestUtils.StateOfObjective((IMemory)(object)_memory, c.questRef, c.objectiveGuid) == c.requiredState) ? TriResult.True() : TriResult.False("CQuestObjective");
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("CQuestObjective(threw:" + ex.GetType().Name + ")");
			}
		}

		private TriResult EvalOncePerCondition(COncePer c)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (c == null)
			{
				return TriResult.Unknown("COncePer(cast)");
			}
			return OncePer(c.flag, c.span);
		}

		public unsafe TriResult OncePer(string flag, TimeSpans span)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Invalid comparison between Unknown and I4
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Invalid comparison between Unknown and I4
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			if (flag == null)
			{
				return TriResult.True();
			}
			if ((int)span == 4)
			{
				return TriResult.True();
			}
			if ((int)span == 999)
			{
				return TriResult.True();
			}
			if ((int)span != 2)
			{
				return TriResult.Unknown("COncePer(volatile:" + ((object)(*(TimeSpans*)(&span))/*cast due to constrained. prefix*/).ToString() + ")");
			}
			if (_graphShared && _ownerContextId == null)
			{
				return TriResult.Unknown("COncePer(sharedGraph)");
			}
			try
			{
				int num = ((_graphShared && _ownerContextId != null) ? _memory.Context(new string[2] { _graphGuid, _ownerContextId }) : _memory.Context(_graphGuid)).Get<int>(flag, 0);
				return GameTimeUtil.HasTimeSpanChanged(World.Only<GameRealTime>().WeatherDaysSinceGameStart, num, span) ? TriResult.True() : TriResult.False("COncePer(Ever)");
			}
			catch (Exception ex)
			{
				_counters.ConditionThrows++;
				return TriResult.Unknown("COncePer(threw:" + ex.GetType().Name + ")");
			}
		}
	}
}
