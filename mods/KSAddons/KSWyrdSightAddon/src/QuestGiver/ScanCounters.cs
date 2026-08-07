using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class ScanCounters
	{
		public int ConditionEvaluations;

		public int ConditionThrows;

		public int LocationRefMatchScans;

		public int StepFlowThrows;

		public int ChaptersTotal;

		public int ChaptersUnreachable;

		public int ChaptersFalseDecided;

		public int ChaptersFalseUnmodelled;

		public int ChaptersDecidedCompared;

		public int CauseChaptersDifferFromLegacy;

		public int CauseChaptersPermanenceDiffers;

		public int CauseChaptersOrderSensitive;

		public int CauseChaptersAtCap;

		public int CauseFixpointBudgetExceededGraphs;

		public int FallThroughsClosedByDecidedGate;

		public int FallThroughsNarrowedToUnknown;

		public int ChaptersFallThroughClosed;

		public int ChaptersFallThroughClosedWithContinuation;

		public int TransferGatesHedgedOnOncePer;

		public int TransferGatesKeptOnConstantOncePer;

		public int SelfGraphJumpStartSeeds;

		public int EntryNotNamedPairs;

		public int EntryPairsAnalysed;

		public int EntriesUnresolved;

		public int CrossEdgesDroppedDecided;

		public int CrossEdgesDroppedDecidedConversational;

		public int CrossEdgesDroppedDecidedSpawned;

		public readonly Dictionary<byte, int> CrossEdgeSourceTypes = new Dictionary<byte, int>();

		public int NpcBookmarkThrows;

		public int SharedGraphs;

		public int SharedGraphsGrantingQuests;

		public const byte HighestKnownStepType = 209;

		public readonly Dictionary<byte, int> StepTypesWithoutEdgeMapping = new Dictionary<byte, int>();

		public readonly Dictionary<byte, int> UnknownStepTypeBytes = new Dictionary<byte, int>();

		public readonly Dictionary<byte, int> UnrecognisedConditionTypes = new Dictionary<byte, int>();

		public readonly Dictionary<byte, int> UnrecognisedGroupTypes = new Dictionary<byte, int>();

		public readonly Dictionary<string, int> NpcBookmarkSources = new Dictionary<string, int>();

		public static void Bump(Dictionary<byte, int> map, byte key)
		{
			map.TryGetValue(key, out var value);
			map[key] = value + 1;
		}
	}
}
