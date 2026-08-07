using System;
using System.Collections.Generic;
using Awaken.TG.Main.Stories.Runtime.Nodes;
using Awaken.TG.Main.Stories.Steps.Helpers;
using Awaken.Utility.Times;

namespace AvalonUntold
{
	public sealed class StepGate
	{
		private readonly ConditionEvaluator _eval;

		private readonly ScanCounters _counters;

		private readonly Dictionary<StoryStep, TriResult> _cache = new Dictionary<StoryStep, TriResult>(RefComparer<StoryStep>.Instance);

		private readonly Dictionary<StoryStep, TriResult> _transferCache = new Dictionary<StoryStep, TriResult>(RefComparer<StoryStep>.Instance);

		public ConditionEvaluator Evaluator => _eval;

		public StepGate(ConditionEvaluator eval)
			: this(eval, null)
		{
		}

		public StepGate(ConditionEvaluator eval, ScanCounters counters)
		{
			_eval = eval;
			_counters = counters;
		}

		public void BeginGraph()
		{
			_cache.Clear();
			_transferCache.Clear();
		}

		public TriResult Gate(StoryStep step)
		{
			if (step == null)
			{
				return TriResult.Unknown("NullStep");
			}
			if (_cache.TryGetValue(step, out var value))
			{
				return value;
			}
			TriResult triResult = Compute(step, forTransfer: false);
			_cache[step] = triResult;
			return triResult;
		}

		public TriResult TransferGate(StoryStep step)
		{
			if (step == null)
			{
				return TriResult.Unknown("NullStep");
			}
			if (_transferCache.TryGetValue(step, out var value))
			{
				return value;
			}
			TriResult triResult = Compute(step, forTransfer: true);
			_transferCache[step] = triResult;
			return triResult;
		}

		private TriResult Compute(StoryStep step, bool forTransfer)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			TriResult triResult = _eval.EvaluateStepConditions(step);
			IOncePer val = (IOncePer)(object)((step is IOncePer) ? step : null);
			if (val != null)
			{
				string spanFlag;
				TimeSpans span;
				try
				{
					spanFlag = val.SpanFlag;
					span = val.Span;
				}
				catch (Exception ex)
				{
					return TriResult.And(triResult, TriResult.Unknown("IOncePer(threw:" + ex.GetType().Name + ")"));
				}
				TriResult b = _eval.OncePer(spanFlag, span);
				if (forTransfer && b.Value == Tri.True && CanFlip(spanFlag, span))
				{
					b = TriResult.Unknown("oncePerNotYetConsumed");
					if (_counters != null)
					{
						_counters.TransferGatesHedgedOnOncePer++;
					}
				}
				else if (forTransfer && b.Value == Tri.True && _counters != null)
				{
					_counters.TransferGatesKeptOnConstantOncePer++;
				}
				triResult = TriResult.And(triResult, b);
			}
			return triResult;
		}

		internal static bool CanFlip(string flag, TimeSpans span)
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Invalid comparison between Unknown and I4
			if (flag == null)
			{
				return false;
			}
			if ((int)span == 999)
			{
				return false;
			}
			return true;
		}
	}
}
