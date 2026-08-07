using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Runtime.Nodes;

namespace AvalonUntold
{
	public struct FlowEdge
	{
		public StoryChapter TargetChapter;

		public StoryBookmark Bookmark;

		public string Kind;

		public byte SourceType;

		public bool SelfTargeted;
	}
}
