using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	internal sealed class OutlinePass : CustomPass
	{
		internal static Color Colour = new Color(1f, 0.776f, 0.294f, 1f);

		internal static float Intensity = 4f;

		internal static float WidthPixels = 4f;

		internal static float MaxDistance = 25f;

		internal static readonly bool CullFront = true;

		internal static readonly bool IncludeHair = false;

		internal static int ScreenHeight;

		internal static string HullShaderName = "(not resolved)";

		internal static bool ShadersOk;

		internal static string PropertyReport = "(not probed)";

		internal static int InstallFailures;

		private static bool _installFailureLogged;

		internal static string RequiredPropertyReport = "(not probed)";

		internal static string StateReadback = "(not probed)";

		internal static string InstallReport = "(not installed)";

		internal static int LastExecutedFrame = -1;

		internal static int LastDrawnMeshes;

		internal static int LastTargets;

		internal static string LastCameraName = "(none)";

		internal static int ExecuteFailures;

		internal static int ExecutedEverFrame = -1;

		internal static int ExecuteCount;

		internal static long DrawnEver;

		private static OutlinePass _instance;

		private Material _hullMat;

		private bool _disposed;

		private static readonly int ColorId = Shader.PropertyToID("_Color");

		private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");

		private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

		private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

		private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

		private static readonly int CullId = Shader.PropertyToID("_Cull");

		internal static bool Installed => _instance != null && ((CustomPass)_instance).enabled;

		internal static bool HasResources => _instance != null;

		private static void FailLog(string message)
		{
			if (!_installFailureLogged)
			{
				_installFailureLogged = true;
				if (Plugin.Log != null)
				{
					Plugin.Log.Error(message + " || Quest-giver outlines remain disabled; check the BepInEx log for details.");
				}
			}
		}

		internal static string DiagnosticsBlock()
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.Append("- shaders resolved: ").Append(ShadersOk).Append(" (")
				.Append(HullShaderName)
				.Append(')')
				.Append('\n');
			stringBuilder.Append("- shader property table: ").Append(PropertyReport).Append('\n');
			stringBuilder.Append("- required-property check: ").Append(RequiredPropertyReport).Append('\n');
			stringBuilder.Append("- render state readback: ").Append(StateReadback).Append('\n');
			stringBuilder.Append("- registration: ").Append(InstallReport).Append('\n');
			stringBuilder.Append("- hull mesh (first bake): ").Append(QuestGlow.HullMeshReport).Append('\n');
			stringBuilder.Append("- live: ").Append(StatusLine()).Append('\n');
			return stringBuilder.ToString();
		}

		internal static bool Install()
		{
			if (_instance != null)
			{
				((CustomPass)_instance).enabled = true;
				return true;
			}
			try
			{
				OutlinePass outlinePass = new OutlinePass();
				((CustomPass)outlinePass).name = "AvalonUntold Outline";
				if (!outlinePass.ResolveShaders())
				{
					outlinePass.DisposeResources();
					InstallFailures++;
					return false;
				}
				CustomPassVolume.RegisterGlobalCustomPass((CustomPassInjectionPoint)6, (CustomPass)(object)outlinePass, 0f);
				_instance = outlinePass;
				_installFailureLogged = false;
				InstallReport = "registered at AfterOpaqueAndSky (inverted hull, shader=" + HullShaderName + "). Expected: a gold band roughly " + WidthPixels + " px wide just OUTSIDE each lit NPC, with the NPC rendered normally inside it. If instead the NPC is FILLED SOLID GOLD, _Cull is inverted on this GPU. If NOTHING appears, check the hull mesh line for color=True stride0=40.";
				return true;
			}
			catch (Exception ex)
			{
				InstallFailures++;
				FailLog("outline pass could not be registered: " + ex);
				return false;
			}
		}

		internal static void Suspend()
		{
			if (_instance == null)
			{
				return;
			}

			((CustomPass)_instance).enabled = false;
			LastExecutedFrame = -1;
			LastDrawnMeshes = 0;
			LastTargets = 0;
			LastCameraName = "(none)";
		}

		internal static void Uninstall()
		{
			OutlinePass instance = _instance;
			_instance = null;
			if (instance == null)
			{
				return;
			}
			try
			{
				CustomPassVolume.UnregisterGlobalCustomPass((CustomPass)(object)instance);
			}
			catch (Exception ex)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.Error("outline unregister: " + ex);
				}
			}
			instance.DisposeResources();
			LastExecutedFrame = -1;
			LastDrawnMeshes = 0;
			LastTargets = 0;
			LastCameraName = "(none)";
		}

		private bool ResolveShaders()
		{
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Expected O, but got Unknown
			Shader val = Shader.Find("Hidden/Internal-Colored");
			HullShaderName = (((Object)(object)val != (Object)null) ? ((Object)val).name : "(not resolved)");
			if ((Object)(object)val == (Object)null)
			{
				FailLog("outline unavailable: Shader.Find(\"Hidden/Internal-Colored\") returned null. That shader lives in Unity's built-in resources and should always be present; without a flat-colour writer whose render state we can set, there is no way to draw an inverted hull.");
				ShadersOk = false;
				PropertyReport = "shader not resolved";
				RequiredPropertyReport = "not reached - no shader";
				StateReadback = "not reached - no shader";
				return false;
			}
			_hullMat = new Material(val);
			((Object)_hullMat).hideFlags = (HideFlags)61;
			((Object)_hullMat).name = "AvalonUntold_OutlineHull";
			PropertyReport = DescribeProperties(val);
			bool flag = _hullMat.HasProperty(ColorId);
			bool flag2 = _hullMat.HasProperty(CullId);
			bool flag3 = _hullMat.HasProperty(ZWriteId);
			bool flag4 = _hullMat.HasProperty(ZTestId);
			bool flag5 = _hullMat.HasProperty(SrcBlendId);
			bool flag6 = _hullMat.HasProperty(DstBlendId);
			RequiredPropertyReport = "shader=" + HullShaderName + " _Color=" + flag + " _Cull=" + flag2 + " _ZWrite=" + flag3 + " _ZTest=" + flag4 + " | optional _SrcBlend=" + flag5 + " _DstBlend=" + flag6;
			if (!flag || !flag2 || !flag3 || !flag4)
			{
				FailLog("outline unavailable: Hidden/Internal-Colored is missing a REQUIRED property (_Color=" + flag + " _Cull=" + flag2 + " _ZWrite=" + flag3 + " _ZTest=" + flag4 + "). Without _Cull the hull would fill every lit NPC with solid gold, and without _ZWrite=0 it would corrupt the depth buffer that fog and depth of field read after us. This refuses to draw rather than draw wrong. Required-property check: " + RequiredPropertyReport + ". Full property table: " + PropertyReport);
				ShadersOk = false;
				StateReadback = "not reached - a required property is missing";
				return false;
			}
			ApplyHullState(_hullMat);
			float num = _hullMat.GetFloat(CullId);
			float num2 = _hullMat.GetFloat(ZWriteId);
			float num3 = _hullMat.GetFloat(ZTestId);
			StateReadback = "_Cull=" + num + " (1=Front 2=Back) _ZWrite=" + num2 + " _ZTest=" + num3 + " (4=LEqual)" + (flag5 ? (" _SrcBlend=" + _hullMat.GetFloat(SrcBlendId)) : " _SrcBlend=absent") + (flag6 ? (" _DstBlend=" + _hullMat.GetFloat(DstBlendId)) : " _DstBlend=absent");
			if ((num != (CullFront ? 1f : 2f) || num2 != 0f || num3 != 4f) && Plugin.Log != null)
			{
				Plugin.Log.Error("the outline's render state did not stick on this shader (" + StateReadback + "). The band may draw as a solid gold fill; check the BepInEx log for details.");
			}
			ShadersOk = true;
			return true;
		}

		private static void ApplyHullState(Material m)
		{
			m.SetFloat(CullId, CullFront ? 1f : 2f);
			m.SetFloat(ZWriteId, 0f);
			m.SetFloat(ZTestId, 4f);
			bool flag = m.HasProperty(SrcBlendId);
			bool flag2 = m.HasProperty(DstBlendId);
			if (flag)
			{
				m.SetFloat(SrcBlendId, 1f);
			}
			if (flag2)
			{
				m.SetFloat(DstBlendId, 0f);
			}
			if ((!flag || !flag2) && Plugin.Log != null)
			{
				Plugin.Log.Warn("hull blend state is not settable on this shader (_SrcBlend=" + flag + " _DstBlend=" + flag2 + "); falling back to the shader's own blend, which for Hidden/Internal-Colored is SrcAlpha/OneMinusSrcAlpha with our alpha forced to 1 - visually the same as replace, so this is survivable.");
			}
		}

		private static string DescribeProperties(Shader s)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				int propertyCount = s.GetPropertyCount();
				StringBuilder stringBuilder = new StringBuilder(160);
				stringBuilder.Append(propertyCount).Append(" declared: ");
				for (int i = 0; i < propertyCount; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append(", ");
					}
					stringBuilder.Append(s.GetPropertyName(i)).Append('(').Append(s.GetPropertyType(i))
						.Append(')');
				}
				return stringBuilder.ToString();
			}
			catch (Exception ex)
			{
				return "property enumeration threw: " + ex.GetType().Name;
			}
		}

		protected override void Cleanup()
		{
			DisposeResources();
		}

		private void DisposeResources()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			if ((Object)(object)_hullMat == (Object)null)
			{
				return;
			}
			try
			{
				Object.Destroy((Object)(object)_hullMat);
			}
			catch (Exception ex)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.Warn("outline material destroy: " + ex);
				}
			}
			_hullMat = null;
		}

		protected override void Execute(CustomPassContext ctx)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			if (_disposed)
			{
				return;
			}
			try
			{
				ExecuteBody(ctx);
			}
			catch (Exception ex)
			{
				ExecuteFailures++;
				if (ExecuteFailures == 1 && Plugin.Log != null)
				{
					Plugin.Log.Error("outline pass threw (further occurrences are counted, not logged): " + ex);
				}
			}
		}

		private void ExecuteBody(CustomPassContext ctx)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Invalid comparison between Unknown and I4
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			Camera val = ((ctx.hdCamera != null) ? ctx.hdCamera.camera : null);
			if ((Object)(object)val == (Object)null || (int)val.cameraType != 1 || (Object)(object)val.targetTexture != (Object)null)
			{
				return;
			}
			int num = 0;
			try
			{
				num = ctx.hdCamera.actualHeight;
			}
			catch (Exception)
			{
			}
			if (num <= 0)
			{
				num = val.pixelHeight;
			}
			if (num > 0)
			{
				ScreenHeight = num;
			}
			LastCameraName = ((Object)val).name;
			LastExecutedFrame = Time.frameCount;
			ExecutedEverFrame = Time.frameCount;
			ExecuteCount++;
			List<QuestGlow> live = QuestGlow.Live;
			int num2 = 0;
			for (int i = 0; i < live.Count; i++)
			{
				if (live[i] != null && live[i].OutlineDrawable)
				{
					num2++;
				}
			}
			LastTargets = num2;
			if (num2 == 0)
			{
				LastDrawnMeshes = 0;
			}
			else
			{
				if ((Object)(object)_hullMat == (Object)null)
				{
					return;
				}
				CommandBuffer cmd = ctx.cmd;
				CoreUtils.SetRenderTarget(cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer, (ClearFlag)0, 0, (CubemapFace)(-1), -1);
				Color val2 = Colour * Mathf.Max(0f, Intensity);
				val2.a = 1f;
				_hullMat.SetColor(ColorId, val2);
				int meshes = 0;
				for (int j = 0; j < live.Count; j++)
				{
					QuestGlow questGlow = live[j];
					if (questGlow != null && questGlow.OutlineDrawable)
					{
						questGlow.EmitHull(cmd, _hullMat, ref meshes);
					}
				}
				LastDrawnMeshes = meshes;
				DrawnEver += meshes;
			}
		}

		internal static string StatusLine()
		{
			return "outline: installed=" + Installed + " shader=" + (ShadersOk ? HullShaderName : "UNRESOLVED") + " cull=" + (CullFront ? "Front" : "Back") + " hair=" + (IncludeHair ? "in" : "OUT") + " lastFrame=" + LastExecutedFrame + " everFrame=" + ExecutedEverFrame + " (now " + Time.frameCount + ") executes=" + ExecuteCount + " cam=" + LastCameraName + " targets=" + LastTargets + " drew=" + LastDrawnMeshes + " meshes (everDrew=" + DrawnEver + ") width=" + WidthPixels + "px intensity=" + Intensity + " screenH=" + ScreenHeight + " failures=" + ExecuteFailures + " bakeFailures=" + QuestGlow.BakeFailuresTotal;
		}
	}
}
