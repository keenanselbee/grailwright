using System;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps;

namespace AvalonUntold
{
	public static class StepTransfer
	{
		public static Tri Transfers(StoryStep step)
		{
			if (step == null)
			{
				return Tri.False;
			}
			byte type;
			try
			{
				type = step.Type;
			}
			catch (Exception)
			{
				return Tri.False;
			}
			switch (type)
			{
			case 50:
			case 54:
			case 98:
			case 157:
				return Tri.True;
			case 59:
			case 68:
				if (!IsLastStep(step))
				{
					return Tri.False;
				}
				return Tri.True;
			case 77:
				return Tri.Unknown;
			default:
				return Tri.False;
			}
		}

		public static bool IsRandomPick(StoryStep step)
		{
			if (step == null)
			{
				return false;
			}
			try
			{
				return step.Type == 137;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool IsLastStep(StoryStep step)
		{
			try
			{
				StoryChapter parentChapter = step.parentChapter;
				if (parentChapter == null || parentChapter.steps == null || parentChapter.steps.Length < 2)
				{
					return false;
				}
				StoryStep[] steps = parentChapter.steps;
				if (!(steps[steps.Length - 1] is SLeave))
				{
					return false;
				}
				return steps[steps.Length - 2] == step;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
