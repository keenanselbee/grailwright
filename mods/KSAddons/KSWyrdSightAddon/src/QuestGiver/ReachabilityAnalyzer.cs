using System;
using System.Collections.Generic;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Runtime;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps;

namespace AvalonUntold
{
	public sealed class ReachabilityAnalyzer
	{
		private struct InEdge
		{
			public StoryChapter From;

			public TriResult Gate;

			public StoryStep Step;

			public bool SelfSettable;
		}

		private const int MaxChapterCauses = 8;

		public const string UnconditionalJumpCause = "unconditionalJump";

		public const string RandomPickCause = "randomPickAlwaysJumps";

		private readonly ScanCounters _counters;

		public bool CountersEnabled = true;

		public bool SequentialTransfers = true;

		private readonly List<FlowEdge> _edges = new List<FlowEdge>(4);

		private readonly Queue<StoryChapter> _work = new Queue<StoryChapter>();

		public readonly Dictionary<StoryChapter, Tri> Reach = new Dictionary<StoryChapter, Tri>(RefComparer<StoryChapter>.Instance);

		public readonly Dictionary<StoryChapter, List<string>> Causes = new Dictionary<StoryChapter, List<string>>(RefComparer<StoryChapter>.Instance);

		public readonly List<CrossGraphEdge> CrossEdges = new List<CrossGraphEdge>();

		private readonly Dictionary<StoryEntry, int> _crossIndex = new Dictionary<StoryEntry, int>();

		public int UnreachableChapters;

		public int FalseChaptersDecided;

		public int FalseChaptersUnmodelled;

		public int CrossEdgesFromUnreachedChapters;

		public bool EntryUnresolved;

		public int EntryOnlyChaptersUnknown;

		public int CauseChaptersCompared;

		public int CauseChaptersDifferFromLegacy;

		public int CauseChaptersPermanenceDiffers;

		public int CauseChaptersOrderSensitive;

		public int CauseChaptersAtCap;

		public bool CauseFixpointBudgetExceeded;

		public const string EntryNotNamedCause = "entryNotNamed";

		private bool _entryScoped;

		private readonly Dictionary<StoryChapter, List<InEdge>> _inEdges = new Dictionary<StoryChapter, List<InEdge>>(RefComparer<StoryChapter>.Instance);

		private readonly HashSet<StoryChapter> _seeded = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, FalseKind> _falseKind = new Dictionary<StoryChapter, FalseKind>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, List<string>> _falseCauses = new Dictionary<StoryChapter, List<string>>(RefComparer<StoryChapter>.Instance);

		private readonly HashSet<StoryChapter> _entryNotNamed = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private readonly Queue<StoryChapter> _falseWork = new Queue<StoryChapter>();

		private readonly List<StoryChapter> _decided = new List<StoryChapter>(16);

		private readonly HashSet<StoryChapter> _causeTop = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, List<StoryChapter>> _causeSucc = new Dictionary<StoryChapter, List<StoryChapter>>(RefComparer<StoryChapter>.Instance);

		private readonly Queue<StoryChapter> _causeWork = new Queue<StoryChapter>();

		private readonly HashSet<StoryChapter> _causeQueued = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private readonly List<string> _causeScratch = new List<string>(4);

		private readonly HashSet<StoryChapter> _causeMixed = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, List<string>> _legacyCauses = new Dictionary<StoryChapter, List<string>>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, List<string>> _legacyCausesDescending = new Dictionary<StoryChapter, List<string>>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<StoryChapter, TriResult> _contGate = new Dictionary<StoryChapter, TriResult>(RefComparer<StoryChapter>.Instance);

		private readonly Dictionary<string, int> _selfSetWrites = new Dictionary<string, int>(StringComparer.Ordinal);

		private int _selfSettableEdges;

		private bool _demoteEnabled = true;

		private readonly Dictionary<StoryChapter, List<string>> _falseCausesNoDemote = new Dictionary<StoryChapter, List<string>>(RefComparer<StoryChapter>.Instance);

		private bool _noDemoteComputed;

		private bool _needCauses;

		private readonly HashSet<StoryChapter> _legacyVisited = new HashSet<StoryChapter>(RefComparer<StoryChapter>.Instance);

		private ScanCounters Counters
		{
			get
			{
				if (!CountersEnabled)
				{
					return null;
				}
				return _counters;
			}
		}

		public int SelfSettableInEdges => _selfSettableEdges;

		private static bool HoldsBookmark(StoryChapter c)
		{
			if (c == null || c.steps == null)
			{
				return false;
			}
			for (int i = 0; i < c.steps.Length; i++)
			{
				if (c.steps[i] is SBookmark)
				{
					return true;
				}
			}
			return false;
		}

		public ReachabilityAnalyzer(ScanCounters counters)
		{
			_counters = counters;
		}

		public void Analyze(StoryGraphRuntime graph, StepGate gate)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			Analyze(graph, gate, EntrySeeding.WholeGraph, null, needCauses: true);
		}

		public void Analyze(StoryGraphRuntime graph, StepGate gate, bool needCauses)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			Analyze(graph, gate, EntrySeeding.WholeGraph, null, needCauses);
		}

		public bool AnalyzeEntry(StoryGraphRuntime graph, StepGate gate, string chapterName)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return AnalyzeEntry(graph, gate, chapterName, needCauses: true);
		}

		public bool AnalyzeEntry(StoryGraphRuntime graph, StepGate gate, string chapterName, bool needCauses)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			Analyze(graph, gate, EntrySeeding.Entry, chapterName, needCauses);
			return !EntryUnresolved;
		}

		private void Analyze(StoryGraphRuntime graph, StepGate gate, EntrySeeding seeding, string chapterName, bool needCauses)
		{
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_023b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			Reach.Clear();
			Causes.Clear();
			CrossEdges.Clear();
			_crossIndex.Clear();
			_work.Clear();
			_inEdges.Clear();
			_seeded.Clear();
			_falseKind.Clear();
			_falseCauses.Clear();
			_entryNotNamed.Clear();
			_falseWork.Clear();
			_contGate.Clear();
			_decided.Clear();
			_causeTop.Clear();
			_causeSucc.Clear();
			_causeWork.Clear();
			_causeQueued.Clear();
			_causeScratch.Clear();
			_causeMixed.Clear();
			_legacyCauses.Clear();
			_legacyCausesDescending.Clear();
			_legacyVisited.Clear();
			_selfSetWrites.Clear();
			_falseCausesNoDemote.Clear();
			_selfSettableEdges = 0;
			_noDemoteComputed = false;
			_demoteEnabled = true;
			_needCauses = needCauses;
			UnreachableChapters = 0;
			FalseChaptersDecided = 0;
			FalseChaptersUnmodelled = 0;
			CrossEdgesFromUnreachedChapters = 0;
			EntryUnresolved = false;
			EntryOnlyChaptersUnknown = 0;
			CauseChaptersCompared = 0;
			CauseChaptersDifferFromLegacy = 0;
			CauseChaptersPermanenceDiffers = 0;
			CauseChaptersOrderSensitive = 0;
			CauseChaptersAtCap = 0;
			CauseFixpointBudgetExceeded = false;
			_entryScoped = false;
			if (seeding == EntrySeeding.Entry)
			{
				SeedEntry(graph, chapterName);
				_entryScoped = !EntryUnresolved;
			}
			else
			{
				SeedWholeGraph(graph, Tri.True);
			}
			Fixpoint(gate);
			ComputeContinuationGates(graph, gate);
			if (needCauses)
			{
				AttributeCauses(graph, gate);
			}
			ClassifyFalses(graph, gate);
			if (needCauses)
			{
				ComputeLegacyFalseCauses(graph, CountersEnabled);
			}
			CollectCrossEdges(graph, gate);
			if (graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter c = graph.chapters[i];
				if (Get(c) == Tri.False)
				{
					UnreachableChapters++;
					if (KindOf(c) == FalseKind.Decided)
					{
						FalseChaptersDecided++;
					}
					else
					{
						FalseChaptersUnmodelled++;
					}
				}
			}
		}

		private void ClassifyFalses(StoryGraphRuntime graph, StepGate gate)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0130: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_0329: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0334: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null)
				{
					continue;
				}
				if (Get(val.continuation) == Tri.False)
				{
					AddInEdge(val.continuation, val, ContinuationGate(val), null);
				}
				if (val.steps == null)
				{
					continue;
				}
				for (int j = 0; j < val.steps.Length; j++)
				{
					StoryStep val2 = val.steps[j];
					if (val2 == null)
					{
						continue;
					}
					StepFlow.Edges(val2, _edges, null);
					if (_edges.Count == 0)
					{
						continue;
					}
					bool flag = false;
					for (int k = 0; k < _edges.Count; k++)
					{
						StoryChapter targetChapter = _edges[k].TargetChapter;
						if (targetChapter != null && Get(targetChapter) == Tri.False)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
					TriResult gate2 = gate.Gate(val2);
					for (int l = 0; l < _edges.Count; l++)
					{
						StoryChapter targetChapter2 = _edges[l].TargetChapter;
						if (targetChapter2 != null && Get(targetChapter2) == Tri.False)
						{
							AddInEdge(targetChapter2, val, gate2, val2);
						}
					}
				}
			}
			for (int m = 0; m < graph.chapters.Length; m++)
			{
				StoryChapter val3 = graph.chapters[m];
				if (val3 != null && Get(val3) == Tri.False && !_seeded.Contains(val3) && (!_inEdges.TryGetValue(val3, out var value) || value == null || value.Count <= 0))
				{
					if (_entryScoped && HoldsBookmark(val3))
					{
						_entryNotNamed.Add(val3);
						EntryOnlyChaptersUnknown++;
					}
					MarkUnmodelled(val3);
				}
			}
			while (_falseWork.Count > 0)
			{
				StoryChapter val4 = _falseWork.Dequeue();
				if (val4.steps != null)
				{
					for (int n = 0; n < val4.steps.Length; n++)
					{
						StoryStep val5 = val4.steps[n];
						if (val5 == null)
						{
							continue;
						}
						StepFlow.Edges(val5, _edges, null);
						if (_edges.Count == 0 || gate.Gate(val5).Value == Tri.False)
						{
							continue;
						}
						for (int num = 0; num < _edges.Count; num++)
						{
							StoryChapter targetChapter3 = _edges[num].TargetChapter;
							if (targetChapter3 != null && Get(targetChapter3) == Tri.False)
							{
								MarkUnmodelled(targetChapter3);
							}
						}
					}
				}
				StoryChapter continuation = val4.continuation;
				if (continuation != null && Get(continuation) == Tri.False && ContinuationGate(val4).Value != Tri.False)
				{
					MarkUnmodelled(continuation);
				}
			}
			for (int num2 = 0; num2 < graph.chapters.Length; num2++)
			{
				StoryChapter val6 = graph.chapters[num2];
				if (val6 != null && Get(val6) == Tri.False && KindOf(val6) != FalseKind.Unmodelled)
				{
					_falseKind[val6] = FalseKind.Decided;
					_decided.Add(val6);
					_causeTop.Add(val6);
				}
			}
			BuildSelfSetWriteIndex(graph);
			MarkSelfSettableEdges(gate);
			if (_selfSettableEdges > 0 && _needCauses)
			{
				_demoteEnabled = false;
				ComputeFalseCauses();
				CopyCausesInto(_falseCausesNoDemote);
				_noDemoteComputed = !CauseFixpointBudgetExceeded;
				ResetCauseState();
			}
			_demoteEnabled = true;
			ComputeFalseCauses();
		}

		private void BuildSelfSetWriteIndex(StoryGraphRuntime graph)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (!_needCauses || _decided.Count == 0 || graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val != null && val.steps != null && (Get(val) != Tri.False || KindOf(val) != FalseKind.Decided))
				{
					for (int j = 0; j < val.steps.Length; j++)
					{
						SelfSetGates.CollectWrites(val.steps[j], _selfSetWrites);
					}
				}
			}
		}

		private void MarkSelfSettableEdges(StepGate gate)
		{
			if (_selfSetWrites.Count == 0)
			{
				return;
			}
			ConditionEvaluator evaluator = gate.Evaluator;
			if (evaluator == null)
			{
				return;
			}
			for (int i = 0; i < _decided.Count; i++)
			{
				if (!_inEdges.TryGetValue(_decided[i], out var value) || value == null)
				{
					continue;
				}
				for (int j = 0; j < value.Count; j++)
				{
					InEdge value2 = value[j];
					if (value2.Gate.Value != Tri.False)
					{
						continue;
					}
					bool flag = false;
					if (value2.Step != null)
					{
						flag = evaluator.StepGateSelfSettable(value2.Step, _selfSetWrites);
					}
					else if (value2.From != null && value2.From.steps != null)
					{
						for (int k = 0; k < value2.From.steps.Length; k++)
						{
							if (flag)
							{
								break;
							}
							StoryStep val = value2.From.steps[k];
							if (val != null && (StepTransfer.IsRandomPick(val) || StepTransfer.Transfers(val) != Tri.False))
							{
								flag = evaluator.StepGateSelfSettable(val, _selfSetWrites);
							}
						}
					}
					if (flag)
					{
						value2.SelfSettable = true;
						value[j] = value2;
						_selfSettableEdges++;
					}
				}
			}
		}

		private void CopyCausesInto(Dictionary<StoryChapter, List<string>> into)
		{
			into.Clear();
			foreach (KeyValuePair<StoryChapter, List<string>> falseCause in _falseCauses)
			{
				into[falseCause.Key] = ((falseCause.Value == null) ? null : new List<string>(falseCause.Value));
			}
		}

		private void ResetCauseState()
		{
			_falseCauses.Clear();
			_causeTop.Clear();
			_causeSucc.Clear();
			_causeWork.Clear();
			_causeQueued.Clear();
			_causeScratch.Clear();
			_causeMixed.Clear();
			CauseFixpointBudgetExceeded = false;
			for (int i = 0; i < _decided.Count; i++)
			{
				_causeTop.Add(_decided[i]);
			}
		}

		public TriResult ChapterResultWithoutSelfSetDemotion(StoryChapter c)
		{
			if (!_noDemoteComputed)
			{
				return ChapterResult(c);
			}
			if (Get(c) != Tri.False || KindOf(c) != FalseKind.Decided)
			{
				return ChapterResult(c);
			}
			if (_falseCausesNoDemote.TryGetValue(c, out var value) && value != null && value.Count > 0)
			{
				return TriResult.WithDecisiveFalses(TriResult.False("lockedChapter"), value);
			}
			return TriResult.FalseUnattributed();
		}

		private void ComputeFalseCauses()
		{
			if (_decided.Count == 0)
			{
				return;
			}
			for (int i = 0; i < _decided.Count; i++)
			{
				StoryChapter val = _decided[i];
				if (!_inEdges.TryGetValue(val, out var value) || value == null)
				{
					continue;
				}
				for (int j = 0; j < value.Count; j++)
				{
					InEdge inEdge = value[j];
					if (inEdge.Gate.Value != Tri.False && inEdge.From != null && KindOf(inEdge.From) == FalseKind.Decided)
					{
						if (!_causeSucc.TryGetValue(inEdge.From, out var value2))
						{
							value2 = new List<StoryChapter>(2);
							_causeSucc[inEdge.From] = value2;
						}
						if (!value2.Contains(val))
						{
							value2.Add(val);
						}
					}
				}
			}
			for (int k = 0; k < _decided.Count; k++)
			{
				EnqueueCause(_decided[k]);
			}
			long num = 40L * (long)_decided.Count + 64;
			while (_causeWork.Count > 0)
			{
				if (--num < 0)
				{
					CauseFixpointBudgetExceeded = true;
					break;
				}
				StoryChapter val2 = _causeWork.Dequeue();
				_causeQueued.Remove(val2);
				if (RecomputeCause(val2) && _causeSucc.TryGetValue(val2, out var value3))
				{
					for (int l = 0; l < value3.Count; l++)
					{
						EnqueueCause(value3[l]);
					}
				}
			}
			if (CauseFixpointBudgetExceeded)
			{
				_causeWork.Clear();
				_causeQueued.Clear();
				for (int m = 0; m < _decided.Count; m++)
				{
					_falseCauses.Remove(_decided[m]);
					_causeMixed.Remove(_decided[m]);
				}
				_causeTop.Clear();
				return;
			}
			foreach (StoryChapter item in _causeTop)
			{
				_falseCauses.Remove(item);
				_causeMixed.Remove(item);
			}
			_causeTop.Clear();
		}

		private void EnqueueCause(StoryChapter c)
		{
			if (c != null && _causeQueued.Add(c))
			{
				_causeWork.Enqueue(c);
			}
		}

		private bool RecomputeCause(StoryChapter c)
		{
			if (!_inEdges.TryGetValue(c, out var value) || value == null || value.Count == 0)
			{
				return SetCause(c, fromScratch: false);
			}
			bool flag = _causeMixed.Contains(c);
			if (!flag)
			{
				for (int i = 0; i < value.Count; i++)
				{
					InEdge inEdge = value[i];
					if (inEdge.Gate.Value == Tri.False)
					{
						if (ConditionEvaluator.MixesVolatileAndPermanent(inEdge.Gate.DecisiveFalses))
						{
							flag = true;
							break;
						}
					}
					else if (inEdge.From != null && KindOf(inEdge.From) == FalseKind.Decided && _causeMixed.Contains(inEdge.From))
					{
						flag = true;
						break;
					}
				}
			}
			bool flag2 = flag && _causeMixed.Add(c);
			bool flag3 = true;
			_causeScratch.Clear();
			for (int j = 0; j < value.Count; j++)
			{
				InEdge inEdge2 = value[j];
				List<string> value2;
				if (inEdge2.Gate.Value == Tri.False)
				{
					value2 = ((_demoteEnabled && inEdge2.SelfSettable) ? null : inEdge2.Gate.DecisiveFalses);
				}
				else if (inEdge2.From != null && KindOf(inEdge2.From) == FalseKind.Decided)
				{
					if (_causeTop.Contains(inEdge2.From))
					{
						continue;
					}
					_falseCauses.TryGetValue(inEdge2.From, out value2);
				}
				else
				{
					value2 = null;
				}
				if (value2 == null || value2.Count == 0)
				{
					flag3 = false;
					_causeScratch.Clear();
					break;
				}
				if (flag3)
				{
					flag3 = false;
					for (int k = 0; k < value2.Count; k++)
					{
						if (_causeScratch.Count >= 8)
						{
							break;
						}
						string text = value2[k];
						if (text != null && !_causeScratch.Contains(text))
						{
							_causeScratch.Add(text);
						}
					}
				}
				else
				{
					for (int num = _causeScratch.Count - 1; num >= 0; num--)
					{
						if (!value2.Contains(_causeScratch[num]))
						{
							_causeScratch.RemoveAt(num);
						}
					}
				}
				if (_causeScratch.Count == 0)
				{
					break;
				}
			}
			if (flag3)
			{
				return flag2;
			}
			return SetCause(c, fromScratch: true) || flag2;
		}

		private bool SetCause(StoryChapter c, bool fromScratch)
		{
			if (!fromScratch)
			{
				_causeScratch.Clear();
			}
			bool num = _causeTop.Contains(c);
			if (!_falseCauses.TryGetValue(c, out var value))
			{
				value = null;
			}
			if (!num)
			{
				if (value == null)
				{
					_causeScratch.Clear();
				}
				else
				{
					for (int num2 = _causeScratch.Count - 1; num2 >= 0; num2--)
					{
						if (!value.Contains(_causeScratch[num2]))
						{
							_causeScratch.RemoveAt(num2);
						}
					}
				}
				if (_causeScratch.Count == (value?.Count ?? 0))
				{
					return false;
				}
			}
			if (value == null)
			{
				value = new List<string>(_causeScratch.Count);
				_falseCauses[c] = value;
			}
			value.Clear();
			for (int i = 0; i < _causeScratch.Count; i++)
			{
				value.Add(_causeScratch[i]);
			}
			_causeTop.Remove(c);
			return true;
		}

		private void ComputeLegacyFalseCauses(StoryGraphRuntime graph, bool alsoDescending)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			if (_decided.Count == 0 || graph.chapters == null)
			{
				return;
			}
			LegacyPass(graph, _legacyCauses, ascending: true);
			if (alsoDescending)
			{
				LegacyPass(graph, _legacyCausesDescending, ascending: false);
			}
			for (int i = 0; i < _decided.Count; i++)
			{
				StoryChapter key = _decided[i];
				CauseChaptersCompared++;
				List<string> value;
				if (_noDemoteComputed)
				{
					_falseCausesNoDemote.TryGetValue(key, out value);
				}
				else
				{
					_falseCauses.TryGetValue(key, out value);
				}
				_legacyCauses.TryGetValue(key, out var value2);
				_falseCauses.TryGetValue(key, out var value3);
				if (value3 != null && value3.Count >= 8)
				{
					CauseChaptersAtCap++;
				}
				if (!SameCauseSet(value, value2))
				{
					CauseChaptersDifferFromLegacy++;
					if (NamesSomethingPermanent(value) != NamesSomethingPermanent(value2))
					{
						CauseChaptersPermanenceDiffers++;
					}
				}
				if (alsoDescending)
				{
					_legacyCausesDescending.TryGetValue(key, out var value4);
					if (!SameCauseSet(value2, value4))
					{
						CauseChaptersOrderSensitive++;
					}
				}
			}
		}

		private void LegacyPass(StoryGraphRuntime graph, Dictionary<StoryChapter, List<string>> into, bool ascending)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			into.Clear();
			int num = graph.chapters.Length;
			_legacyVisited.Clear();
			for (int i = 0; i < num; i++)
			{
				int num2 = (ascending ? i : (num - 1 - i));
				StoryChapter val = graph.chapters[num2];
				if (val == null || KindOf(val) != FalseKind.Decided)
				{
					continue;
				}
				_legacyVisited.Add(val);
				if (!_inEdges.TryGetValue(val, out var value) || value == null)
				{
					continue;
				}
				for (int j = 0; j < value.Count; j++)
				{
					InEdge inEdge = value[j];
					List<string> value2;
					if (inEdge.Gate.Value == Tri.False)
					{
						LegacyAdd(into, val, inEdge.Gate.DecisiveFalses);
					}
					else if (inEdge.From != null && KindOf(inEdge.From) == FalseKind.Decided && _legacyVisited.Contains(inEdge.From) && into.TryGetValue(inEdge.From, out value2))
					{
						LegacyAdd(into, val, value2);
					}
				}
			}
		}

		private static void LegacyAdd(Dictionary<StoryChapter, List<string>> into, StoryChapter c, List<string> causes)
		{
			if (c == null || causes == null || causes.Count == 0)
			{
				return;
			}
			if (!into.TryGetValue(c, out var value))
			{
				value = (into[c] = new List<string>(2));
			}
			for (int i = 0; i < causes.Count; i++)
			{
				if (value.Count >= 8)
				{
					break;
				}
				if (causes[i] != null && !value.Contains(causes[i]))
				{
					value.Add(causes[i]);
				}
			}
		}

		private static bool SameCauseSet(List<string> a, List<string> b)
		{
			int num = a?.Count ?? 0;
			int num2 = b?.Count ?? 0;
			if (num != num2)
			{
				return false;
			}
			for (int i = 0; i < num; i++)
			{
				if (!b.Contains(a[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool NamesSomethingPermanent(List<string> causes)
		{
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

		private void AddInEdge(StoryChapter target, StoryChapter from, TriResult gate, StoryStep step)
		{
			if (target != null)
			{
				if (!_inEdges.TryGetValue(target, out var value))
				{
					value = new List<InEdge>(2);
					_inEdges[target] = value;
				}
				InEdge item = default(InEdge);
				item.From = from;
				item.Gate = gate;
				item.Step = step;
				item.SelfSettable = false;
				value.Add(item);
			}
		}

		private void MarkUnmodelled(StoryChapter c)
		{
			if (c != null && (!_falseKind.TryGetValue(c, out var value) || value != FalseKind.Unmodelled))
			{
				_falseKind[c] = FalseKind.Unmodelled;
				_falseWork.Enqueue(c);
			}
		}

		private FalseKind KindOf(StoryChapter c)
		{
			if (c != null && _falseKind.TryGetValue(c, out var value))
			{
				return value;
			}
			return FalseKind.None;
		}

		private void CollectCrossEdges(StoryGraphRuntime graph, StepGate gate)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0280: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null || val.steps == null)
				{
					continue;
				}
				Tri tri = Get(val);
				bool flag = tri == Tri.False && KindOf(val) == FalseKind.Decided;
				bool flag2 = tri == Tri.False && !flag;
				for (int j = 0; j < val.steps.Length; j++)
				{
					StoryStep val2 = val.steps[j];
					if (val2 == null)
					{
						continue;
					}
					StepFlow.Edges(val2, _edges, null);
					if (_edges.Count == 0)
					{
						continue;
					}
					Tri tri2;
					bool flag3;
					if (flag2)
					{
						tri2 = Tri.Unknown;
						flag3 = false;
					}
					else if (flag)
					{
						tri2 = Tri.False;
						flag3 = true;
					}
					else
					{
						tri2 = TriOps.And(tri, gate.Gate(val2).Value);
						flag3 = tri2 == Tri.False;
					}
					for (int k = 0; k < _edges.Count; k++)
					{
						FlowEdge flowEdge = _edges[k];
						StoryBookmark bookmark = flowEdge.Bookmark;
						if (bookmark == (StoryBookmark)null)
						{
							continue;
						}
						string gUID;
						string chapterName;
						try
						{
							gUID = bookmark.GUID;
							chapterName = bookmark.chapterName;
						}
						catch (Exception)
						{
							continue;
						}
						if (string.IsNullOrEmpty(gUID) || gUID == "(null)")
						{
							continue;
						}
						if (flag2)
						{
							CrossEdgesFromUnreachedChapters++;
						}
						bool selfTargeted = flowEdge.SelfTargeted;
						if (Counters != null)
						{
							ScanCounters.Bump(Counters.CrossEdgeSourceTypes, flowEdge.SourceType);
						}
						StoryEntry storyEntry = new StoryEntry(gUID, chapterName);
						if (_crossIndex.TryGetValue(storyEntry, out var value))
						{
							CrossGraphEdge crossGraphEdge = CrossEdges[value];
							crossGraphEdge.Value = TriOps.Or(crossGraphEdge.Value, tri2);
							crossGraphEdge.FromUnreachedChapter &= flag2;
							crossGraphEdge.DecidedFalse &= flag3;
							if (selfTargeted && !crossGraphEdge.Conversational)
							{
								crossGraphEdge.Conversational = true;
								crossGraphEdge.SourceType = flowEdge.SourceType;
							}
						}
						else
						{
							CrossGraphEdge crossGraphEdge2 = new CrossGraphEdge();
							crossGraphEdge2.To = storyEntry;
							crossGraphEdge2.Value = tri2;
							crossGraphEdge2.FromUnreachedChapter = flag2;
							crossGraphEdge2.DecidedFalse = flag3;
							crossGraphEdge2.Conversational = selfTargeted;
							crossGraphEdge2.SourceType = flowEdge.SourceType;
							_crossIndex[storyEntry] = CrossEdges.Count;
							CrossEdges.Add(crossGraphEdge2);
						}
					}
				}
			}
		}

		public Tri Get(StoryChapter c)
		{
			if (c != null && Reach.TryGetValue(c, out var value))
			{
				return value;
			}
			return Tri.False;
		}

		public TriResult ChapterResult(StoryChapter c)
		{
			switch (Get(c))
			{
			case Tri.True:
				return TriResult.True();
			case Tri.False:
			{
				if (KindOf(c) != FalseKind.Decided)
				{
					return TriResult.Unknown(_entryNotNamed.Contains(c) ? "entryNotNamed" : "orphanChapter");
				}
				if (_falseCauses.TryGetValue(c, out var value2) && value2 != null && value2.Count > 0)
				{
					return TriResult.WithDecisiveFalses(TriResult.False("lockedChapter"), value2);
				}
				return TriResult.FalseUnattributed();
			}
			default:
			{
				TriResult result = TriResult.Unknown("upstreamChapter");
				if (Causes.TryGetValue(c, out var value) && value != null && value.Count > 0)
				{
					result.Causes = new List<string>(value);
				}
				return result;
			}
			}
		}

		public bool ChapterCauseMixed(StoryChapter c)
		{
			if (c != null)
			{
				return _causeMixed.Contains(c);
			}
			return false;
		}

		public TriResult ChapterResultLegacyCauses(StoryChapter c)
		{
			if (Get(c) != Tri.False || KindOf(c) != FalseKind.Decided)
			{
				return ChapterResult(c);
			}
			if (_legacyCauses.TryGetValue(c, out var value) && value != null && value.Count > 0)
			{
				return TriResult.WithDecisiveFalses(TriResult.False("lockedChapter"), value);
			}
			return TriResult.FalseUnattributed();
		}

		private void SeedWholeGraph(StoryGraphRuntime graph, Tri level)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			SeedOne(graph.InitialStoryChapter, level);
			if (graph.startNode != null && graph.startNode.choices != null)
			{
				for (int i = 0; i < graph.startNode.choices.Length; i++)
				{
					SStoryStartChoice val = graph.startNode.choices[i];
					if (val != null)
					{
						SeedOne(val.targetChapter, level);
					}
				}
			}
			if (graph.chapters == null)
			{
				return;
			}
			for (int j = 0; j < graph.chapters.Length; j++)
			{
				StoryChapter val2 = graph.chapters[j];
				if (val2 == null || val2.steps == null)
				{
					continue;
				}
				for (int k = 0; k < val2.steps.Length; k++)
				{
					if (val2.steps[k] is SBookmark)
					{
						SeedOne(val2, level);
						break;
					}
				}
			}
		}

		private void SeedEntry(StoryGraphRuntime graph, string chapterName)
		{
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0103: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0194: Unknown result type (might be due to invalid IL or missing references)
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			bool flag = !string.IsNullOrEmpty(chapterName);
			if (flag)
			{
				bool flag2 = true;
				for (int i = 0; i < chapterName.Length; i++)
				{
					if (!char.IsWhiteSpace(chapterName[i]))
					{
						flag2 = false;
						break;
					}
				}
				flag = !flag2;
			}
			if (flag)
			{
				bool flag3 = true;
				if (graph.chapters != null)
				{
					for (int j = 0; j < graph.chapters.Length; j++)
					{
						StoryChapter val = graph.chapters[j];
						if (val == null || val.steps == null)
						{
							continue;
						}
						bool flag4 = false;
						for (int k = 0; k < val.steps.Length; k++)
						{
							StoryStep obj = val.steps[k];
							SBookmark val2 = (SBookmark)(object)((obj is SBookmark) ? obj : null);
							if (val2 != null)
							{
								string flag5;
								try
								{
									flag5 = val2.flag;
								}
								catch (Exception)
								{
									continue;
								}
								if (string.Equals(flag5, chapterName, StringComparison.Ordinal))
								{
									flag4 = true;
									break;
								}
							}
						}
						if (flag4)
						{
							SeedOne(val, (!flag3) ? Tri.Unknown : Tri.True);
							flag3 = false;
						}
					}
				}
				if (flag3)
				{
					FailOpen(graph);
				}
				return;
			}
			if (graph.startNode == null)
			{
				FailOpen(graph);
				return;
			}
			SStoryStartChoice[] choices = graph.startNode.choices;
			int num = ((choices != null) ? choices.Length : 0);
			if (num == 1)
			{
				if (choices[0] == null || choices[0].targetChapter == null)
				{
					FailOpen(graph);
					return;
				}
				SeedOne(choices[0].targetChapter, Tri.True);
				StoryChapter initialStoryChapter = graph.InitialStoryChapter;
				if (initialStoryChapter != null && initialStoryChapter != choices[0].targetChapter)
				{
					SeedOne(initialStoryChapter, Tri.Unknown);
					if (Counters != null)
					{
						Counters.SelfGraphJumpStartSeeds++;
					}
				}
				return;
			}
			StoryChapter initialStoryChapter2 = graph.InitialStoryChapter;
			if (initialStoryChapter2 == null)
			{
				FailOpen(graph);
				return;
			}
			SeedOne(initialStoryChapter2, Tri.True);
			if (num <= 1)
			{
				return;
			}
			for (int l = 0; l < num; l++)
			{
				if (choices[l] != null)
				{
					SeedOne(choices[l].targetChapter, Tri.Unknown);
				}
			}
		}

		private void FailOpen(StoryGraphRuntime graph)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			EntryUnresolved = true;
			SeedWholeGraph(graph, Tri.Unknown);
		}

		private void SeedOne(StoryChapter c, Tri level)
		{
			if (c != null)
			{
				_seeded.Add(c);
				Relax(c, level);
			}
		}

		private void Fixpoint(StepGate gate)
		{
			while (_work.Count > 0)
			{
				StoryChapter val = _work.Dequeue();
				Tri tri = Get(val);
				if (tri != Tri.False)
				{
					TriResult triResult = ChapterPass(val, gate, tri, relax: true, count: false);
					Relax(val.continuation, TriOps.And(tri, triResult.Value));
				}
			}
		}

		private TriResult ChapterPass(StoryChapter c, StepGate gate, Tri rc, bool relax, bool count)
		{
			TriResult triResult = TriResult.True();
			if (c == null || c.steps == null)
			{
				return triResult;
			}
			bool flag = false;
			Tri a = Tri.False;
			TriResult triResult2 = default(TriResult);
			for (int i = 0; i < c.steps.Length; i++)
			{
				StoryStep val = c.steps[i];
				if (val == null)
				{
					continue;
				}
				StepFlow.Edges(val, _edges, relax ? Counters : null);
				bool flag2 = _edges.Count > 0;
				bool flag3 = StepTransfer.IsRandomPick(val);
				Tri tri = ((!flag3) ? StepTransfer.Transfers(val) : Tri.False);
				if (!flag2 && !flag3 && tri == Tri.False)
				{
					continue;
				}
				if (flag3)
				{
					if (!flag)
					{
						flag = true;
						a = TriOps.And(rc, triResult.Value);
					}
					TriResult triResult3 = gate.Gate(val);
					if (SequentialTransfers)
					{
						triResult2 = TriResult.Or(triResult2, gate.TransferGate(val));
					}
					if (relax && flag2)
					{
						RelaxEdges(TriOps.And(a, triResult3.Value));
					}
					continue;
				}
				if (relax && flag2)
				{
					RelaxEdges(TriOps.And(TriOps.And(rc, triResult.Value), gate.Gate(val).Value));
				}
				if (tri == Tri.False || !SequentialTransfers)
				{
					continue;
				}
				TriResult taken = TriResult.And(gate.TransferGate(val), TransferResult(tri));
				ScanCounters scanCounters = (count ? Counters : null);
				if (scanCounters != null)
				{
					if (taken.Value == Tri.True)
					{
						scanCounters.FallThroughsClosedByDecidedGate++;
					}
					else if (taken.Value == Tri.Unknown)
					{
						scanCounters.FallThroughsNarrowedToUnknown++;
					}
				}
				triResult = TriResult.And(triResult, Negate(taken, val, "unconditionalJump"));
			}
			if (flag && SequentialTransfers)
			{
				triResult = TriResult.And(triResult, Negate(triResult2, null, "randomPickAlwaysJumps"));
			}
			return triResult;
		}

		private void RelaxEdges(Tri val)
		{
			if (val == Tri.False)
			{
				return;
			}
			for (int i = 0; i < _edges.Count; i++)
			{
				if (_edges[i].TargetChapter != null)
				{
					Relax(_edges[i].TargetChapter, val);
				}
			}
		}

		private static TriResult TransferResult(Tri transfers)
		{
			switch (transfers)
			{
			case Tri.True:
				return TriResult.True();
			case Tri.Unknown:
				return TriResult.Unknown("stepMayTransfer");
			default:
				return default(TriResult);
			}
		}

		private static TriResult Negate(TriResult taken, StoryStep step, string fallback)
		{
			TriResult triResult = TriResult.Not(taken);
			if (taken.Value != Tri.True)
			{
				return triResult;
			}
			List<string> list = ConditionEvaluator.StepConditionOrigins(step, "routed:");
			list = ((list != null && list.Count != 0) ? ConditionEvaluator.MarkAllVolatile(list) : new List<string>(1) { fallback });
			return TriResult.WithDecisiveFalses(triResult, list);
		}

		private TriResult ContinuationGate(StoryChapter c)
		{
			if (c != null && _contGate.TryGetValue(c, out var value))
			{
				return value;
			}
			return TriResult.True();
		}

		private void ComputeContinuationGates(StoryGraphRuntime graph, StepGate gate)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null)
				{
					continue;
				}
				TriResult value = ChapterPass(val, gate, Tri.True, relax: false, count: true);
				_contGate[val] = value;
				if (value.Value == Tri.False && Counters != null)
				{
					Counters.ChaptersFallThroughClosed++;
					if (val.continuation != null)
					{
						Counters.ChaptersFallThroughClosedWithContinuation++;
					}
				}
			}
		}

		private void AttributeCauses(StoryGraphRuntime graph, StepGate gate)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (graph.chapters == null)
			{
				return;
			}
			for (int i = 0; i < graph.chapters.Length; i++)
			{
				StoryChapter val = graph.chapters[i];
				if (val == null)
				{
					continue;
				}
				Tri tri = Get(val);
				if (tri == Tri.False)
				{
					continue;
				}
				if (val.continuation != null && Get(val.continuation) == Tri.Unknown)
				{
					TriResult triResult = ContinuationGate(val);
					if (TriOps.And(tri, triResult.Value) == Tri.Unknown)
					{
						if (tri == Tri.Unknown)
						{
							AddCause(val.continuation, "upstreamChapter");
						}
						if (triResult.Value == Tri.Unknown && triResult.Causes != null)
						{
							for (int j = 0; j < triResult.Causes.Count; j++)
							{
								AddCause(val.continuation, triResult.Causes[j]);
							}
						}
					}
				}
				if (val.steps == null)
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
					StepFlow.Edges(val2, _edges, null);
					if (_edges.Count == 0)
					{
						continue;
					}
					TriResult triResult2 = gate.Gate(val2);
					if (TriOps.And(tri, triResult2.Value) != Tri.Unknown)
					{
						continue;
					}
					for (int l = 0; l < _edges.Count; l++)
					{
						StoryChapter targetChapter = _edges[l].TargetChapter;
						if (targetChapter == null || Get(targetChapter) != Tri.Unknown)
						{
							continue;
						}
						if (triResult2.Value == Tri.Unknown && triResult2.Causes != null)
						{
							for (int m = 0; m < triResult2.Causes.Count; m++)
							{
								AddCause(targetChapter, triResult2.Causes[m]);
							}
						}
						if (tri == Tri.Unknown)
						{
							AddCause(targetChapter, "upstreamChapter");
						}
					}
				}
			}
		}

		private void Relax(StoryChapter target, Tri v)
		{
			if (target != null)
			{
				if (!Reach.TryGetValue(target, out var value))
				{
					value = Tri.False;
				}
				if ((int)v > (int)value)
				{
					Reach[target] = v;
					_work.Enqueue(target);
				}
			}
		}

		private void AddCause(StoryChapter c, string cause)
		{
			if (c != null && cause != null)
			{
				if (!Causes.TryGetValue(c, out var value))
				{
					value = new List<string>(2);
					Causes[c] = value;
				}
				if (value.Count < 8 && !value.Contains(cause))
				{
					value.Add(cause);
				}
			}
		}
	}
}
