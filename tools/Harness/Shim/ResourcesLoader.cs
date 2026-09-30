// Shim de UnityEngine — carregamento real de Resources/Art/*.png.
// Fora do editor não existe importador de assets, então o shim lê o PNG do disco e monta
// a Texture2D na mão. Isso faz UiKit.Art() devolver arte com DIMENSÕES REAIS, que é o que
// o layout (preserveAspect, LayoutElement) consome.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace UnityEngine
{
    internal static class ResourcesLoader
    {
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        private static string _root;

        /// <summary>Pasta unity/Assets/Resources, procurada subindo a partir do binário.</summary>
        internal static string Root
        {
            get
            {
                if (_root != null)
                {
                    return _root;
                }

                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "unity", "Assets", "Resources");
                    if (Directory.Exists(candidate))
                    {
                        return _root = candidate;
                    }

                    dir = dir.Parent;
                }

                return _root = string.Empty;
            }
        }

        internal static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || Root.Length == 0)
            {
                return null;
            }

            if (_cache.TryGetValue(path, out Texture2D cached))
            {
                return cached;
            }

            string file = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar) + ".png");
            Texture2D texture = File.Exists(file) ? DecodePng(File.ReadAllBytes(file)) : null;
            if (texture != null)
            {
                texture.name = path;
                texture.Apply();
            }

            _cache[path] = texture;
            return texture;
        }

        /// <summary>
        /// Decodificador PNG suficiente para a arte do jogo: 8 bits por canal, sem entrelaçamento,
        /// tipos de cor 0/2/3/4/6. Devolve null no que não souber ler.
        /// </summary>
        private static Texture2D DecodePng(byte[] bytes)
        {
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G')
            {
                return null;
            }

            int width = 0;
            int height = 0;
            int bitDepth = 0;
            int colorType = 0;
            int interlace = 0;
            byte[] palette = null;
            byte[] paletteAlpha = null;
            var idat = new MemoryStream();

            int offset = 8;
            while (offset + 8 <= bytes.Length)
            {
                int length = ReadInt32(bytes, offset);
                string type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                int dataStart = offset + 8;
                if (length < 0 || dataStart + length > bytes.Length)
                {
                    return null;
                }

                switch (type)
                {
                    case "IHDR":
                        width = ReadInt32(bytes, dataStart);
                        height = ReadInt32(bytes, dataStart + 4);
                        bitDepth = bytes[dataStart + 8];
                        colorType = bytes[dataStart + 9];
                        interlace = bytes[dataStart + 12];
                        break;
                    case "PLTE":
                        palette = new byte[length];
                        Array.Copy(bytes, dataStart, palette, 0, length);
                        break;
                    case "tRNS":
                        paletteAlpha = new byte[length];
                        Array.Copy(bytes, dataStart, paletteAlpha, 0, length);
                        break;
                    case "IDAT":
                        idat.Write(bytes, dataStart, length);
                        break;
                }

                offset = dataStart + length + 4;
                if (type == "IEND")
                {
                    break;
                }
            }

            if (width <= 0 || height <= 0 || bitDepth != 8 || interlace != 0)
            {
                return null;
            }

            int channels = colorType switch
            {
                0 => 1,
                2 => 3,
                3 => 1,
                4 => 2,
                6 => 4,
                _ => 0,
            };

            if (channels == 0)
            {
                return null;
            }

            byte[] raw = Inflate(idat.ToArray());
            if (raw == null)
            {
                return null;
            }

            int stride = width * channels;
            if (raw.Length < (stride + 1) * height)
            {
                return null;
            }

            var pixels = new Color32[width * height];
            var previous = new byte[stride];
            var current = new byte[stride];

            int cursor = 0;
            for (int y = 0; y < height; y++)
            {
                int filter = raw[cursor++];
                Array.Copy(raw, cursor, current, 0, stride);
                cursor += stride;
                Unfilter(filter, current, previous, channels);

                // PNG desce de cima para baixo; a Texture2D da Unity sobe de baixo para cima.
                int destRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    pixels[destRow + x] = ToColor(current, x * channels, colorType, palette, paletteAlpha);
                }

                byte[] swap = previous;
                previous = current;
                current = swap;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            return texture;
        }

        private static Color32 ToColor(byte[] row, int index, int colorType, byte[] palette, byte[] paletteAlpha)
        {
            switch (colorType)
            {
                case 0:
                    return new Color32(row[index], row[index], row[index], 255);
                case 2:
                    return new Color32(row[index], row[index + 1], row[index + 2], 255);
                case 3:
                {
                    int p = row[index] * 3;
                    byte alpha = paletteAlpha != null && row[index] < paletteAlpha.Length ? paletteAlpha[row[index]] : (byte)255;
                    if (palette == null || p + 2 >= palette.Length)
                    {
                        return new Color32(0, 0, 0, alpha);
                    }

                    return new Color32(palette[p], palette[p + 1], palette[p + 2], alpha);
                }
                case 4:
                    return new Color32(row[index], row[index], row[index], row[index + 1]);
                default:
                    return new Color32(row[index], row[index + 1], row[index + 2], row[index + 3]);
            }
        }

        private static void Unfilter(int filter, byte[] current, byte[] previous, int bpp)
        {
            switch (filter)
            {
                case 0:
                    return;
                case 1:
                    for (int i = bpp; i < current.Length; i++)
                    {
                        current[i] = (byte)(current[i] + current[i - bpp]);
                    }

                    return;
                case 2:
                    for (int i = 0; i < current.Length; i++)
                    {
                        current[i] = (byte)(current[i] + previous[i]);
                    }

                    return;
                case 3:
                    for (int i = 0; i < current.Length; i++)
                    {
                        int left = i >= bpp ? current[i - bpp] : 0;
                        current[i] = (byte)(current[i] + ((left + previous[i]) >> 1));
                    }

                    return;
                case 4:
                    for (int i = 0; i < current.Length; i++)
                    {
                        int a = i >= bpp ? current[i - bpp] : 0;
                        int b = previous[i];
                        int c = i >= bpp ? previous[i - bpp] : 0;
                        int p = a + b - c;
                        int pa = Math.Abs(p - a);
                        int pb = Math.Abs(p - b);
                        int pc = Math.Abs(p - c);
                        int pred = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                        current[i] = (byte)(current[i] + pred);
                    }

                    return;
            }
        }

        private static byte[] Inflate(byte[] zlib)
        {
            try
            {
                using var input = new MemoryStream(zlib);
                using var stream = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                stream.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int ReadInt32(byte[] bytes, int index)
            => (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
    }
}
