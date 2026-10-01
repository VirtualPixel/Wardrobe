using System;
using System.IO;
using System.IO.Compression;

namespace Wardrobe.Services
{
    // Just enough PNG for the icons, so the slow part of loading one (inflating and unfiltering two
    // million pixels) runs on a worker thread and the main thread only uploads the result. Reads
    // 8-bit RGB and RGBA without interlacing, which is all Texture2D.EncodeToPNG writes; anything
    // else comes back null and the caller renders the look fresh. Pixels are RGBA with the bottom
    // row first, the way Texture2D.LoadRawTextureData wants them.
    internal static class PngCodec
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly uint[] crcTable = BuildCrcTable();

        public static byte[]? Decode(byte[] png, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (png.Length < 8 + 25 || !StartsWithSignature(png))
            {
                return null;
            }
            int channels = 0;
            var idat = new MemoryStream();
            int at = 8;
            while (at + 12 <= png.Length)
            {
                int length = ReadInt(png, at);
                if (length < 0 || at + 12 + length > png.Length)
                {
                    return null;
                }
                string type = new string(new[] { (char)png[at + 4], (char)png[at + 5], (char)png[at + 6], (char)png[at + 7] });
                int data = at + 8;
                if (type == "IHDR")
                {
                    width = ReadInt(png, data);
                    height = ReadInt(png, data + 4);
                    byte depth = png[data + 8];
                    byte colour = png[data + 9];
                    byte interlace = png[data + 12];
                    channels = colour == 6 ? 4 : colour == 2 ? 3 : 0;
                    if (depth != 8 || channels == 0 || interlace != 0 || width <= 0 || height <= 0 || (long)width * height > 16L * 1024 * 1024)
                    {
                        return null;
                    }
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, data, length);
                }
                else if (type == "IEND")
                {
                    break;
                }
                at += 12 + length;
            }
            if (channels == 0 || idat.Length < 2)
            {
                return null;
            }

            int stride = width * channels;
            var raw = new byte[(stride + 1) * height];
            idat.Position = 2;
            using (var inflate = new DeflateStream(idat, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < raw.Length)
                {
                    int got = inflate.Read(raw, read, raw.Length - read);
                    if (got <= 0)
                    {
                        return null;
                    }
                    read += got;
                }
            }

            var rgba = new byte[width * height * 4];
            var previous = new byte[stride];
            var line = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int start = y * (stride + 1);
                if (!Unfilter(raw[start], raw, start + 1, line, previous, channels))
                {
                    return null;
                }
                int to = (height - 1 - y) * width * 4;
                if (channels == 4)
                {
                    Buffer.BlockCopy(line, 0, rgba, to, stride);
                }
                else
                {
                    for (int x = 0, from = 0; x < width; x++, from += 3, to += 4)
                    {
                        rgba[to] = line[from];
                        rgba[to + 1] = line[from + 1];
                        rgba[to + 2] = line[from + 2];
                        rgba[to + 3] = 255;
                    }
                }
                byte[] swap = previous;
                previous = line;
                line = swap;
            }
            return rgba;
        }

        public static byte[] Encode(byte[] rgba, int width, int height)
        {
            int stride = width * 4;
            var raw = new byte[(stride + 1) * height];
            for (int y = 0; y < height; y++)
            {
                // Paeth on every row: the icons are mostly flat colour and empty space.
                int row = (height - 1 - y) * stride;
                int above = (height - y) * stride;
                int to = y * (stride + 1);
                raw[to++] = 4;
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= 4 ? rgba[row + i - 4] : 0;
                    int b = y > 0 ? rgba[above + i] : 0;
                    int c = i >= 4 && y > 0 ? rgba[above + i - 4] : 0;
                    raw[to + i] = (byte)(rgba[row + i] - Paeth(a, b, c));
                }
            }

            var zlib = new MemoryStream();
            zlib.WriteByte(0x78);
            zlib.WriteByte(0x9C);
            using (var deflate = new DeflateStream(zlib, CompressionLevel.Fastest, true))
            {
                deflate.Write(raw, 0, raw.Length);
            }
            WriteInt(zlib, (int)Adler32(raw));

            var png = new MemoryStream();
            png.Write(Signature, 0, Signature.Length);
            var header = new byte[13];
            PutInt(header, 0, width);
            PutInt(header, 4, height);
            header[8] = 8;
            header[9] = 6;
            Chunk(png, "IHDR", header, header.Length);
            Chunk(png, "IDAT", zlib.GetBuffer(), (int)zlib.Length);
            Chunk(png, "IEND", new byte[0], 0);
            return png.ToArray();
        }

        // Averages each block of the source into one pixel. Colour is weighted by alpha so the
        // empty space round the Semibot does not darken its outline.
        public static byte[] Shrink(byte[] rgba, int width, int height, int toWidth, int toHeight)
        {
            var result = new byte[toWidth * toHeight * 4];
            for (int ty = 0; ty < toHeight; ty++)
            {
                int y0 = (int)((long)ty * height / toHeight);
                int y1 = Math.Max(y0 + 1, (int)((long)(ty + 1) * height / toHeight));
                for (int tx = 0; tx < toWidth; tx++)
                {
                    int x0 = (int)((long)tx * width / toWidth);
                    int x1 = Math.Max(x0 + 1, (int)((long)(tx + 1) * width / toWidth));
                    long r = 0, g = 0, b = 0, a = 0;
                    int n = 0;
                    for (int y = y0; y < y1; y++)
                    {
                        int at = (y * width + x0) * 4;
                        for (int x = x0; x < x1; x++, at += 4)
                        {
                            int alpha = rgba[at + 3];
                            r += rgba[at] * alpha;
                            g += rgba[at + 1] * alpha;
                            b += rgba[at + 2] * alpha;
                            a += alpha;
                            n++;
                        }
                    }
                    int to = (ty * toWidth + tx) * 4;
                    if (a > 0)
                    {
                        result[to] = (byte)(r / a);
                        result[to + 1] = (byte)(g / a);
                        result[to + 2] = (byte)(b / a);
                        result[to + 3] = (byte)((a + n / 2) / n);
                    }
                }
            }
            return result;
        }

        private static bool Unfilter(byte filter, byte[] raw, int at, byte[] line, byte[] previous, int bpp)
        {
            int stride = line.Length;
            switch (filter)
            {
                case 0:
                    Buffer.BlockCopy(raw, at, line, 0, stride);
                    return true;
                case 1:
                    for (int i = 0; i < stride; i++)
                    {
                        line[i] = (byte)(raw[at + i] + (i >= bpp ? line[i - bpp] : 0));
                    }
                    return true;
                case 2:
                    for (int i = 0; i < stride; i++)
                    {
                        line[i] = (byte)(raw[at + i] + previous[i]);
                    }
                    return true;
                case 3:
                    for (int i = 0; i < stride; i++)
                    {
                        line[i] = (byte)(raw[at + i] + (((i >= bpp ? line[i - bpp] : 0) + previous[i]) >> 1));
                    }
                    return true;
                case 4:
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0;
                        int c = i >= bpp ? previous[i - bpp] : 0;
                        line[i] = (byte)(raw[at + i] + Paeth(a, previous[i], c));
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            return pb <= pc ? b : c;
        }

        private static bool StartsWithSignature(byte[] data)
        {
            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static int ReadInt(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

        private static void PutInt(byte[] data, int at, int value)
        {
            data[at] = (byte)(value >> 24);
            data[at + 1] = (byte)(value >> 16);
            data[at + 2] = (byte)(value >> 8);
            data[at + 3] = (byte)value;
        }

        private static void WriteInt(Stream stream, int value)
        {
            var bytes = new byte[4];
            PutInt(bytes, 0, value);
            stream.Write(bytes, 0, 4);
        }

        private static void Chunk(Stream png, string type, byte[] data, int length)
        {
            WriteInt(png, length);
            var name = new[] { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
            png.Write(name, 0, 4);
            png.Write(data, 0, length);
            uint crc = 0xFFFFFFFF;
            crc = Crc(crc, name, 4);
            crc = Crc(crc, data, length);
            WriteInt(png, (int)(crc ^ 0xFFFFFFFF));
        }

        private static uint Crc(uint crc, byte[] data, int length)
        {
            for (int i = 0; i < length; i++)
            {
                crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}
