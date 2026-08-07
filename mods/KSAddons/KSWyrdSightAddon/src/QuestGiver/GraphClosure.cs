using System;
using System.Collections.Generic;

namespace AvalonUntold
{
	internal static class GraphClosure
	{
		public static bool Walk<T>(List<T> seeds, Dictionary<T, List<T>> conversational, Dictionary<T, List<T>> spawned, HashSet<T> spawnedOnlyGranting, int maxGraphs, HashSet<T> into, Queue<T> frontier)
		{
			into.Clear();
			frontier.Clear();
			bool result = false;
			if (seeds == null)
			{
				return false;
			}
			if (maxGraphs < 1)
			{
				maxGraphs = int.MaxValue;
			}
			for (int i = 0; i < seeds.Count; i++)
			{
				if (into.Add(seeds[i]))
				{
					frontier.Enqueue(seeds[i]);
				}
			}
			while (frontier.Count > 0)
			{
				T key = frontier.Dequeue();
				if (conversational != null && conversational.TryGetValue(key, out var value))
				{
					for (int j = 0; j < value.Count; j++)
					{
						if (into.Count >= maxGraphs)
						{
							result = true;
							break;
						}
						if (into.Add(value[j]))
						{
							frontier.Enqueue(value[j]);
						}
					}
				}
				if (spawnedOnlyGranting == null || spawnedOnlyGranting.Count == 0 || spawned == null || !spawned.TryGetValue(key, out value))
				{
					continue;
				}
				for (int k = 0; k < value.Count; k++)
				{
					if (spawnedOnlyGranting.Contains(value[k]))
					{
						if (into.Count >= maxGraphs)
						{
							result = true;
							break;
						}
						if (into.Add(value[k]))
						{
							frontier.Enqueue(value[k]);
						}
					}
				}
			}
			return result;
		}

		public static HashSet<StoryEntry> SpawnedOnlyGranting(Dictionary<StoryEntry, List<StoryEntry>> conversational, Dictionary<StoryEntry, List<StoryEntry>> spawned, Dictionary<string, List<QuestGrantSite>> sitesByGraph)
		{
			HashSet<StoryEntry> hashSet = new HashSet<StoryEntry>();
			if (spawned == null || sitesByGraph == null)
			{
				return hashSet;
			}
			HashSet<StoryEntry> hashSet2 = new HashSet<StoryEntry>();
			if (conversational != null)
			{
				foreach (KeyValuePair<StoryEntry, List<StoryEntry>> item2 in conversational)
				{
					for (int i = 0; i < item2.Value.Count; i++)
					{
						hashSet2.Add(item2.Value[i]);
					}
				}
			}
			foreach (KeyValuePair<StoryEntry, List<StoryEntry>> item3 in spawned)
			{
				List<StoryEntry> value = item3.Value;
				for (int j = 0; j < value.Count; j++)
				{
					StoryEntry item = value[j];
					if (!hashSet2.Contains(item) && !hashSet.Contains(item) && Grants(sitesByGraph, item.Graph))
					{
						hashSet.Add(item);
					}
				}
			}
			return hashSet;
		}

		public static HashSet<string> SpawnedOnlyGrantingGraphs(Dictionary<string, List<string>> conversational, Dictionary<string, List<string>> spawned, Dictionary<string, List<QuestGrantSite>> sitesByGraph)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (spawned == null || sitesByGraph == null)
			{
				return hashSet;
			}
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (conversational != null)
			{
				foreach (KeyValuePair<string, List<string>> item in conversational)
				{
					for (int i = 0; i < item.Value.Count; i++)
					{
						hashSet2.Add(item.Value[i]);
					}
				}
			}
			foreach (KeyValuePair<string, List<string>> item2 in spawned)
			{
				List<string> value = item2.Value;
				for (int j = 0; j < value.Count; j++)
				{
					string text = value[j];
					if (!hashSet2.Contains(text) && !hashSet.Contains(text) && Grants(sitesByGraph, text))
					{
						hashSet.Add(text);
					}
				}
			}
			return hashSet;
		}

		private static bool Grants(Dictionary<string, List<QuestGrantSite>> sitesByGraph, string graph)
		{
			if (string.IsNullOrEmpty(graph) || !sitesByGraph.TryGetValue(graph, out var value) || value == null)
			{
				return false;
			}
			for (int i = 0; i < value.Count; i++)
			{
				if (value[i] != null && !value[i].AutoCompletes)
				{
					return true;
				}
			}
			return false;
		}
	}
}
