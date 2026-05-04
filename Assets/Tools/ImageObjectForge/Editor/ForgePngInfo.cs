using System.IO;
using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgePngInfo
    {
        public class Info
        {
            public bool isPng;
            public int width;
            public int height;
            public int bitDepth;
            public int colorType;
            public bool hasDpi;
            public int dpiX;
            public int dpiY;
        }

        public static Info Read(string absPath)
        {
            var info = new Info();
            if (string.IsNullOrEmpty(absPath) || !File.Exists(absPath)) return info;

            byte[] data;
            try { data = File.ReadAllBytes(absPath); }
            catch { return info; }

            if (data.Length < 8 + 8 + 13) return info;

            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
                return info;

            info.isPng = true;

            int pos = 8;
            int chunkLen = ReadBE(data, pos);
            string chunkType = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            if (chunkType != "IHDR" || chunkLen != 13) return info;

            info.width = ReadBE(data, pos + 8);
            info.height = ReadBE(data, pos + 12);
            info.bitDepth = data[pos + 16];
            info.colorType = data[pos + 17];

            pos += 12 + chunkLen;

            while (pos + 12 <= data.Length)
            {
                int len = ReadBE(data, pos);
                if (pos + 12 + len > data.Length) break;
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (type == "pHYs" && len == 9)
                {
                    int ppuX = ReadBE(data, pos + 8);
                    int ppuY = ReadBE(data, pos + 12);
                    int unit = data[pos + 16];
                    if (unit == 1)
                    {
                        info.hasDpi = true;
                        info.dpiX = Mathf.RoundToInt(ppuX * 0.0254f);
                        info.dpiY = Mathf.RoundToInt(ppuY * 0.0254f);
                    }
                    break;
                }
                if (type == "IEND") break;
                pos += 12 + len;
            }

            return info;
        }

        static int ReadBE(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
