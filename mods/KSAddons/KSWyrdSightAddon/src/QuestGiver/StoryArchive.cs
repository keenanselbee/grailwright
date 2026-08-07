using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace AvalonUntold
{
	public sealed class StoryArchive
	{
		public const string StoryExtension = ".story";

		public const int GuidLength = 32;

		private const long MaxArchiveBytes = 2147483648L;

		private const int MaxBlocksInfoBytes = 67108864;

		private const int MaxBlocks = 1048576;

		private const int MaxDirectoryEntries = 2097152;

		private const int MaxSampleNames = 8;

		public bool Ok;

		public string FailureReason;

		public string ArchivePath;

		public readonly List<string> PathsTried = new List<string>(4);

		public long ArchiveBytes;

		public int UnityFsVersion;

		public string UnityVersion = "";

		public string UnityRevision = "";

		public uint Flags;

		public int BlockCount;

		public int DirectoryCount;

		public int NonConformingNames;

		public int MixedCaseNames;

		public readonly List<string> SampleNonConformingNames = new List<string>(8);

		public long ParseMillis = -1L;

		public readonly List<string> Guids = new List<string>();

		public readonly HashSet<string> GuidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, string> _canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public int Count => Guids.Count;

		public bool Contains(string guid)
		{
			if (!string.IsNullOrEmpty(guid))
			{
				return GuidSet.Contains(guid);
			}
			return false;
		}

		public string Canonical(string guid)
		{
			if (string.IsNullOrEmpty(guid))
			{
				return null;
			}
			if (!_canonical.TryGetValue(guid, out var value))
			{
				return null;
			}
			return value;
		}

		public bool TryLoad()
		{
			string path;
			return TryResolvePath(out path) && TryLoadResolvedPath(path);
		}

		internal bool TryResolvePath(out string path)
		{
			path = null;
			try
			{
				List<string> candidates = CandidatePaths();
				for (int i = 0; i < candidates.Count; i++)
				{
					if (!PathsTried.Contains(candidates[i]))
					{
						PathsTried.Add(candidates[i]);
					}
				}

				for (int i = 0; i < PathsTried.Count; i++)
				{
					try
					{
						if (File.Exists(PathsTried[i]))
						{
							path = PathsTried[i];
							return true;
						}
					}
					catch (Exception)
					{
					}
				}

				FailureReason = "story.arch was not found. Tried: " + ((PathsTried.Count == 0) ? "(no candidate path could even be built)" : string.Join(" ; ", PathsTried.ToArray()));
				return false;
			}
			catch (Exception exception)
			{
				FailureReason = "archive location threw " + exception.GetType().Name + ": " + exception.Message;
				return false;
			}
		}

		internal bool TryLoadResolvedPath(string path)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				return TryLoadFrom(path);
			}
			finally
			{
				stopwatch.Stop();
				ParseMillis = stopwatch.ElapsedMilliseconds;
			}
		}

		public bool TryLoadFrom(string path)
		{
			ArchivePath = path;
			Ok = false;
			FailureReason = null;
			try
			{
				if (!Parse(path, out var fail))
				{
					FailureReason = fail ?? "unspecified parse failure";
					Guids.Clear();
					GuidSet.Clear();
					_canonical.Clear();
					return false;
				}
				if (Guids.Count == 0)
				{
					FailureReason = "the archive directory parsed cleanly but held 0 usable `.story` entries (" + DirectoryCount + " entries, " + NonConformingNames + " non-conforming)";
					return false;
				}
				Ok = true;
				return true;
			}
			catch (Exception ex)
			{
				FailureReason = "parse threw " + ex.GetType().Name + ": " + ex.Message;
				Guids.Clear();
				GuidSet.Clear();
				_canonical.Clear();
				return false;
			}
		}

		private static List<string> CandidatePaths()
		{
			List<string> list = new List<string>(4);
			string text = "Stroy";
			string text2 = "story.arch";
			try
			{
				string streamingAssetsPath = Application.streamingAssetsPath;
				if (!string.IsNullOrEmpty(streamingAssetsPath))
				{
					list.Add(Path.Combine(streamingAssetsPath, text, text2));
				}
			}
			catch (Exception)
			{
			}
			try
			{
				string gameRootPath = Paths.GameRootPath;
				if (!string.IsNullOrEmpty(gameRootPath) && Directory.Exists(gameRootPath))
				{
					string[] directories = Directory.GetDirectories(gameRootPath, "*_Data");
					Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
					for (int i = 0; i < directories.Length; i++)
					{
						list.Add(Path.Combine(directories[i], "StreamingAssets", text, text2));
					}
				}
			}
			catch (Exception)
			{
			}
			return list;
		}

		private bool Parse(string path, out string fail)
		{
			fail = null;
			FileInfo fileInfo = new FileInfo(path);
			if (!fileInfo.Exists)
			{
				fail = "not found: " + path;
				return false;
			}
			ArchiveBytes = fileInfo.Length;
			if (ArchiveBytes < 64)
			{
				fail = "file is only " + ArchiveBytes + " bytes - not a UnityFS archive";
				return false;
			}
			if (ArchiveBytes > 2147483648u)
			{
				fail = "file is " + ArchiveBytes + " bytes, over the " + 2147483648L + " byte sanity limit";
				return false;
			}
			byte[] array2;
			using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
			{
				if (!TryReadCString(fileStream, 32, out var v))
				{
					fail = "could not read the signature";
					return false;
				}
				if (v != "UnityFS")
				{
					fail = "signature is \"" + v + "\", expected \"UnityFS\"";
					return false;
				}
				if (!TryReadU32BE(fileStream, out var v2))
				{
					fail = "truncated at the version field";
					return false;
				}
				UnityFsVersion = (int)v2;
				if (v2 < 6 || v2 > 8)
				{
					fail = "UnityFS format version " + v2 + " is outside the 6..8 range this parser was written against";
					return false;
				}
				if (!TryReadCString(fileStream, 64, out UnityVersion))
				{
					fail = "truncated at unityVersion";
					return false;
				}
				if (!TryReadCString(fileStream, 64, out UnityRevision))
				{
					fail = "truncated at unityRevision";
					return false;
				}
				if (!TryReadI64BE(fileStream, out var v3))
				{
					fail = "truncated at the size field";
					return false;
				}
				if (v3 != ArchiveBytes)
				{
					fail = "header says the archive is " + v3 + " bytes but the file is " + ArchiveBytes;
					return false;
				}
				if (!TryReadU32BE(fileStream, out var v4))
				{
					fail = "truncated at compressedBlocksInfoSize";
					return false;
				}
				if (!TryReadU32BE(fileStream, out var v5))
				{
					fail = "truncated at uncompressedBlocksInfoSize";
					return false;
				}
				if (!TryReadU32BE(fileStream, out Flags))
				{
					fail = "truncated at flags";
					return false;
				}
				if (v4 == 0 || v4 > ArchiveBytes)
				{
					fail = "compressedBlocksInfoSize " + v4 + " is not inside a " + ArchiveBytes + " byte file";
					return false;
				}
				if (v5 == 0 || v5 > 67108864)
				{
					fail = "uncompressedBlocksInfoSize " + v5 + " is outside 1.." + 67108864;
					return false;
				}
				if (v2 >= 7)
				{
					long num = (fileStream.Position + 15) & -16;
					if (num > ArchiveBytes)
					{
						fail = "aligned header end " + num + " is past EOF";
						return false;
					}
					fileStream.Position = num;
				}
				long num2 = (((Flags & 0x80) != 0) ? (ArchiveBytes - v4) : fileStream.Position);
				if (num2 < 0 || num2 + v4 > ArchiveBytes)
				{
					fail = "blocksInfo range " + num2 + ".." + (num2 + v4) + " is not inside the file";
					return false;
				}
				fileStream.Position = num2;
				byte[] array = new byte[v4];
				if (!TryReadExactly(fileStream, array, 0, (int)v4))
				{
					fail = "truncated while reading " + v4 + " bytes of blocksInfo";
					return false;
				}
				int num3 = (int)(Flags & 0x3F);
				switch (num3)
				{
				case 0:
					if (v4 != v5)
					{
						fail = "blocksInfo claims no compression but the two sizes differ (" + v4 + " vs " + v5 + ")";
						return false;
					}
					array2 = array;
					break;
				case 2:
				case 3:
				{
					array2 = new byte[v5];
					int num4 = Lz4Block.Decompress(array, 0, array.Length, array2, 0, array2.Length);
					if (num4 != (int)v5)
					{
						fail = "LZ4 decompress of blocksInfo produced " + num4 + " bytes, expected " + v5;
						return false;
					}
					break;
				}
				default:
					fail = "blocksInfo compression type " + num3 + " is not supported (only none/LZ4/LZ4HC)";
					return false;
				}
			}
			return ParseBlocksInfo(array2, out fail);
		}

		private bool ParseBlocksInfo(byte[] b, out string fail)
		{
			fail = null;
			int p = 16;
			if (!TryU32BE(b, ref p, out var v))
			{
				fail = "blocksInfo truncated at blockCount";
				return false;
			}
			if (v == 0 || v > 1048576)
			{
				fail = "blockCount " + v + " is outside 1.." + 1048576;
				return false;
			}
			BlockCount = (int)v;
			long num = 0L;
			for (int i = 0; i < BlockCount; i++)
			{
				if (!TryU32BE(b, ref p, out var v2) || !TryU32BE(b, ref p, out var _) || !TryU16BE(b, ref p, out var _))
				{
					fail = "blocksInfo truncated in block " + i + " of " + BlockCount;
					return false;
				}
				num += v2;
			}
			if (num <= 0)
			{
				fail = "the archive declares " + BlockCount + " block(s) totalling 0 uncompressed bytes";
				return false;
			}
			if (!TryU32BE(b, ref p, out var v5))
			{
				fail = "blocksInfo truncated at directoryCount";
				return false;
			}
			if (v5 == 0)
			{
				fail = "the archive directory is empty";
				return false;
			}
			if (v5 > 2097152)
			{
				fail = "directoryCount " + v5 + " is over the " + 2097152 + " sanity limit";
				return false;
			}
			DirectoryCount = (int)v5;
			for (int j = 0; j < DirectoryCount; j++)
			{
				if (!TryI64BE(b, ref p, out var v6) || !TryI64BE(b, ref p, out var v7) || !TryU32BE(b, ref p, out var _) || !TryCString(b, ref p, out var v9))
				{
					fail = "blocksInfo truncated in directory entry " + j + " of " + DirectoryCount;
					return false;
				}
				if (v6 < 0 || v7 < 0 || v6 + v7 > num)
				{
					fail = "directory entry " + j + " (\"" + v9 + "\") spans " + v6 + ".." + (v6 + v7) + ", outside the " + num + " byte data blob";
					return false;
				}
				string text = GuidFromEntryName(v9);
				if (text == null)
				{
					NonConformingNames++;
					if (SampleNonConformingNames.Count < 8)
					{
						SampleNonConformingNames.Add(v9);
					}
					continue;
				}
				if (!IsAllLowercase(text))
				{
					MixedCaseNames++;
					if (SampleNonConformingNames.Count < 8)
					{
						SampleNonConformingNames.Add(v9);
					}
				}
				if (GuidSet.Add(text))
				{
					Guids.Add(text);
					_canonical[text] = text;
				}
			}
			if (p != b.Length)
			{
				fail = "blocksInfo has " + (b.Length - p) + " unread trailing byte(s) after " + DirectoryCount + " directory entries - the layout is not what this parser expects";
				return false;
			}
			return true;
		}

		public static string GuidFromEntryName(string name)
		{
			if (name == null)
			{
				return null;
			}
			if (name.Length != 32 + ".story".Length)
			{
				return null;
			}
			if (!name.EndsWith(".story", StringComparison.Ordinal))
			{
				return null;
			}
			for (int i = 0; i < 32; i++)
			{
				char c = name[i];
				if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
				{
					return null;
				}
			}
			return name.Substring(0, 32);
		}

		private static bool IsAllLowercase(string s)
		{
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] >= 'A' && s[i] <= 'F')
				{
					return false;
				}
			}
			return true;
		}

		private static bool TryReadExactly(Stream s, byte[] into, int offset, int count)
		{
			int num;
			for (int i = 0; i < count; i += num)
			{
				num = s.Read(into, offset + i, count - i);
				if (num <= 0)
				{
					return false;
				}
			}
			return true;
		}

		private static bool TryReadU32BE(Stream s, out uint v)
		{
			v = 0u;
			byte[] array = new byte[4];
			if (!TryReadExactly(s, array, 0, 4))
			{
				return false;
			}
			v = (uint)((array[0] << 24) | (array[1] << 16) | (array[2] << 8) | array[3]);
			return true;
		}

		private static bool TryReadI64BE(Stream s, out long v)
		{
			v = 0L;
			byte[] array = new byte[8];
			if (!TryReadExactly(s, array, 0, 8))
			{
				return false;
			}
			for (int i = 0; i < 8; i++)
			{
				v = (v << 8) | array[i];
			}
			return true;
		}

		private static bool TryReadCString(Stream s, int max, out string v)
		{
			v = null;
			StringBuilder stringBuilder = new StringBuilder(16);
			for (int i = 0; i <= max; i++)
			{
				int num = s.ReadByte();
				if (num < 0)
				{
					return false;
				}
				if (num == 0)
				{
					v = stringBuilder.ToString();
					return true;
				}
				stringBuilder.Append((char)num);
			}
			return false;
		}

		private static bool TryU32BE(byte[] b, ref int p, out uint v)
		{
			v = 0u;
			if (p < 0 || p + 4 > b.Length)
			{
				return false;
			}
			v = (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
			p += 4;
			return true;
		}

		private static bool TryU16BE(byte[] b, ref int p, out ushort v)
		{
			v = 0;
			if (p < 0 || p + 2 > b.Length)
			{
				return false;
			}
			v = (ushort)((b[p] << 8) | b[p + 1]);
			p += 2;
			return true;
		}

		private static bool TryI64BE(byte[] b, ref int p, out long v)
		{
			v = 0L;
			if (p < 0 || p + 8 > b.Length)
			{
				return false;
			}
			long num = 0L;
			for (int i = 0; i < 8; i++)
			{
				num = (num << 8) | b[p + i];
			}
			p += 8;
			v = num;
			return true;
		}

		private static bool TryCString(byte[] b, ref int p, out string v)
		{
			v = null;
			if (p < 0 || p >= b.Length)
			{
				return false;
			}
			int num = p;
			while (p < b.Length && b[p] != 0)
			{
				p++;
			}
			if (p >= b.Length)
			{
				return false;
			}
			v = Encoding.UTF8.GetString(b, num, p - num);
			p++;
			return true;
		}
	}
}
