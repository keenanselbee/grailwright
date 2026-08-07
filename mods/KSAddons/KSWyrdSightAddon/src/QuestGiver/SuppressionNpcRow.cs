using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class SuppressionNpcRow
	{
		public string Name = "";

		public string LocationTemplate = "";

		public readonly List<string> Quests = new List<string>();

		public readonly List<string> Causes = new List<string>();

		public readonly List<string> Graphs = new List<string>();

		public readonly List<string> Entries = new List<string>();

		public string SortGuid = "";
	}
}
