using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Awaken.TG.Main.Stories.Runtime;
using Awaken.Utility.Archives;
using Awaken.Utility.Files;
using Unity.IO.LowLevel.Unsafe;

namespace AvalonUntold
{
	public sealed class GraphIndex
	{
		private const int MaxRecordedParseFailures = 20;

		private const int MaxRecordedDangling = 20;

		public readonly List<string> Candidates = new List<string>();

		private StoryArchive _archive;

		private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _referencedByNpc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _referencedByCrossGraph = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public int NpcBookmarkRefs;

		public int NpcBookmarkRefsOutsideArchive;

		public int CrossGraphRefs;

		public int CrossGraphRefsOutsideArchive;

		public readonly List<string> SampleDanglingBookmarks = new List<string>();

		public bool BasePathVerified;

		public string BasePathNote = "not attempted";

		public string BasePath;

		public long BasePathMillis = -1L;

		public int LoadAttempts;

		public int ParseFailures;

		public readonly List<string> ParseFailureGuids = new List<string>();

		public int RefusedNotInDirectory;

		public int NpcSeededGraphs => _referencedByNpc.Count;

		public int CrossReferencedGraphs => _referencedByCrossGraph.Count;

		public int DirectoryCount
		{
			get
			{
				if (_archive != null)
				{
					return _archive.DirectoryCount;
				}
				return 0;
			}
		}

		internal void Initialize(StoryArchive archive)
		{
			_archive = archive;
			Candidates.Clear();
			if (archive != null && archive.Ok)
			{
				Candidates.AddRange(archive.Guids);
			}
		}

		public bool InDirectory(string guid)
		{
			if (_archive != null && _archive.Ok)
			{
				return _archive.Contains(guid);
			}
			return false;
		}

		internal unsafe void VerifyBasePath(ManualLogSourceShim log)
		{
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c8: Invalid comparison between Unknown and I4
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				if (_archive == null || !_archive.Ok || Candidates.Count == 0)
				{
					BasePathNote = "not attempted: the archive directory is unavailable";
					return;
				}
				string text = TryReadCachedBasePath();
				if (text != null && IsUnmountedLiteral(text))
				{
					BasePathNote = "REFUSED: the game has cached StoryGraphRuntime.s_basePath = \"" + text + "\", the un-mounted literal it assigns BEFORE attempting the mount. It never retries, so every StoryGraphRuntime.Get this session resolves to a path that does not exist. Loading anything would size a buffer from a FileSize of -1.";
					log.Warn(BasePathNote);
					return;
				}
				if (text != null)
				{
					BasePath = text;
					BasePathNote = "the game had already mounted the story archive";
				}
				else
				{
					string bakingDirectoryPath = StoryGraphRuntime.BakingDirectoryPath;
					bool flag;
					try
					{
						flag = ArchiveUtils.TryMountAndAdjustPath("Story", "Stroy", "story.arch", ref bakingDirectoryPath);
					}
					catch (Exception ex)
					{
						BasePathNote = "REFUSED: ArchiveUtils.TryMountAndAdjustPath threw " + ex.GetType().Name;
						log.Warn(BasePathNote);
						return;
					}
					if (!flag || IsUnmountedLiteral(bakingDirectoryPath))
					{
						BasePathNote = "REFUSED: the story archive would not mount (TryMountAndAdjustPath returned " + flag + ", path \"" + bakingDirectoryPath + "\")";
						log.Warn(BasePathNote);
						return;
					}
					BasePath = bakingDirectoryPath;
					BasePathNote = "mounted by the plugin";
				}
				string text2 = Candidates[0];
				string text3 = Path.Combine(BasePath, text2 + ".story");
				FileInfoResult fileInfo;
				try
				{
					fileInfo = FileRead.GetFileInfo(text3);
				}
				catch (Exception ex2)
				{
					BasePathNote = "REFUSED: FileRead.GetFileInfo threw " + ex2.GetType().Name + " on \"" + text3 + "\"";
					log.Warn(BasePathNote);
					return;
				}
				if ((int)fileInfo.FileState != 1 || fileInfo.FileSize <= 0)
				{
					BasePathNote = "REFUSED: \"" + text3 + "\" reads FileState=" + ((object)(*(FileState*)(&fileInfo.FileState))/*cast due to constrained. prefix*/).ToString() + ", FileSize=" + fileInfo.FileSize + " - but that GUID is entry 0 of the shipped archive directory, so the base path does not resolve. Nothing was loaded.";
					log.Warn(BasePathNote);
				}
				else
				{
					BasePathVerified = true;
					BasePathNote = BasePathNote + "; verified by a metadata read on a directory entry (FileSize=" + fileInfo.FileSize + ")";
				}
			}
			catch (Exception ex3)
			{
				BasePathNote = "REFUSED: base path verification threw " + ex3.GetType().Name;
				log.Warn(BasePathNote);
			}
			finally
			{
				stopwatch.Stop();
				BasePathMillis = stopwatch.ElapsedMilliseconds;
			}
		}

		private static bool IsUnmountedLiteral(string s)
		{
			return string.Equals(s, StoryGraphRuntime.BakingDirectoryPath, StringComparison.OrdinalIgnoreCase);
		}

		public static string TryReadCachedBasePath()
		{
			try
			{
				FieldInfo field = typeof(StoryGraphRuntime).GetField("s_basePath", BindingFlags.Static | BindingFlags.NonPublic);
				if (field == null)
				{
					return null;
				}
				return field.GetValue(null) as string;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public bool TryGet(string guid, out StoryGraphRuntime graph)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			graph = default(StoryGraphRuntime);
			if (!BasePathVerified)
			{
				return false;
			}
			if (string.IsNullOrEmpty(guid) || guid == "(null)")
			{
				return false;
			}
			if (_failed.Contains(guid))
			{
				return false;
			}
			if (!InDirectory(guid))
			{
				_failed.Add(guid);
				RefusedNotInDirectory++;
				return false;
			}
			string text = _archive.Canonical(guid);
			if (text == null)
			{
				_failed.Add(guid);
				RefusedNotInDirectory++;
				return false;
			}
			LoadAttempts++;
			StoryGraphRuntime? val;
			try
			{
				val = StoryGraphRuntime.Get(text);
			}
			catch (Exception)
			{
				val = null;
			}
			if (val.HasValue)
			{
				StoryGraphRuntime value = val.Value;
				if (value.IsCreated)
				{
					graph = val.Value;
					return true;
				}
			}
			_failed.Add(guid);
			ParseFailures++;
			if (ParseFailureGuids.Count < 20)
			{
				ParseFailureGuids.Add(guid);
			}
			return false;
		}

		public bool NoteNpcBookmark(string guid)
		{
			if (string.IsNullOrEmpty(guid) || guid == "(null)")
			{
				return false;
			}
			NpcBookmarkRefs++;
			if (!InDirectory(guid))
			{
				NpcBookmarkRefsOutsideArchive++;
				Sample("N:" + guid);
				return false;
			}
			_referencedByNpc.Add(guid);
			return true;
		}

		public bool NoteCrossGraphRef(string guid)
		{
			if (string.IsNullOrEmpty(guid) || guid == "(null)")
			{
				return false;
			}
			CrossGraphRefs++;
			if (!InDirectory(guid))
			{
				CrossGraphRefsOutsideArchive++;
				Sample("D:" + guid);
				return false;
			}
			_referencedByCrossGraph.Add(guid);
			return true;
		}

		private void Sample(string s)
		{
			if (SampleDanglingBookmarks.Count < 20 && !SampleDanglingBookmarks.Contains(s))
			{
				SampleDanglingBookmarks.Add(s);
			}
		}
	}
}
