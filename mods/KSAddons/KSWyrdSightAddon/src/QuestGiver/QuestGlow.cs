using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Awaken.Kandra;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Scenes.SceneConstructors;
using Awaken.Utility.Maths;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AvalonUntold
{
	public sealed class QuestGlow : Element<Location>
	{
		private struct Slot
		{
			public KandraRenderer Renderer;

			public int Index;

			public Material Mat;

			public bool HadColor;

			public Color PrevColor;

			public bool HadFloat;

			public float PrevFloat;

			public bool TouchedEmissiveMap;

			public Texture PrevEmissiveMap;

			public bool TouchedEmissionMap;

			public Texture PrevEmissionMap;
		}

		[StructLayout(LayoutKind.Auto)]
		[CompilerGenerated]
		private struct _003CSpawnVfx_003Ed__76 : IAsyncStateMachine
		{
			public int _003C_003E1__state;

			public AsyncUniTaskVoidMethodBuilder _003C_003Et__builder;

			public QuestGlow _003C_003E4__this;

			public Transform parent;

			private UniTask<IPooledInstance>.Awaiter _003C_003Eu__1;

			private void MoveNext()
			{
				//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
				//IL_0050: Unknown result type (might be due to invalid IL or missing references)
				//IL_0055: Unknown result type (might be due to invalid IL or missing references)
				//IL_0075: Unknown result type (might be due to invalid IL or missing references)
				//IL_007a: Unknown result type (might be due to invalid IL or missing references)
				//IL_007e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0083: Unknown result type (might be due to invalid IL or missing references)
				//IL_0098: Unknown result type (might be due to invalid IL or missing references)
				//IL_009a: Unknown result type (might be due to invalid IL or missing references)
				int num = _003C_003E1__state;
				QuestGlow questGlow = _003C_003E4__this;
				try
				{
					IPooledInstance val;
					if (num != 0)
					{
						val = null;
					}
					try
					{
						UniTask<IPooledInstance>.Awaiter val2;
						if (num == 0)
						{
							val2 = _003C_003Eu__1;
							_003C_003Eu__1 = default(UniTask<IPooledInstance>.Awaiter);
							num = (_003C_003E1__state = -1);
							goto IL_00cf;
						}
						CommonReferences get = CommonReferences.Get;
						if (!((Object)(object)get == (Object)null) && get.deadBodyHighlightVfx != null && get.deadBodyHighlightVfx.IsSet)
						{
							val2 = PrefabPool.Instantiate(get.deadBodyHighlightVfx, Vector3.zero, Quaternion.identity, parent, (Vector3?)null, default(CancellationToken), true).GetAwaiter();
							if (!val2.IsCompleted)
							{
								num = (_003C_003E1__state = 0);
								_003C_003Eu__1 = val2;
								_003C_003Et__builder.AwaitUnsafeOnCompleted<UniTask<IPooledInstance>.Awaiter, _003CSpawnVfx_003Ed__76>(ref val2, ref this);
								return;
							}
							goto IL_00cf;
						}
						questGlow._vfxRequested = false;
						goto end_IL_000e;
						IL_00cf:
						val = val2.GetResult();
					}
					catch (Exception ex)
					{
						questGlow._vfxRequested = false;
						Log("vfx spawn: " + ex);
						goto end_IL_000e;
					}
					if (val == null)
					{
						questGlow._vfxRequested = false;
					}
					else if (((Model)questGlow).HasBeenDiscarded || (Object)(object)parent == (Object)null)
					{
						try
						{
							val.Return();
						}
						catch (Exception)
						{
						}
					}
					else
					{
						questGlow._vfx = val;
					}
					end_IL_000e:;
				}
				catch (Exception exception)
				{
					_003C_003E1__state = -2;
					_003C_003Et__builder.SetException(exception);
					return;
				}
				_003C_003E1__state = -2;
				_003C_003Et__builder.SetResult();
			}

			void IAsyncStateMachine.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				this.MoveNext();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				_003C_003Et__builder.SetStateMachine(stateMachine);
			}

			void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
			{
				//ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
				this.SetStateMachine(stateMachine);
			}
		}

		private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

		private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

		private static readonly int EmissiveColorMapId = Shader.PropertyToID("_EmissiveColorMap");

		private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

		internal static readonly List<QuestGlow> Live = new List<QuestGlow>();

		internal static float OutlineRefreshIntervalSeconds = 1f / 30f;

		internal static int EverLit;

		private readonly List<Slot> _slots = new List<Slot>(4);

		private readonly List<KandraRenderer> _instanced = new List<KandraRenderer>(4);

		private readonly List<Material[]> _instancedMats = new List<Material[]>(4);

		private IPooledInstance _vfx;

		private bool _vfxRequested;

		private readonly Color _colour;

		private readonly float _intensity;

		private readonly float _emissionIntensityValue;

		private readonly GlowRoute _route;

		private readonly EmissiveMapMode _mapMode;

		private readonly List<KandraRenderer> _outlineKandras = new List<KandraRenderer>(4);

		private readonly List<Mesh> _outlineMeshes = new List<Mesh>(4);

		private readonly List<int> _outlinePoseGenerations = new List<int>(4);

		private int _requiredPoseGeneration = 1;

		private int _bakeCursor;

		private int _lastBakeFrame = -1;

		private float _nextOutlineBakeAt;

		private bool _outlineVisible;

		private bool _bakedAny;

		private int _outlineBakeFailures;

		private Transform _outlineRoot;

		private float _hullOffset = 0.012f;

		internal static int BakeFailuresTotal;

		private const int TransparentQueueThreshold = 2750;

		private const string HairShaderName = "TG/Character/RealHair";

		private const int MaxOutlineKandras = 8;

		private const int FloatsPerVertex = 10;

		private static bool _strideReported;

		private static bool _hullMeshReported;

		internal static string HullMeshReport = "(no hull mesh baked yet)";

		private static uint[] _whiteScratch;

		private const int MaxSubmeshes = 6;

		public sealed override bool IsNotSaved => true;

		public static int LiveCount => Live.Count;

		internal Transform OutlineRoot => _outlineRoot;

		internal bool OutlineDrawable
		{
			get
			{
				if (!((Model)this).HasBeenDiscarded && _route == GlowRoute.Outline && _outlineVisible)
				{
					return _bakedAny;
				}
				return false;
			}
		}

		internal bool OutlineDrawableCandidate
		{
			get
			{
				if (!((Model)this).HasBeenDiscarded && _route == GlowRoute.Outline && _outlineVisible)
				{
					return _outlineKandras.Count > 0;
				}
				return false;
			}
		}

		public static void RemoveAll()
		{
			QuestGlow[] array = Live.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				try
				{
					if (array[i] != null && !((Model)array[i]).HasBeenDiscarded)
					{
						((Model)array[i]).Discard();
					}
				}
				catch (Exception ex)
				{
					if (Plugin.Log != null)
					{
						Plugin.Log.Error("QuestGlow.RemoveAll: " + ex);
					}
				}
			}
			for (int num = Live.Count - 1; num >= 0; num--)
			{
				QuestGlow questGlow = Live[num];
				bool flag;
				try
				{
					flag = questGlow == null || ((Model)questGlow).HasBeenDiscarded;
				}
				catch (Exception)
				{
					flag = false;
				}
				if (flag)
				{
					Live.RemoveAt(num);
				}
			}
		}

		internal static void RequireFreshPosesForAll()
		{
			for (int i = 0; i < Live.Count; i++)
			{
				QuestGlow glow = Live[i];
				if (glow != null && !((Model)glow).HasBeenDiscarded)
				{
					glow.RequireFreshPose();
				}
			}
		}

		public QuestGlow(Color colour, float intensity, float emissionIntensityValue, GlowRoute route, EmissiveMapMode mapMode)
		{
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			_colour = colour;
			_intensity = intensity;
			_emissionIntensityValue = emissionIntensityValue;
			_route = route;
			_mapMode = mapMode;
		}

		protected override void OnInitialize()
		{
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Expected O, but got Unknown
			Live.Add(this);
			EverLit++;
			NpcElement val = null;
			try
			{
				val = ((Model)base.ParentModel).TryGetElement<NpcElement>();
			}
			catch (Exception)
			{
			}
			if (val == null)
			{
				return;
			}
			try
			{
				ModelExtensions.ListenTo<Location, bool>(base.ParentModel, (IEvent<Location, bool>)(object)NpcElement.Events.AfterNpcVisibilityChanged, (Action<bool>)OnVisibilityChanged, (IListenerOwner)(object)this);
			}
			catch (Exception ex2)
			{
				Log("visibility listener: " + ex2);
			}
			try
			{
				if (val.HasVisualLoaded)
				{
					Apply(val);
				}
				else
				{
					val.OnVisualLoaded(new NpcInitializer.NpcVisualLoadedDelegate(OnNpcVisualLoaded));
				}
			}
			catch (Exception ex3)
			{
				Log("initial apply: " + ex3);
			}
		}

		private void OnNpcVisualLoaded(NpcElement npc, Transform parent)
		{
			if (((Model)this).HasBeenDiscarded || npc == null)
			{
				return;
			}
			try
			{
				Apply(npc);
			}
			catch (Exception ex)
			{
				Log("apply on visual loaded: " + ex);
			}
		}

		private void OnVisibilityChanged(bool visible)
		{
			if (!visible || ((Model)this).HasBeenDiscarded)
			{
				return;
			}
			try
			{
				NpcElement val = ((base.ParentModel != null) ? ((Model)base.ParentModel).TryGetElement<NpcElement>() : null);
				if (val != null && val.HasVisualLoaded)
				{
					Apply(val);
				}
			}
			catch (Exception ex)
			{
				Log("re-apply after visibility: " + ex);
			}
		}

		protected override void OnDiscard(bool fromDomainDrop)
		{
			Live.Remove(this);
			RevertMaterials();
			ReturnVfx();
			ReleaseOutline();
		}

		public void Refresh()
		{
			if (((Model)this).HasBeenDiscarded)
			{
				return;
			}
			try
			{
				NpcElement val = ((base.ParentModel != null) ? ((Model)base.ParentModel).TryGetElement<NpcElement>() : null);
				if (val != null && val.HasVisualLoaded)
				{
					Apply(val);
				}
			}
			catch (Exception ex)
			{
				Log("refresh: " + ex);
			}
		}

		private void Apply(NpcElement npc)
		{
			if (_route == GlowRoute.Outline)
			{
				RefreshOutlineSources(npc);
				return;
			}
			if (_route == GlowRoute.EmissiveLegacy || _route == GlowRoute.EmissiveAndVfxLegacy)
			{
				ApplyKandra(npc);
			}
			if (_route == GlowRoute.VfxLegacy || _route == GlowRoute.EmissiveAndVfxLegacy)
			{
				ApplyVfx(npc);
			}
		}

		internal void SetOutlineVisible(bool visible)
		{
			_outlineVisible = visible;
		}

		private void RequireFreshPose()
		{
			if (_requiredPoseGeneration == int.MaxValue)
			{
				_requiredPoseGeneration = 1;
				for (int i = 0; i < _outlinePoseGenerations.Count; i++)
				{
					_outlinePoseGenerations[i] = 0;
				}
			}
			else
			{
				_requiredPoseGeneration++;
			}

			_bakedAny = false;
			_bakeCursor = 0;
			_lastBakeFrame = -1;
			_nextOutlineBakeAt = 0f;
			_outlineVisible = false;
		}

		internal void SetHullOffset(float metres)
		{
			_hullOffset = metres;
		}

		private void RefreshOutlineSources(NpcElement npc)
		{
			Transform val = null;
			try
			{
				val = npc.ParentTransform;
			}
			catch (Exception)
			{
			}
			_outlineRoot = val;
			if ((Object)(object)val == (Object)null)
			{
				return;
			}
			KandraRenderer[] array = null;
			try
			{
				array = ((Component)val).GetComponentsInChildren<KandraRenderer>(false);
			}
			catch (Exception ex2)
			{
				Log("outline source sweep: " + ex2);
				return;
			}
			for (int num = _outlineKandras.Count - 1; num >= 0; num--)
			{
				KandraRenderer val2 = _outlineKandras[num];
				bool flag;
				try
				{
					flag = (Object)(object)val2 == (Object)null || val2.Destroyed;
				}
				catch (Exception)
				{
					flag = true;
				}
				if (flag || !Contains(array, val2) || !Wanted(val2))
				{
					DestroyMeshAt(num);
					_outlineKandras.RemoveAt(num);
					_outlineMeshes.RemoveAt(num);
					_outlinePoseGenerations.RemoveAt(num);
				}
			}
			if (array != null)
			{
				foreach (KandraRenderer val3 in array)
				{
					if ((Object)(object)val3 == (Object)null)
					{
						continue;
					}
					try
					{
						if (val3.Destroyed)
						{
							continue;
						}
					}
					catch (Exception)
					{
						continue;
					}
					if (Wanted(val3) && !_outlineKandras.Contains(val3))
					{
						if (_outlineKandras.Count >= 8)
						{
							break;
						}
						_outlineKandras.Add(val3);
						_outlineMeshes.Add(null);
						_outlinePoseGenerations.Add(0);
					}
				}
			}
			_bakedAny = false;
			for (int j = 0; j < _outlineMeshes.Count; j++)
			{
				if ((Object)(object)_outlineMeshes[j] != (Object)null
					&& _outlinePoseGenerations[j] == _requiredPoseGeneration)
				{
					_bakedAny = true;
					break;
				}
			}
			if (_outlineKandras.Count == 0)
			{
				Log("outline: NPC visual has no usable KandraRenderer - nothing to outline" + (OutlinePass.IncludeHair ? "" : " (transparent parts, hair included, are excluded from the outline by design)"));
			}
		}

		private static bool Wanted(KandraRenderer r)
		{
			if (OutlinePass.IncludeHair)
			{
				return true;
			}
			return !IsHair(r);
		}

		internal static bool IsHair(KandraRenderer r)
		{
			try
			{
				Material[] materials = r.rendererData.materials;
				if (materials == null)
				{
					return false;
				}
				foreach (Material val in materials)
				{
					if ((Object)(object)val == (Object)null)
					{
						continue;
					}
					int num = -1;
					try
					{
						num = val.renderQueue;
					}
					catch (Exception)
					{
					}
					if (num < 0 && (Object)(object)val.shader != (Object)null)
					{
						try
						{
							num = val.shader.renderQueue;
						}
						catch (Exception)
						{
						}
					}
					if (num >= 2750)
					{
						return true;
					}
					if (num < 0 && (Object)(object)val.shader != (Object)null && ((Object)val.shader).name == "TG/Character/RealHair")
					{
						return true;
					}
				}
			}
			catch (Exception)
			{
			}
			return false;
		}

		private static bool Contains(KandraRenderer[] set, KandraRenderer r)
		{
			if (set == null)
			{
				return false;
			}
			for (int i = 0; i < set.Length; i++)
			{
				if (set[i] == r)
				{
					return true;
				}
			}
			return false;
		}

		internal bool BakeOutlineSlice()
		{
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0128: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0281: Unknown result type (might be due to invalid IL or missing references)
			if (((Model)this).HasBeenDiscarded || _route != GlowRoute.Outline)
			{
				return false;
			}
			if (_outlineKandras.Count == 0)
			{
				return false;
			}
			if (_bakeCursor == 0 && Time.unscaledTime < _nextOutlineBakeAt)
			{
				return false;
			}
			if (_lastBakeFrame == Time.frameCount && _bakeCursor == 0)
			{
				return false;
			}
			if (_bakeCursor >= _outlineKandras.Count)
			{
				_bakeCursor = 0;
			}
			int bakeCursor = _bakeCursor;
			_bakeCursor++;
			if (_bakeCursor >= _outlineKandras.Count)
			{
				_bakeCursor = 0;
				_lastBakeFrame = Time.frameCount;
				_nextOutlineBakeAt = Time.unscaledTime + OutlineRefreshIntervalSeconds;
			}
			KandraRenderer val = _outlineKandras[bakeCursor];
			bool flag;
			try
			{
				flag = (Object)(object)val == (Object)null || val.Destroyed;
			}
			catch (Exception)
			{
				flag = true;
			}
			if (flag)
			{
				return false;
			}
			try
			{
				Mesh val2 = _outlineMeshes[bakeCursor];
				if ((Object)(object)val2 == (Object)null)
				{
					val2 = CreateHullMesh(val);
					if ((Object)(object)val2 == (Object)null)
					{
						return false;
					}
					_outlineMeshes[bakeCursor] = val2;
				}
				float4x4 identity = float4x4.identity;
				float3x4 val3 = mathExt.orthonormal(identity);
				float hullOffset = _hullOffset;
				Mesh.MeshDataArray val4 = Mesh.AllocateWritableMeshData(val2);
				try
				{
					Mesh.MeshData val5 = val4[0];
					KandraRendererPoseBaking.UpdatePoseMesh(val, val5, val3);
					NativeArray<float> vertexData = val5.GetVertexData<float>(0);
					int num = vertexData.Length / 10;
					if (num * 10 != vertexData.Length)
					{
						if (!_strideReported)
						{
							_strideReported = true;
							if (Plugin.Log != null)
							{
								Plugin.Log.Error("hull inflate skipped: stream 0 is " + vertexData.Length + " floats for " + val5.vertexCount + " vertices, which is not " + 10 + " per vertex. The outline will hug the body exactly instead of standing off it, i.e. it will be invisible or a z-fighting speckle.");
							}
						}
					}
					else
					{
						for (int i = 0; i < num; i++)
						{
							int num2 = i * 10;
							float num3 = vertexData[num2 + 3];
							float num4 = vertexData[num2 + 4];
							float num5 = vertexData[num2 + 5];
							if (num3 * num3 + num4 * num4 + num5 * num5 > 0.25f)
							{
								vertexData[num2] += num3 * hullOffset;
								vertexData[num2 + 1] = vertexData[num2 + 1] + num4 * hullOffset;
								vertexData[num2 + 2] = vertexData[num2 + 2] + num5 * hullOffset;
							}
						}
					}
					Mesh.ApplyAndDisposeWritableMeshData(val4, val2, (MeshUpdateFlags)15);
				}
				catch (Exception)
				{
					try
					{
						val4.Dispose();
					}
					catch (Exception)
					{
					}
					throw;
				}
				_bakedAny = true;
				_outlinePoseGenerations[bakeCursor] = _requiredPoseGeneration;
				return true;
			}
			catch (Exception ex4)
			{
				_outlineBakeFailures++;
				BakeFailuresTotal++;
				if (_outlineBakeFailures == 1)
				{
					Log("outline bake failed (counted thereafter): " + ex4);
				}
				return false;
			}
		}

		private static Mesh CreateHullMesh(KandraRenderer r)
		{
			//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Unknown result type (might be due to invalid IL or missing references)
			//IL_0307: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			Mesh val = KandraRendererPoseBaking.BlankMesh(r);
			if ((Object)(object)val == (Object)null)
			{
				return null;
			}
			((Object)val).name = "AvalonUntold_OutlineHull";
			((Object)val).hideFlags = (HideFlags)61;
			val.MarkDynamic();
			int vertexCount = val.vertexCount;
			int subMeshCount = val.subMeshCount;
			long num = 0L;
			for (int i = 0; i < subMeshCount; i++)
			{
				num += val.GetIndexCount(i);
			}
			bool flag = false;
			try
			{
				val.SetVertexBufferParams(vertexCount, (VertexAttributeDescriptor[])(object)new VertexAttributeDescriptor[5]
				{
					new VertexAttributeDescriptor((VertexAttribute)0, (VertexAttributeFormat)0, 3, 0),
					new VertexAttributeDescriptor((VertexAttribute)1, (VertexAttributeFormat)0, 3, 0),
					new VertexAttributeDescriptor((VertexAttribute)2, (VertexAttributeFormat)0, 4, 0),
					new VertexAttributeDescriptor((VertexAttribute)4, (VertexAttributeFormat)0, 2, 1),
					new VertexAttributeDescriptor((VertexAttribute)3, (VertexAttributeFormat)2, 4, 2)
				});
				uint[] array = WhiteScratch(vertexCount);
				val.SetVertexBufferData<uint>(array, 0, 0, vertexCount, 2, (MeshUpdateFlags)15);
				long num2 = 0L;
				for (int j = 0; j < val.subMeshCount; j++)
				{
					num2 += val.GetIndexCount(j);
				}
				flag = val.vertexCount == vertexCount && val.subMeshCount == subMeshCount && num2 == num && val.GetVertexBufferStride(0) == 40 && val.HasVertexAttribute((VertexAttribute)3);
				if (!_hullMeshReported)
				{
					_hullMeshReported = true;
					HullMeshReport = "verts=" + vertexCount + " stride0=" + val.GetVertexBufferStride(0) + " subs=" + val.subMeshCount + "/" + subMeshCount + " idx=" + num2 + "/" + num + " color=" + val.HasVertexAttribute((VertexAttribute)3) + " colorStream=" + val.GetVertexAttributeStream((VertexAttribute)3) + " ok=" + flag;
					if (!flag && Plugin.Log != null)
					{
						Plugin.Log.Error("the outline's white vertex-colour stream could not be written (" + HullMeshReport + "), so the band may draw black or not at all; check the BepInEx log for details.");
					}
				}
			}
			catch (Exception ex)
			{
				flag = false;
				if (!_hullMeshReported)
				{
					_hullMeshReported = true;
					HullMeshReport = "THREW: " + ex.GetType().Name + ": " + ex.Message;
					if (Plugin.Log != null)
					{
						Plugin.Log.Error("hull mesh vertex-colour insurance threw: " + ex);
					}
				}
			}
			if (!flag)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.Error("hull mesh: could not add the white vertex-colour stream, falling back to the plain Kandra layout. Hidden/Internal-Colored multiplies by vertex colour, so IF the GPU feeds black for a missing COLOR attribute the outline will be INVISIBLE. If you see no gold at all, this line is why.");
				}
				try
				{
					Object.Destroy((Object)(object)val);
				}
				catch (Exception)
				{
				}
				val = KandraRendererPoseBaking.BlankMesh(r);
				if ((Object)(object)val == (Object)null)
				{
					return null;
				}
				((Object)val).name = "AvalonUntold_OutlineHull_NoColor";
				((Object)val).hideFlags = (HideFlags)61;
				val.MarkDynamic();
			}
			val.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000f);
			return val;
		}

		private static uint[] WhiteScratch(int count)
		{
			if (_whiteScratch != null && _whiteScratch.Length >= count)
			{
				return _whiteScratch;
			}
			uint[] array = new uint[count];
			for (int i = 0; i < count; i++)
			{
				array[i] = uint.MaxValue;
			}
			_whiteScratch = array;
			return array;
		}

		internal void EmitHull(CommandBuffer cmd, Material material, ref int meshes)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			if (cmd == null || (Object)(object)material == (Object)null)
			{
				return;
			}
			for (int i = 0; i < _outlineMeshes.Count; i++)
			{
				Mesh val = _outlineMeshes[i];
				if (!((Object)(object)val == (Object)null)
					&& _outlinePoseGenerations[i] == _requiredPoseGeneration)
				{
					int num = val.subMeshCount;
					if (num < 1)
					{
						num = 1;
					}
					if (num > 6)
					{
						num = 6;
					}
					for (int j = 0; j < num; j++)
					{
						cmd.DrawMesh(val, Matrix4x4.identity, material, j, 0);
						meshes++;
					}
				}
			}
		}

		private void DestroyMeshAt(int index)
		{
			if (index < 0 || index >= _outlineMeshes.Count)
			{
				return;
			}
			Mesh val = _outlineMeshes[index];
			_outlineMeshes[index] = null;
			_outlinePoseGenerations[index] = 0;
			if ((Object)(object)val == (Object)null)
			{
				return;
			}
			try
			{
				Object.Destroy((Object)(object)val);
			}
			catch (Exception ex)
			{
				Log("outline mesh destroy: " + ex);
			}
		}

		private void ReleaseOutline()
		{
			for (int num = _outlineMeshes.Count - 1; num >= 0; num--)
			{
				DestroyMeshAt(num);
			}
			_outlineMeshes.Clear();
			_outlineKandras.Clear();
			_outlinePoseGenerations.Clear();
			_bakedAny = false;
			_bakeCursor = 0;
			_outlineVisible = false;
			_outlineRoot = null;
		}

		private void ApplyKandra(NpcElement npc)
		{
			Transform parentTransform = npc.ParentTransform;
			if ((Object)(object)parentTransform == (Object)null)
			{
				return;
			}
			KandraRenderer[] componentsInChildren;
			try
			{
				componentsInChildren = ((Component)parentTransform).GetComponentsInChildren<KandraRenderer>(true);
			}
			catch (Exception ex)
			{
				Log("renderer sweep: " + ex);
				return;
			}
			if (componentsInChildren == null)
			{
				return;
			}
			PruneDeadSlots();
			foreach (KandraRenderer val in componentsInChildren)
			{
				if ((Object)(object)val == (Object)null || val.Destroyed || AlreadyInstanced(val))
				{
					continue;
				}
				try
				{
					val.EnsureInitialized();
					val.UseInstancedMaterials();
				}
				catch (Exception ex2)
				{
					Log("UseInstancedMaterials: " + ex2);
					continue;
				}
				_instanced.Add(val);
				_instancedMats.Add(null);
				Material[] array;
				try
				{
					array = val.GetInstantiatedMaterials();
				}
				catch (Exception ex3)
				{
					Log("GetInstantiatedMaterials: " + ex3);
					array = null;
				}
				if (array == null)
				{
					_instanced.RemoveAt(_instanced.Count - 1);
					_instancedMats.RemoveAt(_instancedMats.Count - 1);
					try
					{
						ReleaseRenderer(val, null);
					}
					catch (Exception ex4)
					{
						Log("release after failed read: " + ex4);
					}
					continue;
				}
				_instancedMats[_instancedMats.Count - 1] = (Material[])array.Clone();
				bool flag = false;
				for (int j = 0; j < array.Length; j++)
				{
					try
					{
						flag |= WriteSlot(val, j, array[j]);
					}
					catch (Exception ex5)
					{
						Log("material write: " + ex5);
					}
				}
				if (flag)
				{
					try
					{
						val.TexturesChanged();
					}
					catch (Exception ex6)
					{
						Log("TexturesChanged: " + ex6);
					}
				}
			}
		}

		private bool WriteSlot(KandraRenderer r, int index, Material mat)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)mat == (Object)null)
			{
				return false;
			}
			Slot item = new Slot
			{
				Renderer = r,
				Index = index,
				Mat = mat
			};
			bool flag = false;
			if (mat.HasProperty(EmissiveColorId))
			{
				item.HadColor = true;
				item.PrevColor = mat.GetColor(EmissiveColorId);
				mat.SetColor(EmissiveColorId, _colour * _intensity);
			}
			if (mat.HasProperty(EmissionIntensityId))
			{
				item.HadFloat = true;
				item.PrevFloat = mat.GetFloat(EmissionIntensityId);
				mat.SetFloat(EmissionIntensityId, _emissionIntensityValue);
			}
			if (_mapMode != EmissiveMapMode.Never)
			{
				flag |= MaybeSetWhite(mat, EmissiveColorMapId, ref item.TouchedEmissiveMap, ref item.PrevEmissiveMap);
				flag |= MaybeSetWhite(mat, EmissionMapId, ref item.TouchedEmissionMap, ref item.PrevEmissionMap);
			}
			if (item.HadColor || item.HadFloat || item.TouchedEmissiveMap || item.TouchedEmissionMap)
			{
				_slots.Add(item);
			}
			return flag;
		}

		private bool MaybeSetWhite(Material mat, int id, ref bool touched, ref Texture prev)
		{
			if (!mat.HasProperty(id))
			{
				return false;
			}
			Texture texture = mat.GetTexture(id);
			if (_mapMode == EmissiveMapMode.Auto && (Object)(object)texture != (Object)null)
			{
				return false;
			}
			prev = texture;
			touched = true;
			mat.SetTexture(id, (Texture)(object)Texture2D.whiteTexture);
			return true;
		}

		private bool AlreadyInstanced(KandraRenderer r)
		{
			for (int i = 0; i < _instanced.Count; i++)
			{
				if (_instanced[i] == r)
				{
					return true;
				}
			}
			return false;
		}

		private void PruneDeadSlots()
		{
			for (int num = _instanced.Count - 1; num >= 0; num--)
			{
				KandraRenderer val = _instanced[num];
				if ((Object)(object)val == (Object)null || val.Destroyed)
				{
					_instanced.RemoveAt(num);
					_instancedMats.RemoveAt(num);
					DropSlotsFor(val);
				}
				else if (!TrackingIntact(val))
				{
					ReleaseRenderer(val, _instancedMats[num]);
					_instanced.RemoveAt(num);
					_instancedMats.RemoveAt(num);
					DropSlotsFor(val);
				}
			}
			for (int num2 = _slots.Count - 1; num2 >= 0; num2--)
			{
				KandraRenderer renderer = _slots[num2].Renderer;
				if ((Object)(object)renderer == (Object)null || renderer.Destroyed)
				{
					_slots.RemoveAt(num2);
				}
			}
		}

		private bool TrackingIntact(KandraRenderer r)
		{
			Material[] instantiatedMaterials;
			try
			{
				instantiatedMaterials = r.GetInstantiatedMaterials();
			}
			catch (Exception)
			{
				return false;
			}
			if (instantiatedMaterials == null)
			{
				return false;
			}
			for (int i = 0; i < _slots.Count; i++)
			{
				Slot slot = _slots[i];
				if (slot.Renderer == r)
				{
					if (slot.Index >= instantiatedMaterials.Length)
					{
						return false;
					}
					if (instantiatedMaterials[slot.Index] != slot.Mat)
					{
						return false;
					}
				}
			}
			return true;
		}

		private void DropSlotsFor(KandraRenderer r)
		{
			for (int num = _slots.Count - 1; num >= 0; num--)
			{
				if (_slots[num].Renderer == r)
				{
					_slots.RemoveAt(num);
				}
			}
		}

		private void RevertMaterials()
		{
			for (int i = 0; i < _instanced.Count; i++)
			{
				ReleaseRenderer(_instanced[i], (i < _instancedMats.Count) ? _instancedMats[i] : null);
			}
			_instanced.Clear();
			_instancedMats.Clear();
			_slots.Clear();
		}

		private void ReleaseRenderer(KandraRenderer r, Material[] ownedInstances)
		{
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)r == (Object)null || r.Destroyed)
			{
				return;
			}
			Material[] array = null;
			try
			{
				array = r.GetInstantiatedMaterials();
			}
			catch (Exception ex)
			{
				Log("revert lookup: " + ex);
			}
			if (array != null)
			{
				for (int i = 0; i < _slots.Count; i++)
				{
					Slot slot = _slots[i];
					if (slot.Renderer != r)
					{
						continue;
					}
					try
					{
						if ((Object)(object)slot.Mat == (Object)null || slot.Index >= array.Length || array[slot.Index] != slot.Mat)
						{
							continue;
						}
						if (slot.HadColor)
						{
							slot.Mat.SetColor(EmissiveColorId, slot.PrevColor);
						}
						if (slot.HadFloat)
						{
							slot.Mat.SetFloat(EmissionIntensityId, slot.PrevFloat);
						}
						if (slot.TouchedEmissiveMap)
						{
							slot.Mat.SetTexture(EmissiveColorMapId, slot.PrevEmissiveMap);
						}
						if (slot.TouchedEmissionMap)
						{
							slot.Mat.SetTexture(EmissionMapId, slot.PrevEmissionMap);
						}
						if (slot.TouchedEmissiveMap || slot.TouchedEmissionMap)
						{
							try
							{
								r.TexturesChanged();
							}
							catch (Exception)
							{
							}
						}
					}
					catch (Exception ex3)
					{
						Log("revert slot: " + ex3);
					}
				}
			}
			try
			{
				ushort[] materialsInstancesRefCount = r.rendererData.materialsInstancesRefCount;
				if (materialsInstancesRefCount == null)
				{
					return;
				}
				for (int j = 0; j < materialsInstancesRefCount.Length; j++)
				{
					if (materialsInstancesRefCount[j] != 0 && (ownedInstances == null || (j < ownedInstances.Length && !((Object)(object)ownedInstances[j] == (Object)null) && array != null && j < array.Length && array[j] == ownedInstances[j])))
					{
						r.UseOriginalMaterial(j);
					}
				}
			}
			catch (Exception ex4)
			{
				Log("release instanced materials: " + ex4);
			}
		}

		private void ApplyVfx(NpcElement npc)
		{
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			if (_vfx != null && (Object)(object)_vfx.Instance == (Object)null)
			{
				ReturnVfx();
			}
			if (!_vfxRequested && _vfx == null)
			{
				Transform parentTransform = npc.ParentTransform;
				if (!((Object)(object)parentTransform == (Object)null))
				{
					_vfxRequested = true;
					UniTaskVoid val = SpawnVfx(parentTransform);
					val.Forget();
				}
			}
		}

		[AsyncStateMachine(typeof(_003CSpawnVfx_003Ed__76))]
		private UniTaskVoid SpawnVfx(Transform parent)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			_003CSpawnVfx_003Ed__76 _003CSpawnVfx_003Ed__77 = default(_003CSpawnVfx_003Ed__76);
			_003CSpawnVfx_003Ed__77._003C_003Et__builder = AsyncUniTaskVoidMethodBuilder.Create();
			_003CSpawnVfx_003Ed__77._003C_003E4__this = this;
			_003CSpawnVfx_003Ed__77.parent = parent;
			_003CSpawnVfx_003Ed__77._003C_003E1__state = -1;
			_003CSpawnVfx_003Ed__77._003C_003Et__builder.Start<_003CSpawnVfx_003Ed__76>(ref _003CSpawnVfx_003Ed__77);
			return _003CSpawnVfx_003Ed__77._003C_003Et__builder.Task;
		}

		private void ReturnVfx()
		{
			IPooledInstance vfx = _vfx;
			_vfx = null;
			_vfxRequested = false;
			if (vfx == null)
			{
				return;
			}
			try
			{
				vfx.Return();
			}
			catch (Exception ex)
			{
				Log("vfx return: " + ex);
			}
		}

		private static void Log(string message)
		{
			if (Plugin.Log != null)
			{
				Plugin.Log.Warn("QuestGlow: " + message);
			}
		}
	}
}
