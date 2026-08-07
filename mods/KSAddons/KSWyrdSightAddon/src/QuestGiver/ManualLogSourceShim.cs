using BepInEx.Logging;

namespace AvalonUntold
{
	internal class ManualLogSourceShim
	{
		private readonly ManualLogSource _inner;

		public ManualLogSourceShim(ManualLogSource inner)
		{
			_inner = inner;
		}

		public void Info(string m)
		{
			_inner.LogInfo((object)("[WyrdSight QuestGivers] " + m));
		}

		public void Warn(string m)
		{
			_inner.LogWarning((object)("[WyrdSight QuestGivers] " + m));
		}

		public void Error(string m)
		{
			_inner.LogError((object)("[WyrdSight QuestGivers] " + m));
		}
	}
}
