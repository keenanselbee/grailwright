using System;

namespace AvalonUntold
{
	internal static class Lz4Block
	{
		public static int Decompress(byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstLength)
		{
			if (src == null || dst == null)
			{
				return -1;
			}
			if (srcOffset < 0 || srcLength < 0 || srcOffset + srcLength > src.Length)
			{
				return -1;
			}
			if (dstOffset < 0 || dstLength < 0 || dstOffset + dstLength > dst.Length)
			{
				return -1;
			}
			int num = srcOffset;
			int num2 = srcOffset + srcLength;
			int num3 = dstOffset;
			int num4 = dstOffset + dstLength;
			while (num < num2)
			{
				int num5 = src[num++];
				int num6 = num5 >> 4;
				if (num6 == 15)
				{
					int num7;
					do
					{
						if (num >= num2)
						{
							return -1;
						}
						num7 = src[num++];
						num6 += num7;
						if (num6 < 0)
						{
							return -1;
						}
					}
					while (num7 == 255);
				}
				if (num6 > 0)
				{
					if (num + num6 > num2 || num3 + num6 > num4)
					{
						return -1;
					}
					Buffer.BlockCopy(src, num, dst, num3, num6);
					num += num6;
					num3 += num6;
				}
				if (num >= num2)
				{
					break;
				}
				if (num + 2 > num2)
				{
					return -1;
				}
				int num8 = src[num] | (src[num + 1] << 8);
				num += 2;
				if (num8 <= 0 || num3 - num8 < dstOffset)
				{
					return -1;
				}
				int num9 = num5 & 0xF;
				if (num9 == 15)
				{
					int num10;
					do
					{
						if (num >= num2)
						{
							return -1;
						}
						num10 = src[num++];
						num9 += num10;
						if (num9 < 0)
						{
							return -1;
						}
					}
					while (num10 == 255);
				}
				num9 += 4;
				if (num3 + num9 > num4)
				{
					return -1;
				}
				int num11 = num3 - num8;
				for (int i = 0; i < num9; i++)
				{
					dst[num3++] = dst[num11++];
				}
			}
			return num3 - dstOffset;
		}
	}
}
