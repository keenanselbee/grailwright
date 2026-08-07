using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class ScanReport
	{
		public string Timestamp;

		public string HeroContext = "unknown";

		public long ElapsedMs;

		public int Frames;

		public bool Aborted;

		public string AbortReason;

		public readonly List<string> SetupFailures = new List<string>();

		public int GraphWalkFailures;

		public readonly List<string> GraphWalkFailureGuids = new List<string>();

		public bool AggregationFailed;

		public string AggregationFailReason;

		public readonly List<ProbeResult> Probes = new List<ProbeResult>();

		public readonly List<QuestRow> Quests = new List<QuestRow>();

		public readonly List<QuestRow> Achievements = new List<QuestRow>();

		public readonly List<NpcRow> Npcs = new List<NpcRow>();

		public ScanCounters Counters = new ScanCounters();

		public bool ArchiveOk;

		public string ArchiveFailureReason;

		public string ArchivePath = "";

		public string ArchivePathsTried = "";

		public long ArchiveBytes;

		public int ArchiveFormatVersion;

		public int ArchiveBlockCount;

		public long ArchiveParseMillis = -1L;

		public int DirectoryCount;

		public int ArchiveNonConformingNames;

		public int ArchiveMixedCaseNames;

		public string ArchiveNonConformingSample = "";

		public bool BasePathVerified;

		public string BasePathNote = "";

		public long BasePathMillis = -1L;

		public int NpcSeededGraphs;

		public int CrossReferencedGraphs;

		public int NpcBookmarkRefs;

		public int NpcBookmarkRefsOutsideArchive;

		public int CrossGraphRefs;

		public int CrossGraphRefsOutsideArchive;

		public readonly List<string> SampleDanglingBookmarks = new List<string>();

		public int CrossEdgesFromUnreachedChapters;

		public int CrossEdgesDroppedAtFalse;

		public int ConversationalEdges;

		public int SpawnedEdges;

		public int SpawnedOnlyGrantingGraphs;

		public int SpawnedOnlyGrantingEntries;

		public int ClosureMax;

		public int ClosureMedian;

		public int ClosurePopulation;

		public int EntryPairs;

		public int EntryOutEdges;

		public int EntryOutSpawnedEdges;

		public int UniqueCandidates;

		public int GraphLoadAttempts;

		public int GraphsLoaded;

		public int GraphsFailedToParse;

		public readonly List<string> ParseFailureGuids = new List<string>();

		public int RefusedNotInDirectory;

		public int QuestAddSites;

		public int QuestCompleteSites;

		public int QuestAddCastFailures;

		public int QuestCompleteCastFailures;

		public int QuestAddUnsetRef;

		public int QuestCompleteUnsetRef;

		public int OrphanQuestRefs;

		public int RetainedGraphs;

		public int GraphsWithAvailableQuest;

		public int GraphsWithGrantSites;

		public int NpcsWithDialogue;

		public int NpcsWithAnyBookmark;

		public int NpcsWithGrantable;

		public SuppressionAuditResult Suppression;

		public readonly Dictionary<string, int> UnknownCauseQuests = new Dictionary<string, int>();

		public readonly Dictionary<string, int> UnknownCauseRaw = new Dictionary<string, int>();

		public readonly Dictionary<string, int> LockedCauseQuests = new Dictionary<string, int>();

		public readonly Dictionary<string, int> LockedCauseRaw = new Dictionary<string, int>();

		public int LockedByVolatileOnly;

		public static void Bump(Dictionary<string, int> map, string key)
		{
			map.TryGetValue(key, out var value);
			map[key] = value + 1;
		}
	}
}
