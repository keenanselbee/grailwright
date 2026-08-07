using System.Collections.Generic;

namespace AvalonUntold
{
	public struct TriResult
	{
		private const int MaxCauses = 12;

		public Tri Value;

		public List<string> Causes;

		public List<string> DecisiveFalses;

		public const string NegatedTrue = "negated-true";

		public static TriResult True()
		{
			return new TriResult
			{
				Value = Tri.True
			};
		}

		public static TriResult False(string cause)
		{
			return new TriResult
			{
				Value = Tri.False,
				DecisiveFalses = new List<string>(1) { cause }
			};
		}

		public static TriResult FalseUnattributed()
		{
			return new TriResult
			{
				Value = Tri.False
			};
		}

		public static TriResult Unknown(string cause)
		{
			return new TriResult
			{
				Value = Tri.Unknown,
				Causes = new List<string>(1) { cause }
			};
		}

		public static TriResult And(TriResult a, TriResult b)
		{
			TriResult result = new TriResult
			{
				Value = TriOps.And(a.Value, b.Value)
			};
			if (result.Value == Tri.False)
			{
				if (a.Value == Tri.False)
				{
					Absorb(ref result.DecisiveFalses, a.DecisiveFalses);
				}
				if (b.Value == Tri.False)
				{
					Absorb(ref result.DecisiveFalses, b.DecisiveFalses);
				}
			}
			else if (result.Value == Tri.Unknown)
			{
				if (a.Value == Tri.Unknown)
				{
					Absorb(ref result.Causes, a.Causes);
				}
				if (b.Value == Tri.Unknown)
				{
					Absorb(ref result.Causes, b.Causes);
				}
			}
			return result;
		}

		public static TriResult Or(TriResult a, TriResult b)
		{
			TriResult result = new TriResult
			{
				Value = TriOps.Or(a.Value, b.Value)
			};
			if (result.Value == Tri.Unknown)
			{
				if (a.Value == Tri.Unknown)
				{
					Absorb(ref result.Causes, a.Causes);
				}
				if (b.Value == Tri.Unknown)
				{
					Absorb(ref result.Causes, b.Causes);
				}
			}
			else if (result.Value == Tri.False)
			{
				Absorb(ref result.DecisiveFalses, a.DecisiveFalses);
				Absorb(ref result.DecisiveFalses, b.DecisiveFalses);
			}
			return result;
		}

		public static TriResult Not(TriResult a)
		{
			TriResult result = new TriResult
			{
				Value = TriOps.Not(a.Value)
			};
			if (a.Value == Tri.Unknown)
			{
				result.Causes = a.Causes;
			}
			else if (a.Value == Tri.True)
			{
				result.DecisiveFalses = new List<string>(1) { "negated-true" };
			}
			return result;
		}

		public static TriResult WithDecisiveFalses(TriResult r, List<string> causes)
		{
			if (causes == null || causes.Count == 0)
			{
				return r;
			}
			r.DecisiveFalses = null;
			Absorb(ref r.DecisiveFalses, causes);
			return r;
		}

		private static void Absorb(ref List<string> into, List<string> from)
		{
			if (from != null)
			{
				for (int i = 0; i < from.Count; i++)
				{
					Add(ref into, from[i]);
				}
			}
		}

		private static void Add(ref List<string> into, string cause)
		{
			if (cause != null)
			{
				if (into == null)
				{
					into = new List<string>(2);
				}
				if (into.Count < 12 && !into.Contains(cause))
				{
					into.Add(cause);
				}
			}
		}
	}
}
