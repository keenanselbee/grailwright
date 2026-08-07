namespace AvalonUntold
{
	public static class TriOps
	{
		public static Tri And(Tri a, Tri b)
		{
			if ((int)a >= (int)b)
			{
				return b;
			}
			return a;
		}

		public static Tri Or(Tri a, Tri b)
		{
			if ((int)a <= (int)b)
			{
				return b;
			}
			return a;
		}

		public static Tri Not(Tri a)
		{
			return 2 - a;
		}

		public static Tri Join(Tri a, Tri b)
		{
			return Or(a, b);
		}

		public static string Label(Tri a)
		{
			switch (a)
			{
			case Tri.True:
				return "Obtainable";
			case Tri.False:
				return "Locked";
			default:
				return "Unknown";
			}
		}
	}
}
