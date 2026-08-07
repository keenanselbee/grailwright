using System;
using System.Collections.Generic;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Templates;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public sealed class NpcSurvey
	{
		private sealed class Seed
		{
			public NpcRow Row;

			public readonly List<StoryEntry> Entries = new List<StoryEntry>();
		}

		private readonly List<Seed> _seeds = new List<Seed>();

		public void CollectSeeds(GraphIndex index, ScanCounters counters)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			foreach (NpcElement current in World.All<NpcElement>())
			{
				if (current == null)
				{
					continue;
				}
				NpcRow npcRow = new NpcRow();
				Seed seed = new Seed();
				seed.Row = npcRow;
				try
				{
					npcRow.Name = current.Name;
				}
				catch (Exception ex)
				{
					npcRow.Name = "<name threw " + ex.GetType().Name + ">";
				}
				Location val = null;
				try
				{
					val = ((Element<Location>)(object)current).ParentModel;
				}
				catch (Exception)
				{
				}
				if (val != null)
				{
					try
					{
						npcRow.LocationTemplate = (((Object)(object)val.Template != (Object)null) ? ((Template)val.Template).DebugName : "");
					}
					catch (Exception)
					{
						npcRow.LocationTemplate = "";
					}
					npcRow.HasDialogueAction = QuestGiverIndex.HasDialogueAction(val, counters);
					List<string> list = new List<string>(1);
					List<StoryEntry> list2 = new List<StoryEntry>(2);
					QuestGiverIndex.CollectBookmarks(val, list2, npcRow.BookmarkSources, list, counters);
					if (list.Count > 0)
					{
						npcRow.Error = string.Join("; ", list.ToArray());
					}
					for (int i = 0; i < list2.Count; i++)
					{
						StoryEntry item = list2[i];
						if (index.NoteNpcBookmark(item.Graph))
						{
							seed.Entries.Add(item);
							if (!npcRow.GraphGuids.Contains(item.Label))
							{
								npcRow.GraphGuids.Add(item.Label);
							}
							if (item.Chapter != null)
							{
								npcRow.NamedEntries++;
							}
						}
						else
						{
							npcRow.DanglingBookmarks++;
						}
					}
				}
				_seeds.Add(seed);
			}
		}

		public void Build(ScanReport report, Dictionary<StoryEntry, List<StoryEntry>> entryOut, Dictionary<StoryEntry, List<StoryEntry>> entryOutSpawned, Dictionary<StoryEntry, List<QuestGrantSite>> entrySites, HashSet<StoryEntry> analysedEntries, Dictionary<string, List<QuestGrantSite>> sitesByGraph, QuestCatalog catalog, int maxClosureGraphs)
		{
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			HashSet<StoryEntry> hashSet = GraphClosure.SpawnedOnlyGranting(entryOut, entryOutSpawned, sitesByGraph);
			report.SpawnedOnlyGrantingEntries = hashSet.Count;
			HashSet<StoryEntry> hashSet2 = new HashSet<StoryEntry>();
			Queue<StoryEntry> frontier = new Queue<StoryEntry>();
			List<StoryEntry> list = new List<StoryEntry>(4);
			for (int i = 0; i < _seeds.Count; i++)
			{
				Seed seed = _seeds[i];
				NpcRow row = seed.Row;
				report.Npcs.Add(row);
				if (row.HasDialogueAction)
				{
					report.NpcsWithDialogue++;
				}
				if (seed.Entries.Count == 0)
				{
					continue;
				}
				report.NpcsWithAnyBookmark++;
				list.Clear();
				list.AddRange(seed.Entries);
				row.ClosureCapped = GraphClosure.Walk(list, entryOut, entryOutSpawned, hashSet, maxClosureGraphs, hashSet2, frontier);
				row.ReachableGraphs = hashSet2.Count;
				foreach (StoryEntry item in hashSet2)
				{
					if (analysedEntries != null && !analysedEntries.Contains(item))
					{
						row.UnanalysedEntries++;
						row.HasAnyGrantable = true;
						row.BestAvailability = TriOps.Join(row.BestAvailability, Tri.Unknown);
					}
					else
					{
						if (!entrySites.TryGetValue(item, out var value) || value == null)
						{
							continue;
						}
						for (int j = 0; j < value.Count; j++)
						{
							QuestGrantSite questGrantSite = value[j];
							if (!catalog.ByGuid.TryGetValue(questGrantSite.QuestGuid, out var value2) || value2.IsAchievement || (int)value2.State != 0)
							{
								continue;
							}
							if (questGrantSite.AutoCompletes)
							{
								if (!row.UntakenAutoCompleted.Contains(value2.Name))
								{
									row.UntakenAutoCompleted.Add(value2.Name);
								}
								continue;
							}
							row.HasAnyGrantable = true;
							row.BestAvailability = TriOps.Join(row.BestAvailability, questGrantSite.Availability);
							if (!row.UntakenGrantable.Contains(value2.Name))
							{
								row.UntakenGrantable.Add(value2.Name);
							}
						}
					}
				}
				if (row.HasAnyGrantable)
				{
					report.NpcsWithGrantable++;
				}
			}
		}
	}
}
