using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AvalonUntold
{
	internal sealed class RefComparer<T> : IEqualityComparer<T> where T : class
	{
		internal static readonly RefComparer<T> Instance = new RefComparer<T>();

		public bool Equals(T a, T b)
		{
			return a == b;
		}

		public int GetHashCode(T o)
		{
			return RuntimeHelpers.GetHashCode(o);
		}
	}
}
