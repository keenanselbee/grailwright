using System.Collections.Generic;

namespace AvalonUntold
{
	public sealed class QuestGrantSite
	{
		public string QuestGuid;

		public string GraphGuid;

		public int ChapterId;

		public int StepIndex;

		public bool AutoCompletes;

		public Tri Availability;

		public List<string> Causes;

		public List<string> DecisiveFalses;

		public Tri AvailabilityPreNarrowing;

		public bool LegacyCauseWouldSuppress;

		public bool CauseMixedVolatileAndPermanent;

		public bool NoSelfSetDemotionWouldSuppress;
	}
}
