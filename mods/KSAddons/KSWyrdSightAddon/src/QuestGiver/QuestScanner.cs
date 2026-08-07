using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Runtime;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps;
using Awaken.TG.Main.Templates;

namespace AvalonUntold
{
	public sealed class QuestScanner
	{
		private readonly ScanReport _report;

		private readonly TemplatesProvider _tp;

		private readonly GameplayMemory _memory;

		private readonly ManualLogSourceShim _log;

		private readonly StoryArchive _archive = new StoryArchive();

		private Task<bool> _archiveLoad;

		private readonly GraphIndex _index = new GraphIndex();

		private readonly QuestCatalog _catalog = new QuestCatalog();

		private readonly NpcSurvey _npcs = new NpcSurvey();

		private readonly ConditionEvaluator _eval;

		private readonly StepGate _gate;

		private readonly ReachabilityAnalyzer _reach;

		private readonly Dictionary<string, List<string>> _graphOut = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, List<string>> _graphOutSpawned = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, List<QuestGrantSite>> _sitesByGraph = new Dictionary<string, List<QuestGrantSite>>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<StoryEntry, List<StoryEntry>> _entryOut = new Dictionary<StoryEntry, List<StoryEntry>>();

		private readonly Dictionary<StoryEntry, List<StoryEntry>> _entryOutSpawned = new Dictionary<StoryEntry, List<StoryEntry>>();

		private readonly Dictionary<StoryEntry, List<QuestGrantSite>> _entrySites = new Dictionary<StoryEntry, List<QuestGrantSite>>();

		private readonly HashSet<StoryEntry> _analysedEntries = new HashSet<StoryEntry>();

		private readonly Dictionary<string, string[]> _entryNamesByGraph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, StoryGraphRuntime> _retained = new Dictionary<string, StoryGraphRuntime>(StringComparer.OrdinalIgnoreCase);

		public string NotPublishedReason { get; private set; }

		internal bool ArchiveLoadPending =>
			_archiveLoad != null && !_archiveLoad.IsCompleted;

		internal QuestScanner(ScanReport report, TemplatesProvider tp, GameplayMemory memory, ManualLogSourceShim log)
		{
			_report = report;
			_tp = tp;
			_memory = memory;
			_log = log;
			_eval = new ConditionEvaluator(memory, report.Counters);
			_gate = new StepGate(_eval, report.Counters);
			_reach = new ReachabilityAnalyzer(report.Counters);
		}

		public IEnumerator Run(int graphsPerFrame, int maxMillisPerFrame, int maxScanSeconds, bool includeNpcSection, int maxClosureGraphs, bool writeReport)
		{
			Stopwatch total = Stopwatch.StartNew();
			Stopwatch slice = Stopwatch.StartNew();
			string archivePath;
			if (!_archive.TryResolvePath(out archivePath))
			{
				_log.Error("story.arch could not be read: " + _archive.FailureReason);
			}
			else
			{
				try
				{
					_archiveLoad = Task.Run(() => _archive.TryLoadResolvedPath(archivePath));
				}
				catch (Exception exception)
				{
					_log.Error("story.arch background parse could not start: " + exception);
				}

				while (ArchiveLoadPending)
				{
					_report.Frames++;
					yield return null;
				}

				if (_archiveLoad != null)
				{
					try
					{
						if (!_archiveLoad.GetAwaiter().GetResult())
						{
							_log.Error("story.arch could not be read: " + _archive.FailureReason);
						}
					}
					catch (Exception exception)
					{
						_log.Error("story.arch background parse failed: " + exception);
					}
					finally
					{
						_archiveLoad = null;
					}
				}
			}
			_index.Initialize(_archive);
			slice.Restart();
			Step("story base path verification", delegate
			{
				_index.VerifyBasePath(_log);
			});
			if (slice.ElapsedMilliseconds >= maxMillisPerFrame)
			{
				_report.Frames++;
				yield return null;
				slice.Restart();
			}
			IEnumerator catalogJob = _catalog.BuildJob(_tp, _memory, _log, _report, maxMillisPerFrame);
			while (catalogJob.MoveNext())
			{
				_report.Frames++;
				yield return null;
				slice.Restart();
			}
			if (writeReport)
			{
				Step("probes", delegate
				{
					Probes.Run(_report, _tp);
				});
				if (slice.ElapsedMilliseconds >= maxMillisPerFrame)
				{
					_report.Frames++;
					yield return null;
					slice.Restart();
				}
			}
			if (includeNpcSection)
			{
				Step("NPC seed collection", delegate
				{
					_npcs.CollectSeeds(_index, _report.Counters);
				});
			}
			_report.Frames++;
			yield return null;
			slice.Restart();
			int i = 0;
			int inSlice = 0;
			while (!_report.Aborted && _index.BasePathVerified && i < _index.Candidates.Count)
			{
				try
				{
					WalkGraph(_index.Candidates[i]);
				}
				catch (OutOfMemoryException ex)
				{
					_log.Error("scan aborted (out of memory) on graph " + _index.Candidates[i] + ": " + ex);
					_report.Aborted = true;
					_report.AbortReason = "OutOfMemoryException after " + i + "/" + _index.Candidates.Count + " graphs";
				}
				catch (Exception ex2)
				{
					_log.Error("graph walk failed on " + _index.Candidates[i] + ": " + ex2);
					_report.GraphWalkFailures++;
					if (_report.GraphWalkFailureGuids.Count < 20)
					{
						_report.GraphWalkFailureGuids.Add(_index.Candidates[i] + " (" + ex2.GetType().Name + ")");
					}
				}
				i++;
				inSlice++;
				if (inSlice >= graphsPerFrame || slice.ElapsedMilliseconds >= maxMillisPerFrame)
				{
					inSlice = 0;
					_report.Frames++;
					yield return null;
					slice.Restart();
					if (total.Elapsed.TotalSeconds > (double)maxScanSeconds)
					{
						_report.Aborted = true;
						_report.AbortReason = "wall-clock budget of " + maxScanSeconds + "s exceeded after " + i + "/" + _index.Candidates.Count + " graphs";
					}
				}
			}
			try
			{
				Aggregate();
			}
			catch (Exception ex3)
			{
				_log.Error("aggregation failed: " + ex3);
				_report.AggregationFailed = true;
				_report.AggregationFailReason = ex3.GetType().Name;
			}
			_report.Frames++;
			yield return null;
			slice.Restart();
			if (includeNpcSection)
			{
				try
				{
					_npcs.Build(_report, _entryOut, _entryOutSpawned, _entrySites, _analysedEntries, _sitesByGraph, _catalog, maxClosureGraphs);
				}
				catch (Exception ex4)
				{
					_log.Error("NPC survey failed: " + ex4);
					_report.SetupFailures.Add("NPC survey: " + ex4.GetType().Name + " " + ex4.Message);
				}
				_report.Frames++;
				yield return null;
				slice.Restart();
			}
			try
			{
				if (_archive.Ok && _index.BasePathVerified && !_report.AggregationFailed && !_report.Aborted && _report.GraphsLoaded > 0 && _catalog.Quests.Count > 0)
				{
					List<string> list = new List<string>(_catalog.Quests.Count);
					for (int num = 0; num < _catalog.Quests.Count; num++)
					{
						list.Add(_catalog.Quests[num].Guid);
					}
					QuestGiverIndex questGiverIndex = QuestGiverIndex.Current ?? new QuestGiverIndex();
					EntryModel entryModel = new EntryModel();
					entryModel.Out = _entryOut;
					entryModel.OutSpawned = _entryOutSpawned;
					entryModel.Sites = _entrySites;
					entryModel.Analysed = _analysedEntries;
					entryModel.NamesByGraph = _entryNamesByGraph;
					questGiverIndex.Populate(_archive.Guids, _sitesByGraph, _graphOut, _graphOutSpawned, _retained, entryModel, list, _memory, maxClosureGraphs, _report.GraphsLoaded, _archive.DirectoryCount);
					QuestGiverIndex.Current = questGiverIndex;
					_report.GraphsWithAvailableQuest = questGiverIndex.GraphsWithAvailableQuest;
					_report.GraphsWithGrantSites = questGiverIndex.GraphsWithGrantSites;
					_report.SpawnedOnlyGrantingGraphs = questGiverIndex.SpawnedOnlyGrantingGraphs;
					_report.ClosureMax = questGiverIndex.ClosureMax;
					_report.ClosureMedian = questGiverIndex.ClosureMedian;
					_report.ClosurePopulation = questGiverIndex.ClosurePopulation;
				}
				else
				{
					string text = "archive ok=" + _archive.Ok + ", base path verified=" + _index.BasePathVerified + ", aggregation failed=" + _report.AggregationFailed + ", aborted=" + _report.Aborted + ", graphs loaded=" + _report.GraphsLoaded + ", quest catalogue=" + _catalog.Quests.Count + " grantable quests";
					_report.SetupFailures.Add("QuestGiverIndex was NOT published: the run was not complete enough to trust (" + text + ")");
					_log.Error("quest index NOT published - NOTHING WILL GLOW. " + text);
					NotPublishedReason = text;
				}
			}
			catch (Exception ex5)
			{
				_log.Error("publishing the quest-giver index failed: " + ex5);
				_report.SetupFailures.Add("publish quest-giver index: " + ex5.GetType().Name + " " + ex5.Message);
				NotPublishedReason = "publication threw " + ex5.GetType().Name;
			}
			_report.Frames++;
			yield return null;
			slice.Restart();
			if (writeReport)
			{
				try
				{
					Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (KeyValuePair<string, QuestRow> item in _catalog.ByGuid)
					{
						if (!dictionary.ContainsKey(item.Key))
						{
							dictionary.Add(item.Key, item.Value.Name);
						}
					}
					_report.Suppression = SuppressionAudit.Run(QuestGiverIndex.Current, dictionary, true);
				}
				catch (Exception ex6)
				{
					_log.Error("suppression recount failed: " + ex6);
					_report.SetupFailures.Add("suppression recount: " + ex6.GetType().Name + " " + ex6.Message);
				}
			}
			total.Stop();
			_report.ElapsedMs = total.ElapsedMilliseconds;
			CopyCounters();
		}

		private static int CountEdges(Dictionary<StoryEntry, List<StoryEntry>> map)
		{
			int num = 0;
			foreach (KeyValuePair<StoryEntry, List<StoryEntry>> item in map)
			{
				num += item.Value.Count;
			}
			return num;
		}

		private void CopyCounters()
		{
			_report.EntryPairs = _analysedEntries.Count;
			_report.EntryOutEdges = CountEdges(_entryOut);
			_report.EntryOutSpawnedEdges = CountEdges(_entryOutSpawned);
			_report.ArchiveOk = _archive.Ok;
			_report.ArchiveFailureReason = _archive.FailureReason;
			_report.ArchivePath = _archive.ArchivePath ?? "";
			_report.ArchivePathsTried = string.Join(" ; ", _archive.PathsTried.ToArray());
			_report.ArchiveBytes = _archive.ArchiveBytes;
			_report.ArchiveFormatVersion = _archive.UnityFsVersion;
			_report.ArchiveBlockCount = _archive.BlockCount;
			_report.ArchiveParseMillis = _archive.ParseMillis;
			_report.DirectoryCount = _archive.DirectoryCount;
			_report.ArchiveNonConformingNames = _archive.NonConformingNames;
			_report.ArchiveMixedCaseNames = _archive.MixedCaseNames;
			_report.ArchiveNonConformingSample = string.Join(", ", _archive.SampleNonConformingNames.ToArray());
			_report.BasePathVerified = _index.BasePathVerified;
			_report.BasePathNote = _index.BasePathNote;
			_report.BasePathMillis = _index.BasePathMillis;
			_report.NpcSeededGraphs = _index.NpcSeededGraphs;
			_report.CrossReferencedGraphs = _index.CrossReferencedGraphs;
			_report.NpcBookmarkRefs = _index.NpcBookmarkRefs;
			_report.NpcBookmarkRefsOutsideArchive = _index.NpcBookmarkRefsOutsideArchive;
			_report.CrossGraphRefs = _index.CrossGraphRefs;
			_report.CrossGraphRefsOutsideArchive = _index.CrossGraphRefsOutsideArchive;
			_report.SampleDanglingBookmarks.AddRange(_index.SampleDanglingBookmarks);
			_report.UniqueCandidates = _index.Candidates.Count;
			_report.GraphLoadAttempts = _index.LoadAttempts;
			_report.GraphsFailedToParse = _index.ParseFailures;
			_report.ParseFailureGuids.AddRange(_index.ParseFailureGuids);
			_report.RefusedNotInDirectory = _index.RefusedNotInDirectory;
			_report.RetainedGraphs = _retained.Count;
		}

		private void Step(string name, Action body)
		{
			try
			{
				body();
			}
			catch (Exception ex)
			{
				_log.Error("setup step '" + name + "' failed: " + ex);
				_report.SetupFailures.Add(name + ": " + ex.GetType().Name + " " + ex.Message);
			}
		}

		private void WalkGraph(string guid)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_05df: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_0611: Unknown result type (might be due to invalid IL or missing references)
			//IL_0619: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_05b9: Unknown result type (might be due to invalid IL or missing references)
			if (!_index.TryGet(guid, out var graph))
			{
				return;
			}
			_report.GraphsLoaded++;
			_eval.BeginGraph(guid, graph.sharedBetweenMultipleNPCs, null);
			_gate.BeginGraph();
			_reach.CountersEnabled = true;
			_reach.Analyze(graph, _gate, HasGrantSites(graph));
			_report.Counters.ChaptersTotal += ((graph.chapters != null) ? graph.chapters.Length : 0);
			_report.Counters.ChaptersUnreachable += _reach.UnreachableChapters;
			_report.Counters.ChaptersFalseDecided += _reach.FalseChaptersDecided;
			_report.Counters.ChaptersFalseUnmodelled += _reach.FalseChaptersUnmodelled;
			_report.Counters.ChaptersDecidedCompared += _reach.CauseChaptersCompared;
			_report.Counters.CauseChaptersDifferFromLegacy += _reach.CauseChaptersDifferFromLegacy;
			_report.Counters.CauseChaptersPermanenceDiffers += _reach.CauseChaptersPermanenceDiffers;
			_report.Counters.CauseChaptersOrderSensitive += _reach.CauseChaptersOrderSensitive;
			_report.Counters.CauseChaptersAtCap += _reach.CauseChaptersAtCap;
			if (_reach.CauseFixpointBudgetExceeded)
			{
				_report.Counters.CauseFixpointBudgetExceededGraphs++;
			}
			_report.CrossEdgesFromUnreachedChapters += _reach.CrossEdgesFromUnreachedChapters;
			if (graph.sharedBetweenMultipleNPCs)
			{
				_report.Counters.SharedGraphs++;
			}
			for (int i = 0; i < _reach.CrossEdges.Count; i++)
			{
				CrossGraphEdge crossGraphEdge = _reach.CrossEdges[i];
				if (_index.NoteCrossGraphRef(crossGraphEdge.To.Graph))
				{
					if (crossGraphEdge.Value == Tri.False)
					{
						_report.CrossEdgesDroppedAtFalse++;
					}
					Dictionary<string, List<string>> dictionary = (crossGraphEdge.Conversational ? _graphOut : _graphOutSpawned);
					if (crossGraphEdge.Conversational)
					{
						_report.ConversationalEdges++;
					}
					else
					{
						_report.SpawnedEdges++;
					}
					if (!dictionary.TryGetValue(guid, out var value))
					{
						value = (dictionary[guid] = new List<string>(2));
					}
					if (!value.Contains(crossGraphEdge.To.Graph))
					{
						value.Add(crossGraphEdge.To.Graph);
					}
				}
			}
			if (graph.chapters == null)
			{
				return;
			}
			bool flag = false;
			bool flag2 = false;
			for (int j = 0; j < graph.chapters.Length; j++)
			{
				StoryChapter val = graph.chapters[j];
				if (val == null || val.steps == null)
				{
					continue;
				}
				for (int k = 0; k < val.steps.Length; k++)
				{
					StoryStep val2 = val.steps[k];
					if (val2 == null)
					{
						continue;
					}
					byte type;
					try
					{
						type = val2.Type;
					}
					catch (Exception)
					{
						continue;
					}
					if (type != 2 && type != 3)
					{
						continue;
					}
					string text = null;
					bool flag3 = type == 3;
					if (type == 2)
					{
						SQuestAdd val3 = (SQuestAdd)(object)((val2 is SQuestAdd) ? val2 : null);
						if (val3 == null)
						{
							_report.QuestAddCastFailures++;
							continue;
						}
						if (val3.questRef == (TemplateReference)null || !val3.questRef.IsSet)
						{
							_report.QuestAddUnsetRef++;
							continue;
						}
						text = val3.questRef.GUID;
					}
					else
					{
						SQuestComplete val4 = (SQuestComplete)(object)((val2 is SQuestComplete) ? val2 : null);
						if (val4 == null)
						{
							_report.QuestCompleteCastFailures++;
							continue;
						}
						if (val4.questTemplate == (TemplateReference)null || !val4.questTemplate.IsSet)
						{
							_report.QuestCompleteUnsetRef++;
							continue;
						}
						text = val4.questTemplate.GUID;
					}
					if (string.IsNullOrEmpty(text))
					{
						continue;
					}
					TriResult triResult = _gate.Gate(val2);
					TriResult composed = TriResult.And(_reach.ChapterResult(val), triResult);
					TriResult triResult2 = TriResult.And(_reach.ChapterResultLegacyCauses(val), triResult);
					TriResult triResult3 = TriResult.And(_reach.ChapterResultWithoutSelfSetDemotion(val), triResult);
					QuestGrantSite questGrantSite = new QuestGrantSite();
					questGrantSite.LegacyCauseWouldSuppress = QuestGiverIndex.WouldSuppressVerdict(triResult2.Value, triResult2.DecisiveFalses);
					questGrantSite.NoSelfSetDemotionWouldSuppress = QuestGiverIndex.WouldSuppressVerdict(triResult3.Value, triResult3.DecisiveFalses);
					questGrantSite.QuestGuid = text;
					questGrantSite.GraphGuid = guid;
					questGrantSite.ChapterId = j;
					questGrantSite.StepIndex = k;
					questGrantSite.AutoCompletes = flag3;
					questGrantSite.Availability = composed.Value;
					questGrantSite.Causes = composed.Causes;
					questGrantSite.DecisiveFalses = composed.DecisiveFalses;
					questGrantSite.CauseMixedVolatileAndPermanent = MixedCauseHazard(_reach, val, triResult, composed);
					if (!_sitesByGraph.TryGetValue(guid, out var value2))
					{
						value2 = new List<QuestGrantSite>(2);
						_sitesByGraph[guid] = value2;
					}
					value2.Add(questGrantSite);
					if (flag3)
					{
						_report.QuestCompleteSites++;
						continue;
					}
					_report.QuestAddSites++;
					flag = true;
					if (!flag2)
					{
						_retained[guid] = graph;
						flag2 = true;
					}
				}
			}
			if (flag && graph.sharedBetweenMultipleNPCs)
			{
				_report.Counters.SharedGraphsGrantingQuests++;
			}
			EntryPass(guid, graph);
			PreNarrowingPass(guid, graph);
		}

		private void PreNarrowingPass(string guid, StoryGraphRuntime graph)
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			if (!_sitesByGraph.TryGetValue(guid, out var value) || value == null || value.Count == 0)
			{
				return;
			}
			_reach.CountersEnabled = false;
			_reach.SequentialTransfers = false;
			try
			{
				_reach.Analyze(graph, _gate, needCauses: false);
				for (int i = 0; i < value.Count; i++)
				{
					QuestGrantSite questGrantSite = value[i];
					QuestGrantSite questGrantSite2 = new QuestGrantSite();
					questGrantSite2.ChapterId = questGrantSite.ChapterId;
					questGrantSite2.StepIndex = questGrantSite.StepIndex;
					questGrantSite2.Availability = questGrantSite.Availability;
					EvaluateSite(questGrantSite2, graph, _reach, _gate);
					questGrantSite.AvailabilityPreNarrowing = questGrantSite2.Availability;
				}
			}
			finally
			{
				_reach.SequentialTransfers = true;
				_reach.CountersEnabled = true;
			}
		}

		private void EntryPass(string guid, StoryGraphRuntime graph)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			string[] array = EntryNames(graph);
			_entryNamesByGraph[guid] = array;
			if (!_sitesByGraph.TryGetValue(guid, out var value))
			{
				value = null;
			}
			bool flag = value == null && _reach.CrossEdges.Count == 0;
			bool flag2 = array.Length == 1 && array[0] == null && UnionPassIsStartEntry(graph);
			_reach.CountersEnabled = false;
			try
			{
				for (int i = 0; i < array.Length; i++)
				{
					StoryEntry storyEntry = new StoryEntry(guid, array[i]);
					_analysedEntries.Add(storyEntry);
					_report.Counters.EntryPairsAnalysed++;
					if (flag)
					{
						continue;
					}
					if (!flag2 || i != 0)
					{
						if (!_reach.AnalyzeEntry(graph, _gate, array[i], value != null))
						{
							_report.Counters.EntriesUnresolved++;
						}
						_report.Counters.EntryNotNamedPairs += _reach.EntryOnlyChaptersUnknown;
						if (_reach.CauseFixpointBudgetExceeded)
						{
							_report.Counters.CauseFixpointBudgetExceededGraphs++;
						}
					}
					CollectEntryEdges(graph, storyEntry);
					if (value != null)
					{
						BuildEntrySites(storyEntry, graph, value);
					}
				}
			}
			finally
			{
				_reach.CountersEnabled = true;
			}
		}

		private static bool HasGrantSites(StoryGraphRuntime graph)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return false;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null || val.steps == null)
				{
					continue;
				}
				for (int j = 0; j < val.steps.Length; j++)
				{
					StoryStep val2 = val.steps[j];
					if (val2 != null)
					{
						byte type;
						try
						{
							type = val2.Type;
						}
						catch (Exception)
						{
							continue;
						}
						if (type == 2 || type == 3)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private static bool UnionPassIsStartEntry(StoryGraphRuntime graph)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			if (graph.startNode == null || graph.InitialStoryChapter == null)
			{
				return false;
			}
			if (graph.startNode.choices != null && graph.startNode.choices.Length != 0)
			{
				return false;
			}
			if (graph.chapters == null)
			{
				return true;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null || val.steps == null)
				{
					continue;
				}
				for (int j = 0; j < val.steps.Length; j++)
				{
					if (val.steps[j] is SBookmark)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static string[] EntryNames(StoryGraphRuntime graph)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			List<string> list = new List<string>(2);
			list.Add(null);
			if (graph.chapters == null)
			{
				return list.ToArray();
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null || val.steps == null)
				{
					continue;
				}
				for (int j = 0; j < val.steps.Length; j++)
				{
					StoryStep obj = val.steps[j];
					SBookmark val2 = (SBookmark)(object)((obj is SBookmark) ? obj : null);
					if (val2 != null)
					{
						string flag;
						try
						{
							flag = val2.flag;
						}
						catch (Exception)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(flag) && !list.Contains(flag))
						{
							list.Add(flag);
						}
					}
				}
			}
			return list.ToArray();
		}

		private void CollectEntryEdges(StoryGraphRuntime graph, StoryEntry from)
		{
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			for (int i = 0; i < _reach.CrossEdges.Count; i++)
			{
				CrossGraphEdge crossGraphEdge = _reach.CrossEdges[i];
				if (!_index.InDirectory(crossGraphEdge.To.Graph))
				{
					continue;
				}
				if (crossGraphEdge.DecidedFalse)
				{
					_report.Counters.CrossEdgesDroppedDecided++;
					if (crossGraphEdge.Conversational)
					{
						_report.Counters.CrossEdgesDroppedDecidedConversational++;
					}
					else
					{
						_report.Counters.CrossEdgesDroppedDecidedSpawned++;
					}
					RetainForReGating(from.Graph, graph);
					continue;
				}
				Dictionary<StoryEntry, List<StoryEntry>> dictionary = (crossGraphEdge.Conversational ? _entryOut : _entryOutSpawned);
				if (!dictionary.TryGetValue(from, out var value))
				{
					value = (dictionary[from] = new List<StoryEntry>(2));
				}
				if (!value.Contains(crossGraphEdge.To))
				{
					value.Add(crossGraphEdge.To);
				}
			}
		}

		private void RetainForReGating(string guid, StoryGraphRuntime graph)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			if (!_retained.ContainsKey(guid))
			{
				_retained[guid] = graph;
			}
		}

		private void BuildEntrySites(StoryEntry entry, StoryGraphRuntime graph, List<QuestGrantSite> sites)
		{
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			List<QuestGrantSite> list = new List<QuestGrantSite>(sites.Count);
			for (int i = 0; i < sites.Count; i++)
			{
				QuestGrantSite questGrantSite = sites[i];
				QuestGrantSite questGrantSite2 = new QuestGrantSite();
				questGrantSite2.QuestGuid = questGrantSite.QuestGuid;
				questGrantSite2.GraphGuid = questGrantSite.GraphGuid;
				questGrantSite2.ChapterId = questGrantSite.ChapterId;
				questGrantSite2.StepIndex = questGrantSite.StepIndex;
				questGrantSite2.AutoCompletes = questGrantSite.AutoCompletes;
				EvaluateSite(questGrantSite2, graph, _reach, _gate);
				list.Add(questGrantSite2);
			}
			_entrySites[entry] = list;
		}

		private static bool MixedCauseHazard(ReachabilityAnalyzer reach, StoryChapter chapter, TriResult stepGate, TriResult composed)
		{
			if (composed.Value != Tri.False)
			{
				return false;
			}
			if (!reach.ChapterCauseMixed(chapter) && !ConditionEvaluator.MixesVolatileAndPermanent(stepGate.DecisiveFalses))
			{
				return ConditionEvaluator.MixesVolatileAndPermanent(composed.DecisiveFalses);
			}
			return true;
		}

		internal static void EvaluateSite(QuestGrantSite q, StoryGraphRuntime graph, ReachabilityAnalyzer reach, StepGate gate)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null || q.ChapterId < 0 || q.ChapterId >= graph.chapters.Length)
			{
				return;
			}
			StoryChapter val = graph.chapters[q.ChapterId];
			if (val != null && val.steps != null && q.StepIndex >= 0 && q.StepIndex < val.steps.Length)
			{
				StoryStep val2 = val.steps[q.StepIndex];
				if (val2 != null)
				{
					TriResult triResult = gate.Gate(val2);
					TriResult composed = TriResult.And(reach.ChapterResult(val), triResult);
					q.Availability = composed.Value;
					q.Causes = composed.Causes;
					q.DecisiveFalses = composed.DecisiveFalses;
					q.CauseMixedVolatileAndPermanent = MixedCauseHazard(reach, val, triResult, composed);
					TriResult triResult2 = TriResult.And(reach.ChapterResultLegacyCauses(val), triResult);
					q.LegacyCauseWouldSuppress = QuestGiverIndex.WouldSuppressVerdict(triResult2.Value, triResult2.DecisiveFalses);
					TriResult triResult3 = TriResult.And(reach.ChapterResultWithoutSelfSetDemotion(val), triResult);
					q.NoSelfSetDemotionWouldSuppress = QuestGiverIndex.WouldSuppressVerdict(triResult3.Value, triResult3.DecisiveFalses);
				}
			}
		}

		private void Aggregate()
		{
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			foreach (KeyValuePair<string, List<QuestGrantSite>> item in _sitesByGraph)
			{
				List<QuestGrantSite> value = item.Value;
				for (int i = 0; i < value.Count; i++)
				{
					QuestGrantSite questGrantSite = value[i];
					if (!_catalog.ByGuid.TryGetValue(questGrantSite.QuestGuid, out var value2))
					{
						_report.OrphanQuestRefs++;
						continue;
					}
					value2.GrantingGraphs.Add(questGrantSite.GraphGuid);
					if (questGrantSite.AutoCompletes)
					{
						value2.AutoCompleteSites.Add(questGrantSite);
						continue;
					}
					value2.Sites.Add(questGrantSite);
					value2.AvailableGrantingGraphs.Add(questGrantSite.GraphGuid);
					value2.Availability = TriOps.Join(value2.Availability, questGrantSite.Availability);
					value2.AvailabilityPreNarrowing = TriOps.Join(value2.AvailabilityPreNarrowing, questGrantSite.AvailabilityPreNarrowing);
				}
			}
			_report.Quests.AddRange(_catalog.Quests);
			_report.Achievements.AddRange(_catalog.Achievements);
			for (int j = 0; j < _catalog.Quests.Count; j++)
			{
				if ((int)_catalog.Quests[j].State == 0)
				{
					Attribute(_catalog.Quests[j]);
				}
			}
		}

		private void Attribute(QuestRow row)
		{
			if (!row.HasSites)
			{
				return;
			}
			HashSet<string> hashSet = new HashSet<string>();
			if (row.Availability == Tri.Unknown)
			{
				for (int i = 0; i < row.Sites.Count; i++)
				{
					QuestGrantSite questGrantSite = row.Sites[i];
					if (questGrantSite.Availability == Tri.Unknown && questGrantSite.Causes != null)
					{
						for (int j = 0; j < questGrantSite.Causes.Count; j++)
						{
							ScanReport.Bump(_report.UnknownCauseRaw, questGrantSite.Causes[j]);
							hashSet.Add(questGrantSite.Causes[j]);
						}
					}
				}
				{
					foreach (string item in hashSet)
					{
						ScanReport.Bump(_report.UnknownCauseQuests, item);
					}
					return;
				}
			}
			if (row.Availability != Tri.False)
			{
				return;
			}
			bool flag = false;
			bool flag2 = true;
			for (int k = 0; k < row.Sites.Count; k++)
			{
				QuestGrantSite questGrantSite2 = row.Sites[k];
				if (questGrantSite2.DecisiveFalses == null)
				{
					continue;
				}
				for (int l = 0; l < questGrantSite2.DecisiveFalses.Count; l++)
				{
					string text = questGrantSite2.DecisiveFalses[l];
					ScanReport.Bump(_report.LockedCauseRaw, text);
					hashSet.Add(text);
					flag = true;
					if (!text.EndsWith("(volatile)", StringComparison.Ordinal))
					{
						flag2 = false;
					}
				}
			}
			foreach (string item2 in hashSet)
			{
				ScanReport.Bump(_report.LockedCauseQuests, item2);
			}
			if (flag && flag2)
			{
				_report.LockedByVolatileOnly++;
			}
		}
	}
}
