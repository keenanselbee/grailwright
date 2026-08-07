using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class NpcRow
	{
		public string Name;

		public string LocationTemplate;

		public readonly List<string> GraphGuids = new List<string>();

		public readonly List<string> BookmarkSources = new List<string>();

		public bool HasDialogueAction;

		public int DanglingBookmarks;

		public int ReachableGraphs;

		public int NamedEntries;

		public int UnanalysedEntries;

		public bool ClosureCapped;

		public readonly List<string> UntakenGrantable = new List<string>();

		public readonly List<string> UntakenAutoCompleted = new List<string>();

		public Tri BestAvailability;

		public bool HasAnyGrantable;

		public string Error;
	}
}
