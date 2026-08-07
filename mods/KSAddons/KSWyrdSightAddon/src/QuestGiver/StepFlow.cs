using System;
using System.Collections.Generic;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps;

namespace AvalonUntold
{
	public static class StepFlow
	{
		public static void Edges(StoryStep step, List<FlowEdge> into, ScanCounters counters)
		{
			//IL_0141: Unknown result type (might be due to invalid IL or missing references)
			//IL_0147: Expected O, but got Unknown
			//IL_0321: Unknown result type (might be due to invalid IL or missing references)
			//IL_0332: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_021e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0383: Unknown result type (might be due to invalid IL or missing references)
			//IL_038a: Expected O, but got Unknown
			//IL_0256: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0194: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Expected O, but got Unknown
			//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_023a: Unknown result type (might be due to invalid IL or missing references)
			//IL_028f: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0272: Unknown result type (might be due to invalid IL or missing references)
			//IL_015f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0165: Expected O, but got Unknown
			//IL_036a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0202: Unknown result type (might be due to invalid IL or missing references)
			//IL_0304: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
			into.Clear();
			if (step == null)
			{
				return;
			}
			byte type;
			try
			{
				type = step.Type;
			}
			catch (Exception)
			{
				if (counters != null)
				{
					counters.StepFlowThrows++;
				}
				return;
			}
			try
			{
				switch (type)
				{
				case 168:
				{
					SStoryStartChoice val3 = (SStoryStartChoice)step;
					Chapter(into, val3.targetChapter, "SStoryStartChoice.targetChapter", type);
					return;
				}
				case 31:
				case 32:
				case 33:
				{
					SChoice val2 = (SChoice)step;
					Chapter(into, val2.targetChapter, "SChoice.targetChapter", type);
					Chapter(into, val2.choice.targetChapter, "SChoice.choice.targetChapter", type);
					return;
				}
				case 135:
				{
					SPopupChoice val = (SPopupChoice)step;
					Chapter(into, val.targetChapter, "SPopupChoice.targetChapter", type);
					Chapter(into, val.choice.targetChapter, "SPopupChoice.choice.targetChapter", type);
					return;
				}
				case 98:
					Chapter(into, ((SNodeJump)step).targetChapter, "SNodeJump.targetChapter", type);
					return;
				case 137:
					Chapter(into, ((SRandomPick)step).targetChapter, "SRandomPick.targetChapter", type);
					return;
				case 161:
					Chapter(into, ((SStatDependantChoice)step).successChapter, "SStatDependantChoice.successChapter", type);
					return;
				case 70:
					Chapter(into, ((SHasAchievement)step).targetChapter, "SHasAchievement.targetChapter", type);
					return;
				case 117:
					Chapter(into, ((SOpenGemsUI)step).targetChapter, "SOpenGemsUI.targetChapter", type);
					return;
				case 28:
					Chapter(into, ((SChangeItemsQuantity)step).leaveChapter, "SChangeItemsQuantity.leaveChapter", type);
					return;
				case 68:
					Bookmark(into, ((SGraphJump)step).bookmark, "SGraphJump.bookmark", type, selfTargeted: true);
					return;
				case 90:
					Bookmark(into, ((SLocationStartStory)step).bookmark, "SLocationStartStory.bookmark", type, IsSelf(((SLocationStartStory)step).locationReference));
					return;
				case 89:
					Bookmark(into, ((SLocationRunUnobserved)step).targetStory, "SLocationRunUnobserved.targetStory", type, selfTargeted: false);
					return;
				case 85:
					Bookmark(into, ((SLocationMakeBusy)step).busyStory, "SLocationMakeBusy.busyStory", type, IsSelf(((SLocationMakeBusy)step).locationReference));
					return;
				case 119:
					Bookmark(into, ((SOpenHouseUnlock)step).storyOnUnlock, "SOpenHouseUnlock.storyOnUnlock", type, selfTargeted: false);
					return;
				case 124:
					Bookmark(into, ((SPerformInteraction)step).callback, "SPerformInteraction.callback", type, IsSelf(((SPerformInteraction)step).locations));
					return;
				case 171:
					Bookmark(into, ((STeleportHeroOnHeroDeath)step).bookmark, "STeleportHeroOnHeroDeath.bookmark", type, selfTargeted: false);
					return;
				case 206:
					Bookmark(into, ((SWaitForPopupDiscard)step).bookmark, "SWaitForPopupDiscard.bookmark", type, selfTargeted: true);
					return;
				case 45:
				{
					SDuelCreate val4 = (SDuelCreate)step;
					Bookmark(into, val4.callbackOnGroup0Victory, "SDuelCreate.callbackOnGroup0Victory", type, selfTargeted: false);
					Bookmark(into, val4.callbackOnGroup1Victory, "SDuelCreate.callbackOnGroup1Victory", type, selfTargeted: false);
					return;
				}
				case 43:
					Bookmark(into, ((SDuelAddNewGroup)step).callbackOnGroupVictory, "SDuelAddNewGroup.callbackOnGroupVictory", type, selfTargeted: false);
					return;
				case 2:
				case 16:
				case 77:
				case 136:
				case 138:
					return;
				}
				if (counters != null)
				{
					if (type > 209)
					{
						ScanCounters.Bump(counters.UnknownStepTypeBytes, type);
					}
					else
					{
						ScanCounters.Bump(counters.StepTypesWithoutEdgeMapping, type);
					}
				}
			}
			catch (Exception)
			{
				if (counters != null)
				{
					counters.StepFlowThrows++;
				}
			}
		}

		private static bool IsSelf(LocationReference reference)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			if (reference == null)
			{
				return true;
			}
			try
			{
				return (int)reference.targetTypes == 0;
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static void Chapter(List<FlowEdge> into, StoryChapter target, string kind, byte sourceType)
		{
			if (target != null)
			{
				into.Add(new FlowEdge
				{
					TargetChapter = target,
					Kind = kind,
					SourceType = sourceType
				});
			}
		}

		private static void Bookmark(List<FlowEdge> into, StoryBookmark bookmark, string kind, byte sourceType, bool selfTargeted)
		{
			if (!(bookmark == (StoryBookmark)null) && bookmark.IsValid)
			{
				into.Add(new FlowEdge
				{
					Bookmark = bookmark,
					Kind = kind,
					SourceType = sourceType,
					SelfTargeted = selfTargeted
				});
			}
		}
	}
}
