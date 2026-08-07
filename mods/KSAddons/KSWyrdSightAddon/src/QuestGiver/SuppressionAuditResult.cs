using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class SuppressionAuditResult
	{
		public bool Ran;

		public string NotRunReason = "";

		public long ElapsedMs;

		public bool SuppressionOn;

		public bool PreciseMode;

		public string ModeName = "";

		public int SitesTotal;

		public int SitesAutoComplete;

		public int SitesUntaken;

		public int SuppressedSites;

		public int SuppressedQuests;

		public int SuppressedSitesMixedCause;

		public int SitesSuppressedUnderLegacyCauses;

		public int SitesFreedByCauseFix;

		public int SitesSuppressedOnlyAfterCauseFix;

		public int SitesFreedBySelfSetGate;

		public int SitesHiddenOnlyAfterSelfSetDemotion;

		public int SitesSuppressedWithoutSelfSetDemotion;

		public readonly Dictionary<string, int> SuppressedSitesByCause = new Dictionary<string, int>();

		public readonly Dictionary<string, int> SuppressedQuestsByCause = new Dictionary<string, int>();

		public int UnresolvedSitesAll;

		public int UnresolvedSitesFailOpen;

		public int UnresolvedQuestGuids;

		public int NpcsLoaded;

		public int NpcsWithBookmark;

		public int NpcsWithUntakenGrantable;

		public int NpcsLitSuppressionOff;

		public int NpcsLitSuppressionOn;

		public int NpcsHidden;

		public int NpcsLitOnlyByUnresolved;

		public int NpcsLitGraphModel;

		public int NpcsLitEntryModel;

		public int NarrowedTotal;

		public int NpcsWithNamedEntry;

		public int NpcsWithUnanalysedEntry;

		public int IndexEntryPairs;

		public int IndexEntriesUnknownAtQuery;

		public readonly List<SuppressionNpcRow> Narrowed = new List<SuppressionNpcRow>();

		public bool StructuralOk;

		public int HiddenWithNoCause;

		public int IndexWouldSuppressCount;

		public int IndexUnresolvedQuestRefs;

		public readonly List<SuppressionNpcRow> Hidden = new List<SuppressionNpcRow>();

		public readonly List<SuppressionNpcRow> StillLit = new List<SuppressionNpcRow>();

		public int HiddenTotal;

		public int StillLitTotal;

		public int ExpectedIndexUnresolved
		{
			get
			{
				if (!SuppressionOn)
				{
					return UnresolvedSitesAll;
				}
				return UnresolvedSitesFailOpen;
			}
		}
	}
}
