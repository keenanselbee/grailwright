using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Actions.Customs;
using Awaken.TG.Main.Locations.Attachments.Attachment;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Runtime;
using Awaken.TG.Main.Templates;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public sealed class QuestGiverIndex
	{
		public int RebuildCount;

		public int RebuildFailures;

		public long LastRebuildMillis = -1L;

		public int LastRebuildFrames;

		public long WorstRebuildSliceMs;

		public int RebuildLocationScans;

		public int RebuildConditionEvaluations;

		public int CoverageWalked;

		public int CoverageTotal;

		private readonly HashSet<string> _directory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _graphHasAvailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, bool> _seedHasAvailable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _graphHasCertain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, bool> _seedHasCertain = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, List<QuestGrantSite>> _sitesByGraph = new Dictionary<string, List<QuestGrantSite>>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, List<string>> _graphOut = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, List<string>> _graphOutSpawned = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		private HashSet<string> _spawnedOnlyGranting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly List<string> _closureSeeds = new List<string>(1);

		private readonly HashSet<string> _closureVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly Queue<string> _closureFrontier = new Queue<string>();

		private Dictionary<string, StoryGraphRuntime> _retained;

		private HashSet<string> _grantableQuestGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<StoryEntry, List<StoryEntry>> _entryOut = new Dictionary<StoryEntry, List<StoryEntry>>();

		private Dictionary<StoryEntry, List<StoryEntry>> _entryOutSpawned = new Dictionary<StoryEntry, List<StoryEntry>>();

		private Dictionary<StoryEntry, List<QuestGrantSite>> _entrySites = new Dictionary<StoryEntry, List<QuestGrantSite>>();

		private HashSet<StoryEntry> _analysedEntries = new HashSet<StoryEntry>();

		private Dictionary<string, string[]> _entryNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

		private HashSet<StoryEntry> _spawnedOnlyGrantingEntries = new HashSet<StoryEntry>();

		private readonly HashSet<StoryEntry> _entryHasAvailable = new HashSet<StoryEntry>();

		private readonly HashSet<StoryEntry> _entryHasCertain = new HashSet<StoryEntry>();

		private readonly Dictionary<StoryEntry, bool> _seedEntryHasAvailable = new Dictionary<StoryEntry, bool>();

		private readonly Dictionary<StoryEntry, bool> _seedEntryHasCertain = new Dictionary<StoryEntry, bool>();

		private readonly List<StoryEntry> _entrySeeds = new List<StoryEntry>(1);

		private readonly HashSet<StoryEntry> _entryVisited = new HashSet<StoryEntry>();

		private readonly Queue<StoryEntry> _entryFrontier = new Queue<StoryEntry>();

		public int EntriesUnknownAtQuery;

		private int _unresolvedQuestRefs;

		private GameplayMemory _memory;

		private int _maxClosureGraphs = 256;

		private ConditionEvaluator _eval;

		private StepGate _gate;

		private ReachabilityAnalyzer _reach;

		private ScanCounters _rebuildCounters;

		internal static bool SuppressLockedQuests;

		private static readonly MemberInfo StoryInteractBookmark;

		private static readonly MemberInfo ReadBookmark;

		private static readonly MemberInfo SearchBookmark;

		private static readonly MemberInfo PickItemBookmark;

		private static readonly MemberInfo DigOutBookmark;

		private static readonly MemberInfo ShrineBookmark;

		private static readonly MemberInfo FirstDamageBookmark;

		private static readonly MemberInfo TriggeringRangeBookmark;

		private static readonly MemberInfo BusyBookmark;

		private static readonly MemberInfo TemporaryDeathSpec;

		private static readonly MemberInfo StagfathersTrialSpec;

		public static readonly List<string> UnresolvedBookmarkMembers;

		public static QuestGiverIndex Current { get; internal set; }

		public bool IsReady { get; private set; }

		public int ClosureMax { get; private set; }

		public int ClosureMedian { get; private set; }

		public int ClosurePopulation { get; private set; }

		public int SpawnedOnlyGrantingGraphs => _spawnedOnlyGranting.Count;

		internal Dictionary<string, List<QuestGrantSite>> SitesByGraphForAudit => _sitesByGraph;

		public int EntryPairs => _analysedEntries.Count;

		public int RetainedGraphs
		{
			get
			{
				if (_retained != null)
				{
					return _retained.Count;
				}
				return 0;
			}
		}

		public int GraphsWithGrantSites
		{
			get
			{
				if (_sitesByGraph != null)
				{
					return _sitesByGraph.Count;
				}
				return 0;
			}
		}

		public int GraphsWithAvailableQuest => _graphHasAvailable.Count;

		public int LocationCacheEntries
		{
			get
			{
				if (_eval != null)
				{
					return _eval.LocationCacheEntries;
				}
				return 0;
			}
		}

		public bool CoverageComplete
		{
			get
			{
				if (CoverageTotal > 0)
				{
					return CoverageWalked >= CoverageTotal;
				}
				return false;
			}
		}

		public int UnresolvedQuestRefs => _unresolvedQuestRefs;

		public int WouldSuppressCount { get; private set; }

		public event Action Rebuilt;

		public bool GraphHasAvailableQuest(string graphGuid)
		{
			return GraphHasAvailableQuest(graphGuid, onlyCertain: false);
		}

		public bool GraphHasAvailableQuest(string graphGuid, bool onlyCertain)
		{
			if (string.IsNullOrEmpty(graphGuid))
			{
				return false;
			}
			if (!onlyCertain)
			{
				return _graphHasAvailable.Contains(graphGuid);
			}
			return _graphHasCertain.Contains(graphGuid);
		}

		public IEnumerable<StoryEntry> ReachableEntries(StoryEntry seed)
		{
			List<StoryEntry> list = new List<StoryEntry>(4);
			if (!seed.IsValid || !_directory.Contains(seed.Graph))
			{
				return list;
			}
			_entrySeeds.Clear();
			_entrySeeds.Add(seed);
			GraphClosure.Walk(_entrySeeds, _entryOut, _entryOutSpawned, _spawnedOnlyGrantingEntries, _maxClosureGraphs, _entryVisited, _entryFrontier);
			list.AddRange(_entryVisited);
			return list;
		}

		public IEnumerable<string> ReachableGraphs(string seedGraphGuid)
		{
			List<string> list = new List<string>(4);
			if (string.IsNullOrEmpty(seedGraphGuid) || !_directory.Contains(seedGraphGuid))
			{
				return list;
			}
			_closureSeeds.Clear();
			_closureSeeds.Add(seedGraphGuid);
			GraphClosure.Walk(_closureSeeds, _graphOut, _graphOutSpawned, _spawnedOnlyGranting, _maxClosureGraphs, _closureVisited, _closureFrontier);
			list.AddRange(_closureVisited);
			return list;
		}

		public bool LocationHasAvailableQuest(Location location)
		{
			return LocationHasAvailableQuest(location, onlyCertain: false);
		}

		public bool LocationHasAvailableQuest(Location location, bool onlyCertain)
		{
			if (location == null || !IsReady)
			{
				return false;
			}
			List<StoryEntry> list = BookmarkEntries(location);
			for (int i = 0; i < list.Count; i++)
			{
				if (SeedHasAvailableQuest(list[i], onlyCertain))
				{
					return true;
				}
			}
			return false;
		}

		public bool BoundToCurrentMemory()
		{
			if (_memory == null)
			{
				return false;
			}
			try
			{
				GameplayMemory val = default(GameplayMemory);
				if (World.Services == null || !World.Services.TryGet<GameplayMemory>(out val))
				{
					return false;
				}
				return val == _memory;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public bool SeedHasAvailableQuest(StoryEntry seed)
		{
			return SeedHasAvailableQuest(seed, onlyCertain: false);
		}

		public bool SeedHasAvailableQuest(StoryEntry seed, bool onlyCertain)
		{
			if (!seed.IsValid)
			{
				return false;
			}
			Dictionary<StoryEntry, bool> dictionary = (onlyCertain ? _seedEntryHasCertain : _seedEntryHasAvailable);
			HashSet<StoryEntry> hashSet = (onlyCertain ? _entryHasCertain : _entryHasAvailable);
			if (dictionary.TryGetValue(seed, out var value))
			{
				return value;
			}
			bool flag = false;
			foreach (StoryEntry item in ReachableEntries(seed))
			{
				if (hashSet.Contains(item))
				{
					flag = true;
					break;
				}
				if (!_analysedEntries.Contains(item))
				{
					EntriesUnknownAtQuery++;
					flag = true;
					break;
				}
			}
			dictionary[seed] = flag;
			return flag;
		}

		public bool SeedGraphHasAvailableQuest(string seedGraphGuid, bool onlyCertain)
		{
			if (string.IsNullOrEmpty(seedGraphGuid))
			{
				return false;
			}
			Dictionary<string, bool> dictionary = (onlyCertain ? _seedHasCertain : _seedHasAvailable);
			HashSet<string> hashSet = (onlyCertain ? _graphHasCertain : _graphHasAvailable);
			if (dictionary.TryGetValue(seedGraphGuid, out var value))
			{
				return value;
			}
			bool flag = false;
			foreach (string item in ReachableGraphs(seedGraphGuid))
			{
				if (hashSet.Contains(item))
				{
					flag = true;
					break;
				}
			}
			dictionary[seedGraphGuid] = flag;
			return flag;
		}

		public static List<StoryEntry> BookmarkEntries(Location location)
		{
			List<StoryEntry> list = new List<StoryEntry>(2);
			CollectBookmarks(location, list, null, null, null);
			return list;
		}

		public static List<string> BookmarkGuids(Location location)
		{
			List<StoryEntry> list = BookmarkEntries(location);
			List<string> list2 = new List<string>(list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				if (!list2.Contains(list[i].Graph))
				{
					list2.Add(list[i].Graph);
				}
			}
			return list2;
		}

		public IEnumerator RebuildJob(int maxMillisPerFrame)
		{
			if (_retained == null || _eval == null)
			{
				yield break;
			}
			if (maxMillisPerFrame < 1)
			{
				maxMillisPerFrame = 1;
			}
			List<string> keys = new List<string>(_retained.Keys);
			Stopwatch slice = Stopwatch.StartNew();
			long workMs = 0L;
			int frames = 1;
			long num = 0L;
			for (int i = 0; i < keys.Count; i++)
			{
				try
				{
					if (_retained.TryGetValue(keys[i], out var value))
					{
						_sitesByGraph.TryGetValue(keys[i], out var value2);
						Reevaluate(keys[i], value, value2);
					}
				}
				catch (Exception)
				{
					RebuildFailures++;
				}
				long elapsedMilliseconds = slice.ElapsedMilliseconds;
				if (elapsedMilliseconds - num > WorstRebuildSliceMs)
				{
					WorstRebuildSliceMs = elapsedMilliseconds - num;
				}
				num = elapsedMilliseconds;
				if (elapsedMilliseconds >= maxMillisPerFrame)
				{
					workMs += elapsedMilliseconds;
					yield return null;
					frames++;
					slice.Restart();
					num = 0L;
				}
			}
			try
			{
				_spawnedOnlyGrantingEntries = GraphClosure.SpawnedOnlyGranting(_entryOut, _entryOutSpawned, _sitesByGraph);
				RecomputeAvailability();
			}
			catch (Exception ex2)
			{
				RebuildFailures++;
				if (Plugin.Log != null)
				{
					Plugin.Log.Error("QuestGiverIndex rebuild failed: " + ex2);
				}
			}
			slice.Stop();
			workMs += slice.ElapsedMilliseconds;
			LastRebuildMillis = workMs;
			LastRebuildFrames = frames;
			RebuildCount++;
			RebuildLocationScans = ((_rebuildCounters != null) ? _rebuildCounters.LocationRefMatchScans : 0);
			RebuildConditionEvaluations = ((_rebuildCounters != null) ? _rebuildCounters.ConditionEvaluations : 0);
			RaiseRebuilt();
		}

		public void InvalidateLocationCache()
		{
			if (_eval != null)
			{
				_eval.InvalidateLocationCache();
			}
		}

		public void RefreshSuppression()
		{
			if (!IsReady)
			{
				return;
			}
			try
			{
				RecomputeAvailability();
			}
			catch (Exception ex)
			{
				RebuildFailures++;
				if (Plugin.Log != null)
				{
					Plugin.Log.Error("suppression refresh failed: " + ex);
				}
			}
			RaiseRebuilt();
		}

		internal List<QuestGrantSite> EntrySitesForAudit(StoryEntry entry)
		{
			if (!_entrySites.TryGetValue(entry, out var value))
			{
				return null;
			}
			return value;
		}

		internal bool WasEntryAnalysed(StoryEntry entry)
		{
			return _analysedEntries.Contains(entry);
		}

		internal bool IsGrantableQuestGuid(string questGuid)
		{
			if (!string.IsNullOrEmpty(questGuid))
			{
				return _grantableQuestGuids.Contains(questGuid);
			}
			return false;
		}

		internal QuestState LiveStateOf(string questGuid)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return StateOf(questGuid);
		}

		internal void Populate(IEnumerable<string> directory, Dictionary<string, List<QuestGrantSite>> sitesByGraph, Dictionary<string, List<string>> graphOut, Dictionary<string, List<string>> graphOutSpawned, Dictionary<string, StoryGraphRuntime> retained, EntryModel entries, IEnumerable<string> grantableQuestGuids, GameplayMemory memory, int maxClosureGraphs, int coverageWalked, int coverageTotal)
		{
			if (entries != null)
			{
				_entryOut = entries.Out ?? new Dictionary<StoryEntry, List<StoryEntry>>();
				_entryOutSpawned = entries.OutSpawned ?? new Dictionary<StoryEntry, List<StoryEntry>>();
				_entrySites = entries.Sites ?? new Dictionary<StoryEntry, List<QuestGrantSite>>();
				_analysedEntries = entries.Analysed ?? new HashSet<StoryEntry>();
				_entryNames = entries.NamesByGraph ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
			}
			EntriesUnknownAtQuery = 0;
			CoverageWalked = coverageWalked;
			CoverageTotal = coverageTotal;
			_directory.Clear();
			if (directory != null)
			{
				foreach (string item in directory)
				{
					_directory.Add(item);
				}
			}
			_sitesByGraph = sitesByGraph ?? new Dictionary<string, List<QuestGrantSite>>(StringComparer.OrdinalIgnoreCase);
			_graphOut = graphOut ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			_graphOutSpawned = graphOutSpawned ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			_retained = retained ?? new Dictionary<string, StoryGraphRuntime>(StringComparer.OrdinalIgnoreCase);
			_grantableQuestGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (grantableQuestGuids != null)
			{
				foreach (string grantableQuestGuid in grantableQuestGuids)
				{
					if (!string.IsNullOrEmpty(grantableQuestGuid))
					{
						_grantableQuestGuids.Add(grantableQuestGuid);
					}
				}
			}
			_memory = memory;
			_maxClosureGraphs = ((maxClosureGraphs > 0) ? maxClosureGraphs : 256);
			_spawnedOnlyGranting = GraphClosure.SpawnedOnlyGrantingGraphs(_graphOut, _graphOutSpawned, _sitesByGraph);
			_spawnedOnlyGrantingEntries = GraphClosure.SpawnedOnlyGranting(_entryOut, _entryOutSpawned, _sitesByGraph);
			MeasureClosure();
			NewEvaluator();
			RecomputeAvailability();
			IsReady = true;
			RaiseRebuilt();
		}

		private void MeasureClosure()
		{
			ClosureMax = 0;
			ClosureMedian = 0;
			ClosurePopulation = _graphOut.Count;
			if (_graphOut.Count == 0)
			{
				return;
			}
			List<int> list = new List<int>(_graphOut.Count);
			foreach (KeyValuePair<string, List<string>> item in _graphOut)
			{
				_closureSeeds.Clear();
				_closureSeeds.Add(item.Key);
				GraphClosure.Walk(_closureSeeds, _graphOut, _graphOutSpawned, _spawnedOnlyGranting, _maxClosureGraphs, _closureVisited, _closureFrontier);
				int count = _closureVisited.Count;
				list.Add(count);
				if (count > ClosureMax)
				{
					ClosureMax = count;
				}
			}
			list.Sort();
			ClosureMedian = list[list.Count / 2];
		}

		private void NewEvaluator()
		{
			_rebuildCounters = new ScanCounters();
			_eval = new ConditionEvaluator(_memory, _rebuildCounters);
			_gate = new StepGate(_eval, _rebuildCounters);
			_reach = new ReachabilityAnalyzer(_rebuildCounters);
		}

		private void RaiseRebuilt()
		{
			Action action = this.Rebuilt;
			if (action == null)
			{
				return;
			}
			try
			{
				action();
			}
			catch (Exception ex)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.Error("a QuestGiverIndex.Rebuilt subscriber threw: " + ex);
				}
			}
		}

		private void RecomputeAvailability()
		{
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			_graphHasAvailable.Clear();
			_seedHasAvailable.Clear();
			_graphHasCertain.Clear();
			_seedHasCertain.Clear();
			_entryHasAvailable.Clear();
			_entryHasCertain.Clear();
			_seedEntryHasAvailable.Clear();
			_seedEntryHasCertain.Clear();
			TallySiteDiagnostics();
			foreach (KeyValuePair<StoryEntry, List<QuestGrantSite>> entrySite in _entrySites)
			{
				bool flag = false;
				bool flag2 = false;
				List<QuestGrantSite> value = entrySite.Value;
				for (int i = 0; i < value.Count; i++)
				{
					QuestGrantSite questGrantSite = value[i];
					if (questGrantSite.AutoCompletes || (SuppressLockedQuests && WouldSuppress(questGrantSite)))
					{
						continue;
					}
					if (!_grantableQuestGuids.Contains(questGrantSite.QuestGuid))
					{
						flag = true;
					}
					else if ((int)StateOf(questGrantSite.QuestGuid) == 0)
					{
						flag = true;
						if (questGrantSite.Availability == Tri.True)
						{
							flag2 = true;
							break;
						}
					}
				}
				if (flag)
				{
					_entryHasAvailable.Add(entrySite.Key);
				}
				if (flag2)
				{
					_entryHasCertain.Add(entrySite.Key);
				}
			}
			foreach (KeyValuePair<string, List<QuestGrantSite>> item in _sitesByGraph)
			{
				bool flag3 = false;
				bool flag4 = false;
				List<QuestGrantSite> value2 = item.Value;
				for (int j = 0; j < value2.Count; j++)
				{
					QuestGrantSite questGrantSite2 = value2[j];
					if (questGrantSite2.AutoCompletes || (SuppressLockedQuests && WouldSuppress(questGrantSite2)))
					{
						continue;
					}
					if (!_grantableQuestGuids.Contains(questGrantSite2.QuestGuid))
					{
						flag3 = true;
					}
					else if ((int)StateOf(questGrantSite2.QuestGuid) == 0)
					{
						flag3 = true;
						if (questGrantSite2.Availability == Tri.True)
						{
							flag4 = true;
							break;
						}
					}
				}
				if (flag3)
				{
					_graphHasAvailable.Add(item.Key);
				}
				if (flag4)
				{
					_graphHasCertain.Add(item.Key);
				}
			}
		}

		private void TallySiteDiagnostics()
		{
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			_unresolvedQuestRefs = 0;
			WouldSuppressCount = 0;
			foreach (KeyValuePair<string, List<QuestGrantSite>> item in _sitesByGraph)
			{
				List<QuestGrantSite> value = item.Value;
				if (value == null)
				{
					continue;
				}
				for (int i = 0; i < value.Count; i++)
				{
					QuestGrantSite questGrantSite = value[i];
					if (questGrantSite != null && !questGrantSite.AutoCompletes)
					{
						bool flag = WouldSuppress(questGrantSite);
						if (flag && (int)StateOf(questGrantSite.QuestGuid) == 0)
						{
							WouldSuppressCount++;
						}
						if (!(SuppressLockedQuests && flag) && !_grantableQuestGuids.Contains(questGrantSite.QuestGuid))
						{
							_unresolvedQuestRefs++;
						}
					}
				}
			}
		}

		private static bool IsDecisiveFalse(QuestGrantSite s)
		{
			if (SuppressLockedQuests)
			{
				return WouldSuppress(s);
			}
			return false;
		}

		internal static bool WouldSuppress(QuestGrantSite s)
		{
			return WouldSuppressVerdict(s.Availability, s.DecisiveFalses);
		}

		internal static bool WouldSuppressVerdict(Tri availability, List<string> causes)
		{
			if (availability != Tri.False)
			{
				return false;
			}
			if (causes == null || causes.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < causes.Count; i++)
			{
				string text = causes[i];
				if (text == null)
				{
					return false;
				}
				if (!text.EndsWith("(volatile)", StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private QuestState StateOf(string questGuid)
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected O, but got Unknown
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			if (_memory == null || string.IsNullOrEmpty(questGuid))
			{
				return (QuestState)0;
			}
			try
			{
				return QuestUtils.StateOfQuestWithId((IMemory)(object)_memory, new TemplateReference(questGuid));
			}
			catch (Exception)
			{
				return (QuestState)0;
			}
		}

		private void Reevaluate(string graphGuid, StoryGraphRuntime graph, List<QuestGrantSite> sites)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return;
			}
			_eval.BeginGraph(graphGuid, graph.sharedBetweenMultipleNPCs, null);
			_gate.BeginGraph();
			_reach.CountersEnabled = true;
			_reach.Analyze(graph, _gate, sites != null);
			if (sites != null)
			{
				for (int i = 0; i < sites.Count; i++)
				{
					QuestScanner.EvaluateSite(sites[i], graph, _reach, _gate);
				}
			}
			if (!_entryNames.TryGetValue(graphGuid, out var value) || value == null)
			{
				return;
			}
			_reach.CountersEnabled = false;
			try
			{
				for (int j = 0; j < value.Length; j++)
				{
					StoryEntry storyEntry = new StoryEntry(graphGuid, value[j]);
					_reach.AnalyzeEntry(graph, _gate, value[j], sites != null);
					if (_entrySites.TryGetValue(storyEntry, out var value2) && value2 != null)
					{
						for (int k = 0; k < value2.Count; k++)
						{
							QuestScanner.EvaluateSite(value2[k], graph, _reach, _gate);
						}
					}
					RefreshEntryEdges(storyEntry);
				}
			}
			finally
			{
				_reach.CountersEnabled = true;
			}
		}

		private void RefreshEntryEdges(StoryEntry from)
		{
			List<StoryEntry> list = null;
			List<StoryEntry> list2 = null;
			for (int i = 0; i < _reach.CrossEdges.Count; i++)
			{
				CrossGraphEdge crossGraphEdge = _reach.CrossEdges[i];
				if (!crossGraphEdge.To.IsValid || !_directory.Contains(crossGraphEdge.To.Graph) || crossGraphEdge.DecidedFalse)
				{
					continue;
				}
				if (crossGraphEdge.Conversational)
				{
					if (list == null)
					{
						list = new List<StoryEntry>(2);
					}
					if (!list.Contains(crossGraphEdge.To))
					{
						list.Add(crossGraphEdge.To);
					}
				}
				else
				{
					if (list2 == null)
					{
						list2 = new List<StoryEntry>(2);
					}
					if (!list2.Contains(crossGraphEdge.To))
					{
						list2.Add(crossGraphEdge.To);
					}
				}
			}
			if (list != null)
			{
				_entryOut[from] = list;
			}
			else
			{
				_entryOut.Remove(from);
			}
			if (list2 != null)
			{
				_entryOutSpawned[from] = list2;
			}
			else
			{
				_entryOutSpawned.Remove(from);
			}
		}

		static QuestGiverIndex()
		{
			SuppressLockedQuests = false;
			UnresolvedBookmarkMembers = new List<string>();
			StoryInteractBookmark = Member(typeof(StoryInteractAction), "StoryBookmark", property: true);
			ReadBookmark = Member(typeof(ReadAction), "_readable", property: false);
			SearchBookmark = Member(typeof(SearchActionStory), "_storyBookmark", property: false);
			PickItemBookmark = Member(typeof(PickItemAction), "_storyBookmark", property: false);
			DigOutBookmark = Member(typeof(DigOutAction), "_storyOnDugOut", property: false);
			ShrineBookmark = Member(typeof(SarrasShrineAction), "_bookmark", property: false);
			FirstDamageBookmark = Member(typeof(TriggerStoryOnFirstDamageTaken), "_bookmark", property: false);
			TriggeringRangeBookmark = Member(typeof(TriggeringRange), "_storyToRun", property: false);
			BusyBookmark = Member(typeof(Busy), "_busyStory", property: false);
			TemporaryDeathSpec = Member(typeof(TemporaryDeathElement), "_spec", property: false);
			StagfathersTrialSpec = Member(typeof(StagfathersTrial), "_spec", property: false);
		}

		private static MemberInfo Member(Type t, string name, bool property)
		{
			MemberInfo memberInfo = null;
			string text = "?";
			try
			{
				text = t.Name + "." + name;
				memberInfo = (property ? ((MemberInfo)t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) : ((MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)));
			}
			catch (Exception ex)
			{
				UnresolvedBookmarkMembers.Add(text + " (" + ex.GetType().Name + ")");
				return null;
			}
			if (memberInfo == null)
			{
				UnresolvedBookmarkMembers.Add(text + " (not found)");
			}
			return memberInfo;
		}

		private static StoryBookmark ReadMember(MemberInfo m, object instance)
		{
			if (m == null || instance == null)
			{
				return null;
			}
			FieldInfo fieldInfo = m as FieldInfo;
			if (fieldInfo != null)
			{
				object value = fieldInfo.GetValue(instance);
				return (StoryBookmark)((value is StoryBookmark) ? value : null);
			}
			PropertyInfo propertyInfo = m as PropertyInfo;
			if (propertyInfo == null)
			{
				return null;
			}
			MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
			if (getMethod == null)
			{
				return null;
			}
			object obj = getMethod.Invoke(instance, null);
			return (StoryBookmark)((obj is StoryBookmark) ? obj : null);
		}

		internal static void CollectBookmarks(Location loc, List<StoryEntry> entries, List<string> sources, List<string> errors, ScanCounters counters)
		{
			if (loc == null || entries == null)
			{
				return;
			}
			DialogueAction val = TryElement<DialogueAction>(loc, counters);
			if (val != null)
			{
				StoryBookmark bookmark = null;
				try
				{
					bookmark = val.Bookmark;
				}
				catch (Exception ex)
				{
					Fail(errors, counters, "DialogueAction", ex);
				}
				Take(entries, sources, errors, counters, "DialogueAction", bookmark);
			}
			Take(entries, sources, errors, counters, "StoryInteractAction", SafeRead(errors, counters, "StoryInteractAction", StoryInteractBookmark, TryElement<StoryInteractAction>(loc, counters)));
			Take(entries, sources, errors, counters, "ReadAction", SafeRead(errors, counters, "ReadAction", ReadBookmark, TryElement<ReadAction>(loc, counters)));
			Take(entries, sources, errors, counters, "SearchActionStory", SafeRead(errors, counters, "SearchActionStory", SearchBookmark, TryElement<SearchActionStory>(loc, counters)));
			Take(entries, sources, errors, counters, "PickItemAction", SafeRead(errors, counters, "PickItemAction", PickItemBookmark, TryElement<PickItemAction>(loc, counters)));
			Take(entries, sources, errors, counters, "DigOutAction", SafeRead(errors, counters, "DigOutAction", DigOutBookmark, TryElement<DigOutAction>(loc, counters)));
			Take(entries, sources, errors, counters, "SarrasShrineAction", SafeRead(errors, counters, "SarrasShrineAction", ShrineBookmark, TryElement<SarrasShrineAction>(loc, counters)));
			Take(entries, sources, errors, counters, "TriggerStoryOnFirstDamageTaken", SafeRead(errors, counters, "TriggerStoryOnFirstDamageTaken", FirstDamageBookmark, TryElement<TriggerStoryOnFirstDamageTaken>(loc, counters)));
			Take(entries, sources, errors, counters, "TriggeringRange", SafeRead(errors, counters, "TriggeringRange", TriggeringRangeBookmark, TryElement<TriggeringRange>(loc, counters)));
			Take(entries, sources, errors, counters, "Busy", SafeRead(errors, counters, "Busy", BusyBookmark, TryElement<Busy>(loc, counters)));
			AliveLocation val2 = TryElement<AliveLocation>(loc, counters);
			if (val2 != null)
			{
				StoryBookmark bookmark2 = null;
				try
				{
					AliveLocationAttachment spec = val2.Spec;
					if ((Object)(object)spec != (Object)null)
					{
						bookmark2 = spec.StoryOnDeath;
					}
				}
				catch (Exception ex2)
				{
					Fail(errors, counters, "AliveLocation", ex2);
				}
				Take(entries, sources, errors, counters, "AliveLocation", bookmark2);
			}
			TemporaryDeathAttachment val3 = ReadSpec<TemporaryDeathAttachment>(errors, counters, "TemporaryDeathElement", TemporaryDeathSpec, TryElement<TemporaryDeathElement>(loc, counters));
			if ((Object)(object)val3 != (Object)null)
			{
				StoryBookmark bookmark3 = null;
				try
				{
					bookmark3 = val3.StoryToRunOnTemporaryDeath;
				}
				catch (Exception ex3)
				{
					Fail(errors, counters, "TemporaryDeathElement", ex3);
				}
				Take(entries, sources, errors, counters, "TemporaryDeathElement", bookmark3);
			}
			StagfathersTrialAttachment val4 = ReadSpec<StagfathersTrialAttachment>(errors, counters, "StagfathersTrial", StagfathersTrialSpec, TryElement<StagfathersTrial>(loc, counters));
			if (!((Object)(object)val4 != (Object)null))
			{
				return;
			}
			try
			{
				Take(entries, sources, errors, counters, "StagfathersTrial", val4.startBookmark);
				Take(entries, sources, errors, counters, "StagfathersTrial", val4.completeBookmark);
				Take(entries, sources, errors, counters, "StagfathersTrial", val4.failBookmark);
				Take(entries, sources, errors, counters, "StagfathersTrial", val4.rewardBookmark);
			}
			catch (Exception ex4)
			{
				Fail(errors, counters, "StagfathersTrial", ex4);
			}
		}

		private static TSpec ReadSpec<TSpec>(List<string> errors, ScanCounters counters, string source, MemberInfo member, object element) where TSpec : class
		{
			if (element == null || member == null)
			{
				return null;
			}
			try
			{
				FieldInfo fieldInfo = member as FieldInfo;
				if (fieldInfo != null)
				{
					return fieldInfo.GetValue(element) as TSpec;
				}
				PropertyInfo propertyInfo = member as PropertyInfo;
				if (propertyInfo == null)
				{
					return null;
				}
				MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
				return (getMethod == null) ? null : (getMethod.Invoke(element, null) as TSpec);
			}
			catch (Exception ex)
			{
				Fail(errors, counters, source, ex);
				return null;
			}
		}

		internal static bool HasDialogueAction(Location loc, ScanCounters counters)
		{
			if (loc != null)
			{
				return TryElement<DialogueAction>(loc, counters) != null;
			}
			return false;
		}

		private static T TryElement<T>(Location loc, ScanCounters counters) where T : class, IModel
		{
			try
			{
				return ((Model)loc).TryGetElement<T>();
			}
			catch (Exception)
			{
				if (counters != null)
				{
					counters.NpcBookmarkThrows++;
				}
				return null;
			}
		}

		private static StoryBookmark SafeRead(List<string> errors, ScanCounters counters, string source, MemberInfo member, object element)
		{
			if (element == null)
			{
				return null;
			}
			try
			{
				return ReadMember(member, element);
			}
			catch (Exception ex)
			{
				Fail(errors, counters, source, ex);
				return null;
			}
		}

		private static void Take(List<StoryEntry> entries, List<string> sources, List<string> errors, ScanCounters counters, string source, StoryBookmark bookmark)
		{
			if (bookmark == (StoryBookmark)null)
			{
				return;
			}
			bool isValid;
			try
			{
				isValid = bookmark.IsValid;
			}
			catch (Exception ex)
			{
				Fail(errors, counters, source, ex);
				return;
			}
			if (!isValid)
			{
				return;
			}
			string gUID;
			string chapterName;
			try
			{
				gUID = bookmark.GUID;
				chapterName = bookmark.chapterName;
			}
			catch (Exception ex2)
			{
				Fail(errors, counters, source, ex2);
				return;
			}
			if (!string.IsNullOrEmpty(gUID) && !(gUID == "(null)"))
			{
				if (counters != null)
				{
					ScanReport.Bump(counters.NpcBookmarkSources, source);
				}
				StoryEntry item = new StoryEntry(gUID, chapterName);
				if (!entries.Contains(item))
				{
					entries.Add(item);
				}
				if (sources != null && !sources.Contains(source))
				{
					sources.Add(source);
				}
			}
		}

		private static void Fail(List<string> errors, ScanCounters counters, string source, Exception ex)
		{
			if (counters != null)
			{
				counters.NpcBookmarkThrows++;
			}
			if (errors != null)
			{
				Exception ex2 = ((ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex);
				string item = source + " threw " + ex2.GetType().Name;
				if (!errors.Contains(item))
				{
					errors.Add(item);
				}
			}
		}
	}
}
