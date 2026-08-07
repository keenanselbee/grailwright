using System;
using System.Collections.Generic;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Stories.Quests.UI;
using Awaken.TG.Main.Templates;

namespace AvalonUntold
{
	public sealed class QuestRow
	{
		public string Guid;

		public string Name;

		public QuestType Type;

		public QuestCategory Category;

		public TemplateType TemplateType;

		public bool IsAchievement;

		public QuestState State;

		public readonly List<QuestGrantSite> Sites = new List<QuestGrantSite>();

		public readonly List<QuestGrantSite> AutoCompleteSites = new List<QuestGrantSite>();

		public readonly HashSet<string> GrantingGraphs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public readonly HashSet<string> AvailableGrantingGraphs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public Tri Availability;

		public Tri AvailabilityPreNarrowing;

		public bool HasSites => Sites.Count > 0;

		public bool HasAutoCompleteSitesOnly
		{
			get
			{
				if (Sites.Count == 0)
				{
					return AutoCompleteSites.Count > 0;
				}
				return false;
			}
		}
	}
}
