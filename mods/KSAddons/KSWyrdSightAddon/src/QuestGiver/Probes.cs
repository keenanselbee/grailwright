using System;
using System.Linq;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Stories;
using Awaken.TG.Main.Stories.Quests.Templates;
using Awaken.TG.Main.Stories.Runtime;
using Awaken.TG.Main.Templates;

namespace AvalonUntold
{
	public static class Probes
	{
		public static void Run(ScanReport report, TemplatesProvider tp)
		{
			Environment(report, tp);
			ProbeQuestTemplateBaseBucket(report, tp);
			ProbeAutoContext(report);
		}

		private static void Add(ScanReport report, string claim, string observed, string consequence)
		{
			ProbeResult probeResult = new ProbeResult();
			probeResult.Claim = claim;
			probeResult.Observed = observed;
			probeResult.Consequence = consequence;
			report.Probes.Add(probeResult);
		}

		private static void Environment(ScanReport report, TemplatesProvider tp)
		{
			string text;
			try
			{
				text = tp.AllLoaded.ToString();
			}
			catch (Exception ex)
			{
				text = "threw " + ex.GetType().Name;
			}
			string text2;
			try
			{
				text2 = (Hero.Current != null).ToString();
			}
			catch (Exception ex2)
			{
				text2 = "threw " + ex2.GetType().Name;
			}
			string text3;
			try
			{
				text3 = SceneLifetimeEvents.Get.EverythingInitialized.ToString();
			}
			catch (Exception ex3)
			{
				text3 = "threw " + ex3.GetType().Name;
			}
			Add(report, "the scan runs in a fully initialised gameplay session", "TemplatesProvider.AllLoaded=" + text + ", Hero.Current!=null=" + text2 + ", SceneLifetimeEvents.EverythingInitialized=" + text3, "conditions that dereference Hero.Current or World.Only<T> are safe to delegate only when these hold");
		}

		private static void ProbeQuestTemplateBaseBucket(ScanReport report, TemplatesProvider tp)
		{
			string observed;
			try
			{
				int num = tp.GetAllOfType<QuestTemplateBase>((TemplateTypeFlag)255).Count();
				int num2 = tp.AllTemplates.OfType<QuestTemplateBase>().Count();
				observed = "GetAllOfType<QuestTemplateBase>()=" + num + ", AllTemplates.OfType<>()=" + num2;
			}
			catch (Exception ex)
			{
				observed = "threw " + ex.GetType().Name + ": " + ex.Message;
			}
			Add(report, "GetAllOfType<QuestTemplateBase>() is empty because the template type map is keyed by concrete type", observed, "we use AllTemplates.OfType<>() either way; a non-zero first number only means the deduction was wrong, not the code");
		}

		private static void ProbeAutoContext(ScanReport report)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			string observed;
			try
			{
				StoryUtilsRuntime.AutoContext((Story)null);
				observed = "returned without throwing";
			}
			catch (Exception ex)
			{
				observed = "threw " + ex.GetType().Name;
			}
			Add(report, "StoryUtilsRuntime.AutoContext(null) throws NullReferenceException, so COncePer can never be delegated", observed, "we reimplement COncePer unconditionally; if this did NOT throw, delegating becomes possible for span != Dialogue - record it, do not change code on one probe");
		}
	}
}
