using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class EntryModel
	{
		public Dictionary<StoryEntry, List<StoryEntry>> Out;

		public Dictionary<StoryEntry, List<StoryEntry>> OutSpawned;

		public Dictionary<StoryEntry, List<QuestGrantSite>> Sites;

		public HashSet<StoryEntry> Analysed;

		public Dictionary<string, string[]> NamesByGraph;
	}
}
