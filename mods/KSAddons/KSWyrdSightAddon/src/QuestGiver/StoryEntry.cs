using System;
using System.Collections.Generic;

namespace AvalonUntold
{
	public struct StoryEntry : IEquatable<StoryEntry>
	{
		private sealed class EntryComparer : IComparer<StoryEntry>
		{
			public int Compare(StoryEntry a, StoryEntry b)
			{
				return StoryEntry.Compare(a, b);
			}
		}

		public readonly string Graph;

		public readonly string Chapter;

		public static readonly IComparer<StoryEntry> Comparer = new EntryComparer();

		public bool IsStart => Chapter == null;

		public bool IsValid => !string.IsNullOrEmpty(Graph);

		public string Label
		{
			get
			{
				if (Chapter != null)
				{
					return Graph + "#" + Chapter;
				}
				return Graph;
			}
		}

		public StoryEntry(string graph, string chapter)
		{
			Graph = graph ?? "";
			Chapter = Normalise(chapter);
		}

		private static string Normalise(string chapter)
		{
			if (string.IsNullOrEmpty(chapter))
			{
				return null;
			}
			for (int i = 0; i < chapter.Length; i++)
			{
				if (!char.IsWhiteSpace(chapter[i]))
				{
					return chapter;
				}
			}
			return null;
		}

		public bool Equals(StoryEntry other)
		{
			if (string.Equals(Graph, other.Graph, StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(Chapter, other.Chapter, StringComparison.Ordinal);
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is StoryEntry)
			{
				return Equals((StoryEntry)obj);
			}
			return false;
		}

		public override int GetHashCode()
		{
			int num = ((Graph != null) ? StringComparer.OrdinalIgnoreCase.GetHashCode(Graph) : 0);
			int num2 = ((Chapter != null) ? StringComparer.Ordinal.GetHashCode(Chapter) : 0);
			return (num * 397) ^ num2;
		}

		public override string ToString()
		{
			return Label;
		}

		public static int Compare(StoryEntry a, StoryEntry b)
		{
			int num = string.CompareOrdinal(a.Graph ?? "", b.Graph ?? "");
			if (num != 0)
			{
				return num;
			}
			return string.CompareOrdinal(a.Chapter ?? "", b.Chapter ?? "");
		}
	}
}
