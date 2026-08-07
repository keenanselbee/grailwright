using System;
using System.Collections.Generic;
using System.Diagnostics;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Templates;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public static class SuppressionAudit
	{
		public const int DefaultRowCap = 40;

		public static SuppressionAuditResult Run(QuestGiverIndex index, Dictionary<string, string> questNames, bool buildRows)
		{
			SuppressionAuditResult suppressionAuditResult = new SuppressionAuditResult();
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				if (index == null || !index.IsReady)
				{
					suppressionAuditResult.NotRunReason = "the quest index is not ready - no scan has published one yet, so nothing is glowing and nothing can be verified";
				}
				else if (!index.BoundToCurrentMemory())
				{
					suppressionAuditResult.NotRunReason = "the quest index is bound to a DIFFERENT save; every verdict in it describes another playthrough";
				}
				else
				{
					suppressionAuditResult.SuppressionOn = QuestGiverIndex.SuppressLockedQuests;
					suppressionAuditResult.PreciseMode = GlowController.CurrentMode() == GlowMode.Precise;
					suppressionAuditResult.ModeName = GlowController.CurrentMode().ToString();
					suppressionAuditResult.IndexWouldSuppressCount = index.WouldSuppressCount;
					suppressionAuditResult.IndexUnresolvedQuestRefs = index.UnresolvedQuestRefs;
					suppressionAuditResult.IndexEntryPairs = index.EntryPairs;
					suppressionAuditResult.IndexEntriesUnknownAtQuery = index.EntriesUnknownAtQuery;
					SitePass(index, suppressionAuditResult);
					NpcPass(index, questNames, buildRows, suppressionAuditResult);
					suppressionAuditResult.StructuralOk = suppressionAuditResult.NpcsLitSuppressionOn + suppressionAuditResult.NpcsHidden == suppressionAuditResult.NpcsLitSuppressionOff && suppressionAuditResult.NpcsLitSuppressionOn <= suppressionAuditResult.NpcsLitSuppressionOff;
					if (buildRows)
					{
						for (int i = 0; i < suppressionAuditResult.Hidden.Count; i++)
						{
							if (suppressionAuditResult.Hidden[i] != null && suppressionAuditResult.Hidden[i].Causes.Count == 0)
							{
								suppressionAuditResult.HiddenWithNoCause++;
							}
						}
					}
					suppressionAuditResult.Ran = true;
				}
			}
			catch (Exception ex)
			{
				suppressionAuditResult.Ran = false;
				suppressionAuditResult.NotRunReason = "the recount threw " + ex.GetType().Name + ": " + ex.Message;
			}
			stopwatch.Stop();
			suppressionAuditResult.ElapsedMs = stopwatch.ElapsedMilliseconds;
			return suppressionAuditResult;
		}

		private static void SitePass(QuestGiverIndex index, SuppressionAuditResult r)
		{
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Invalid comparison between Unknown and I4
			Dictionary<string, HashSet<string>> dictionary = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, List<QuestGrantSite>> item in index.SitesByGraphForAudit)
			{
				List<QuestGrantSite> value = item.Value;
				if (value == null)
				{
					continue;
				}
				for (int i = 0; i < value.Count; i++)
				{
					QuestGrantSite questGrantSite = value[i];
					if (questGrantSite == null)
					{
						continue;
					}
					r.SitesTotal++;
					if (questGrantSite.AutoCompletes)
					{
						r.SitesAutoComplete++;
						continue;
					}
					bool flag = QuestGiverIndex.WouldSuppress(questGrantSite);
					if (!index.IsGrantableQuestGuid(questGrantSite.QuestGuid))
					{
						r.UnresolvedSitesAll++;
						if (!flag)
						{
							r.UnresolvedSitesFailOpen++;
						}
						if (!string.IsNullOrEmpty(questGrantSite.QuestGuid))
						{
							hashSet2.Add(questGrantSite.QuestGuid);
						}
					}
					if ((int)index.LiveStateOf(questGrantSite.QuestGuid) != 0)
					{
						continue;
					}
					r.SitesUntaken++;
					if (questGrantSite.LegacyCauseWouldSuppress != questGrantSite.NoSelfSetDemotionWouldSuppress)
					{
						if (questGrantSite.NoSelfSetDemotionWouldSuppress)
						{
							r.SitesSuppressedOnlyAfterCauseFix++;
						}
						else
						{
							r.SitesFreedByCauseFix++;
						}
					}
					if (questGrantSite.LegacyCauseWouldSuppress)
					{
						r.SitesSuppressedUnderLegacyCauses++;
					}
					if (questGrantSite.NoSelfSetDemotionWouldSuppress != flag)
					{
						if (flag)
						{
							r.SitesHiddenOnlyAfterSelfSetDemotion++;
						}
						else
						{
							r.SitesFreedBySelfSetGate++;
						}
					}
					if (questGrantSite.NoSelfSetDemotionWouldSuppress)
					{
						r.SitesSuppressedWithoutSelfSetDemotion++;
					}
					if (!flag)
					{
						continue;
					}
					r.SuppressedSites++;
					if (!string.IsNullOrEmpty(questGrantSite.QuestGuid))
					{
						hashSet.Add(questGrantSite.QuestGuid);
					}
					if (questGrantSite.CauseMixedVolatileAndPermanent)
					{
						r.SuppressedSitesMixedCause++;
					}
					List<string> decisiveFalses = questGrantSite.DecisiveFalses;
					for (int j = 0; j < decisiveFalses.Count; j++)
					{
						string text = decisiveFalses[j];
						if (text == null)
						{
							continue;
						}
						ScanReport.Bump(r.SuppressedSitesByCause, text);
						if (!string.IsNullOrEmpty(questGrantSite.QuestGuid))
						{
							if (!dictionary.TryGetValue(questGrantSite.QuestGuid, out var value2))
							{
								value2 = new HashSet<string>(StringComparer.Ordinal);
								dictionary.Add(questGrantSite.QuestGuid, value2);
							}
							value2.Add(text);
						}
					}
				}
			}
			r.SuppressedQuests = hashSet.Count;
			r.UnresolvedQuestGuids = hashSet2.Count;
			foreach (KeyValuePair<string, HashSet<string>> item2 in dictionary)
			{
				foreach (string item3 in item2.Value)
				{
					ScanReport.Bump(r.SuppressedQuestsByCause, item3);
				}
			}
		}

		private static void NpcPass(QuestGiverIndex index, Dictionary<string, string> questNames, bool buildRows, SuppressionAuditResult r)
		{
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0401: Unknown result type (might be due to invalid IL or missing references)
			HashSet<StoryEntry> hashSet = new HashSet<StoryEntry>();
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, Tri> dictionary = new Dictionary<string, Tri>(StringComparer.Ordinal);
			List<string> list = new List<string>(4);
			List<string> list2 = new List<string>(4);
			List<string> list3 = new List<string>(4);
			List<string> list4 = new List<string>(4);
			List<string> list5 = new List<string>(4);
			List<string> list6 = new List<string>(4);
			List<string> list7 = new List<string>(4);
			List<string> list8 = new List<string>(2);
			foreach (NpcElement current in World.All<NpcElement>())
			{
				if (current == null)
				{
					continue;
				}
				r.NpcsLoaded++;
				Location parentModel;
				try
				{
					parentModel = ((Element<Location>)(object)current).ParentModel;
				}
				catch (Exception)
				{
					continue;
				}
				if (parentModel == null || ((Model)parentModel).HasBeenDiscarded)
				{
					continue;
				}
				List<StoryEntry> list9;
				try
				{
					list9 = QuestGiverIndex.BookmarkEntries(parentModel);
				}
				catch (Exception)
				{
					continue;
				}
				if (list9 == null || list9.Count == 0)
				{
					continue;
				}
				r.NpcsWithBookmark++;
				list8.Clear();
				bool flag = false;
				for (int i = 0; i < list9.Count; i++)
				{
					list8.Add(list9[i].Label);
					if (list9[i].Chapter != null)
					{
						flag = true;
					}
				}
				if (flag)
				{
					r.NpcsWithNamedEntry++;
				}
				hashSet.Clear();
				hashSet2.Clear();
				for (int j = 0; j < list9.Count; j++)
				{
					foreach (StoryEntry item2 in index.ReachableEntries(list9[j]))
					{
						hashSet.Add(item2);
					}
					foreach (string item3 in index.ReachableGraphs(list9[j].Graph))
					{
						hashSet2.Add(item3);
					}
				}
				if (hashSet.Count == 0 && hashSet2.Count == 0)
				{
					continue;
				}
				dictionary.Clear();
				list.Clear();
				list2.Clear();
				list3.Clear();
				list4.Clear();
				list5.Clear();
				list6.Clear();
				list7.Clear();
				bool flag2 = false;
				bool flag3 = false;
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = false;
				bool flag7 = false;
				bool flag8 = false;
				bool flag9 = false;
				foreach (StoryEntry item4 in hashSet)
				{
					if (!index.WasEntryAnalysed(item4))
					{
						flag9 = true;
						flag2 = true;
						flag3 = true;
						flag4 = true;
						flag8 = true;
						continue;
					}
					List<QuestGrantSite> list10 = index.EntrySitesForAudit(item4);
					if (list10 == null)
					{
						continue;
					}
					for (int k = 0; k < list10.Count; k++)
					{
						QuestGrantSite questGrantSite = list10[k];
						if (questGrantSite == null || questGrantSite.AutoCompletes)
						{
							continue;
						}
						bool flag10 = !index.IsGrantableQuestGuid(questGrantSite.QuestGuid);
						if (!flag10 && (int)index.LiveStateOf(questGrantSite.QuestGuid) != 0)
						{
							continue;
						}
						bool num = QuestGiverIndex.WouldSuppress(questGrantSite);
						flag2 = true;
						flag3 = true;
						if (!flag10 && questGrantSite.Availability == Tri.True)
						{
							flag5 = true;
						}
						if (num)
						{
							if (buildRows)
							{
								RecordSuppressed(questGrantSite, questNames, list2, list3, list4);
							}
							continue;
						}
						flag4 = true;
						if (!flag10 && questGrantSite.Availability == Tri.True)
						{
							flag6 = true;
						}
						if (flag10)
						{
							flag8 = true;
						}
						else
						{
							flag7 = true;
						}
						if (buildRows)
						{
							RecordLit(questGrantSite, flag10, questNames, dictionary, list, list5);
						}
					}
				}
				if (flag9)
				{
					r.NpcsWithUnanalysedEntry++;
				}
				bool flag11 = false;
				foreach (string item5 in hashSet2)
				{
					if (!index.SitesByGraphForAudit.TryGetValue(item5, out var value) || value == null)
					{
						continue;
					}
					for (int l = 0; l < value.Count; l++)
					{
						QuestGrantSite questGrantSite2 = value[l];
						if (questGrantSite2 == null || questGrantSite2.AutoCompletes)
						{
							continue;
						}
						bool flag12 = !index.IsGrantableQuestGuid(questGrantSite2.QuestGuid);
						if ((!flag12 && (int)index.LiveStateOf(questGrantSite2.QuestGuid) != 0) || (r.SuppressionOn && QuestGiverIndex.WouldSuppress(questGrantSite2)) || (r.PreciseMode && (flag12 || questGrantSite2.Availability != Tri.True)))
						{
							continue;
						}
						flag11 = true;
						if (buildRows)
						{
							string item = QuestLabel(questGrantSite2.QuestGuid, questNames);
							if (!list6.Contains(item))
							{
								list6.Add(item);
							}
							if (!string.IsNullOrEmpty(questGrantSite2.GraphGuid) && !list7.Contains(questGrantSite2.GraphGuid))
							{
								list7.Add(questGrantSite2.GraphGuid);
							}
						}
					}
				}
				if (flag2)
				{
					r.NpcsWithUntakenGrantable++;
				}
				bool num2 = (r.PreciseMode ? flag5 : flag3);
				bool flag13 = (r.PreciseMode ? flag6 : flag4);
				if (num2)
				{
					r.NpcsLitSuppressionOff++;
				}
				if (flag13)
				{
					r.NpcsLitSuppressionOn++;
				}
				if (flag13 && flag8 && !flag7)
				{
					r.NpcsLitOnlyByUnresolved++;
				}
				if (flag11)
				{
					r.NpcsLitGraphModel++;
				}
				if (flag13)
				{
					r.NpcsLitEntryModel++;
				}
				if (flag11 && !flag13)
				{
					r.NarrowedTotal++;
					if (buildRows)
					{
						SuppressionNpcRow suppressionNpcRow = BuildRow(current, parentModel, list6, null, list7);
						suppressionNpcRow.Entries.AddRange(list8);
						r.Narrowed.Add(suppressionNpcRow);
					}
				}
				if (num2 && !flag13)
				{
					r.HiddenTotal++;
					if (buildRows)
					{
						r.Hidden.Add(BuildRow(current, parentModel, list2, list3, list4));
					}
				}
				else if (flag13)
				{
					r.StillLitTotal++;
					if (buildRows)
					{
						r.StillLit.Add(BuildRow(current, parentModel, LitLabels(list, dictionary), null, list5));
					}
				}
			}
			r.NpcsHidden = r.HiddenTotal;
			SortRows(r.Hidden);
			SortRows(r.StillLit);
			SortRows(r.Narrowed);
		}

		private static void RecordSuppressed(QuestGrantSite s, Dictionary<string, string> questNames, List<string> names, List<string> causes, List<string> graphs)
		{
			string item = QuestLabel(s.QuestGuid, questNames);
			if (!names.Contains(item))
			{
				names.Add(item);
			}
			List<string> decisiveFalses = s.DecisiveFalses;
			if (decisiveFalses != null)
			{
				for (int i = 0; i < decisiveFalses.Count; i++)
				{
					if (decisiveFalses[i] != null && !causes.Contains(decisiveFalses[i]))
					{
						causes.Add(decisiveFalses[i]);
					}
				}
			}
			if (!string.IsNullOrEmpty(s.GraphGuid) && !graphs.Contains(s.GraphGuid))
			{
				graphs.Add(s.GraphGuid);
			}
		}

		private static void RecordLit(QuestGrantSite s, bool unresolved, Dictionary<string, string> questNames, Dictionary<string, Tri> quests, List<string> order, List<string> graphs)
		{
			string text = QuestLabel(s.QuestGuid, questNames);
			if (unresolved)
			{
				text += " [unresolved ref]";
			}
			if (quests.TryGetValue(text, out var value))
			{
				quests[text] = TriOps.Join(value, s.Availability);
			}
			else
			{
				quests.Add(text, s.Availability);
				order.Add(text);
			}
			if (!string.IsNullOrEmpty(s.GraphGuid) && !graphs.Contains(s.GraphGuid))
			{
				graphs.Add(s.GraphGuid);
			}
		}

		private static SuppressionNpcRow BuildRow(NpcElement npc, Location loc, List<string> quests, List<string> causes, List<string> graphs)
		{
			SuppressionNpcRow suppressionNpcRow = new SuppressionNpcRow();
			try
			{
				suppressionNpcRow.Name = npc.Name ?? "";
			}
			catch (Exception ex)
			{
				suppressionNpcRow.Name = "<name threw " + ex.GetType().Name + ">";
			}
			try
			{
				suppressionNpcRow.LocationTemplate = (((Object)(object)loc.Template != (Object)null) ? ((Template)loc.Template).DebugName : "");
			}
			catch (Exception)
			{
				suppressionNpcRow.LocationTemplate = "";
			}
			if (quests != null)
			{
				suppressionNpcRow.Quests.AddRange(quests);
			}
			if (causes != null)
			{
				suppressionNpcRow.Causes.AddRange(causes);
			}
			if (graphs != null)
			{
				suppressionNpcRow.Graphs.AddRange(graphs);
			}
			suppressionNpcRow.Quests.Sort(StringComparer.Ordinal);
			suppressionNpcRow.Causes.Sort(StringComparer.Ordinal);
			suppressionNpcRow.Graphs.Sort(StringComparer.Ordinal);
			suppressionNpcRow.SortGuid = ((suppressionNpcRow.Graphs.Count > 0) ? suppressionNpcRow.Graphs[0] : "");
			return suppressionNpcRow;
		}

		private static List<string> LitLabels(List<string> order, Dictionary<string, Tri> quests)
		{
			List<string> list = new List<string>(order.Count);
			for (int i = 0; i < order.Count; i++)
			{
				quests.TryGetValue(order[i], out var value);
				object obj;
				switch (value)
				{
				default:
					obj = "False";
					break;
				case Tri.Unknown:
					obj = "Unknown";
					break;
				case Tri.True:
					obj = "True";
					break;
				}
				string text = (string)obj;
				list.Add(order[i] + " (" + text + ")");
			}
			return list;
		}

		private static string QuestLabel(string guid, Dictionary<string, string> questNames)
		{
			if (string.IsNullOrEmpty(guid))
			{
				return "<no quest guid>";
			}
			if (questNames != null && questNames.TryGetValue(guid, out var value) && !string.IsNullOrEmpty(value))
			{
				return value;
			}
			return guid;
		}

		public static void SortRows(List<SuppressionNpcRow> rows)
		{
			rows.Sort(delegate(SuppressionNpcRow a, SuppressionNpcRow b)
			{
				int num = string.CompareOrdinal(a.Name, b.Name);
				return (num == 0) ? string.CompareOrdinal(a.SortGuid, b.SortGuid) : num;
			});
		}
	}
}
