using System.Collections.Generic;
using UnityEngine;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Desenha em código as formas da interface (painéis, botões, buracos, grão de impressão).
    /// Motivo: o protótipo faz tudo com CSS/SVG; gerar por código mantém o traço nítido em
    /// qualquer densidade de tela, sem atlas gigante e sem depender de importador.
    ///
    /// Regra de composição: as formas saem BRANCAS com alfa antisserrilhado e são tingidas
    /// pelo <see cref="UnityEngine.UI.Image.color"/> — assim a troca de skin é instantânea.
    /// </summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(32);

        /// <summary>Retângulo arredondado sólido, pronto para 9-slice.</summary>
        public static Sprite RoundedRect(int size = 96, int radius = 20)
        {
            string key = $"rr:{size}:{radius}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    px[y * size + x] = White(Coverage(d));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            int slice = radius + 2;
            return Store(key, tex, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>Contorno grosso de retângulo arredondado (o traço preto do cartaz).</summary>
        public static Sprite RoundedOutline(int size = 96, int radius = 20, int thickness = 6)
        {
            string key = $"ro:{size}:{radius}:{thickness}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float a = Coverage(d) - Coverage(d + thickness);
                    px[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            int slice = radius + thickness + 2;
            return Store(key, tex, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>Disco cheio.</summary>
        public static Sprite Disc(int size = 128)
        {
            string key = $"disc:{size}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt(Sqr(x + 0.5f - half) + Sqr(y + 0.5f - half)) - (half - 1f);
                    px[y * size + x] = White(Coverage(d));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Disco com queda suave — poeira, brilho, respingo.</summary>
        public static Sprite SoftDisc(int size = 128, float power = 2f)
        {
            string key = $"soft:{size}:{power}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt(Sqr(x + 0.5f - half) + Sqr(y + 0.5f - half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), power);
                    px[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Anel de espessura constante.</summary>
        public static Sprite Ring(int size = 128, int thickness = 8)
        {
            string key = $"ring:{size}:{thickness}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt(Sqr(x + 0.5f - half) + Sqr(y + 0.5f - half)) - (half - 1f);
                    float a = Coverage(d) - Coverage(d + thickness);
                    px[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>
        /// Buraco cavado na areia molhada: gradiente radial escuro com sombra interna no topo
        /// e lábio claro embaixo — o mesmo desenho do CSS `.buraco .cava`.
        /// Este sprite já sai COLORIDO (não é tingido).
        /// </summary>
        public static Sprite Cava(int size = 128)
        {
            string key = $"cava:{size}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half;
                    float fy = y + 0.5f - half;
                    float d = Mathf.Sqrt(fx * fx + fy * fy) - (half - 1f);
                    float a = Coverage(d);
                    if (a <= 0f)
                    {
                        px[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Centro do gradiente deslocado para baixo (50% 78% no CSS; Y do texel cresce para cima).
                    float gx = fx / half;
                    float gy = (fy + half * 0.56f) / half;
                    float t = Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy));

                    Color c = Gradient4(t,
                        Palette.AreiaFunda, 0f,
                        Palette.AreiaSombra, 0.38f,
                        Palette.AreiaEscura, 0.72f,
                        Palette.AreiaBase, 1f);

                    // Sombra projetada da borda superior para dentro.
                    float rim = Mathf.Clamp01((fy / half) * 0.9f + 0.15f);
                    c = Color.Lerp(c, Palette.AreiaFunda, rim * 0.45f);

                    // Lábio iluminado na borda de baixo.
                    float lip = Mathf.Clamp01(-fy / half - 0.55f) * 2f;
                    c = Color.Lerp(c, Palette.AreiaClara, Mathf.Clamp01(lip) * 0.35f);

                    px[y * size + x] = ToColor32(c, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>
        /// Montinho de areia fofa por cima do buraco tapado, com a marca da pazinha.
        /// Sprite já colorido, como o <see cref="Cava"/>.
        /// </summary>
        public static Sprite Monte(int size = 128)
        {
            string key = $"monte:{size}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;

            // D1 — contorno de tinta-areia de 2 CSS px. O monte é desenhado em `size` texels e
            // vai para a tela com ~1,12 u de lado; a 128 texels isso dá pouco mais de 3 texels.
            // Escalar com `size` mantém a espessura estável se o sprite for pedido maior.
            float stroke = Mathf.Max(1.5f, size * 0.025f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half;
                    float fy = y + 0.5f - half;
                    float d = Mathf.Sqrt(fx * fx + fy * fy) - (half - 1f);
                    float a = Coverage(d);
                    if (a <= 0f)
                    {
                        px[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Luz vinda de cima à esquerda (38% 32% no CSS).
                    float gx = (fx + half * 0.24f) / half;
                    float gy = (fy - half * 0.36f) / half;
                    float t = Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy));

                    Color c = Gradient4(t,
                        Palette.AreiaClara, 0f,
                        Palette.AreiaMeia, 0.42f,
                        Palette.AreiaMolhada, 0.72f,
                        Palette.MonteBase, 1f);

                    // Marca da pazinha: arco raso na parte de cima do montinho.
                    float mx = fx / (half * 0.52f);
                    float my = (fy - half * 0.18f) / (half * 0.34f);
                    float ring = Mathf.Abs(Mathf.Sqrt(mx * mx + my * my) - 1f);
                    if (ring < 0.12f && fy > -half * 0.1f)
                    {
                        float k = (1f - ring / 0.12f) * 0.35f;
                        c = Color.Lerp(c, Palette.AreiaSombra, k);
                    }

                    // Contorno na borda do monte: faixa de `stroke` texels colada no limite.
                    float edge = Mathf.Clamp01(a - Coverage(d + stroke));
                    if (edge > 0f)
                    {
                        c = Color.Lerp(c, Palette.MonteContorno, edge);
                    }

                    px[y * size + x] = ToColor32(c, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>
        /// Anel irregular, como rabisco de caneta — skin Papel de Pão.
        /// <paramref name="seed"/> muda o contorno para nenhum buraco ficar igual ao outro.
        /// </summary>
        public static Sprite SketchRing(int size = 128, int thickness = 7, int seed = 0)
        {
            string key = $"sk:{size}:{thickness}:{seed}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            // Raio por ângulo: soma de três harmônicas com fase pseudoaleatória estável.
            var rng = new System.Random(seed * 7919 + 13);
            float p1 = (float)rng.NextDouble() * Mathf.PI * 2f;
            float p2 = (float)rng.NextDouble() * Mathf.PI * 2f;
            float p3 = (float)rng.NextDouble() * Mathf.PI * 2f;

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            float baseRadius = half - thickness * 0.5f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half;
                    float fy = y + 0.5f - half;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    float ang = Mathf.Atan2(fy, fx);
                    float wobble =
                        Mathf.Sin(ang * 2f + p1) * 0.045f +
                        Mathf.Sin(ang * 3f + p2) * 0.030f +
                        Mathf.Sin(ang * 5f + p3) * 0.018f;
                    float target = baseRadius * (1f + wobble);
                    float a = Mathf.Clamp01(thickness * 0.5f + 0.5f - Mathf.Abs(r - target));
                    px[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Grão de impressão: ruído branco tileável, usado em multiply por cima de tudo.</summary>
        public static Sprite Grain(int size = 256, int seed = 7)
        {
            string key = $"grain:{size}:{seed}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            var rng = new System.Random(seed);
            Texture2D tex = NewTexture(size, size, true);
            Color32[] px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                // Ruído em 3 oitavas grosseiras: mais parecido com granulado de papel que ruído puro.
                float n = (float)rng.NextDouble();
                float v = 0.55f + (n - 0.5f) * 0.9f;
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                px[i] = new Color32(g, g, g, 255);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Retícula de meio-tom: pontinhos regulares, também tileável.</summary>
        public static Sprite Halftone(int cell = 8, int repeats = 32)
        {
            string key = $"ht:{cell}:{repeats}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            int size = cell * repeats;
            Texture2D tex = NewTexture(size, size, true);
            Color32[] px = new Color32[size * size];
            float radius = cell * 0.22f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = (x % cell) - cell * 0.5f + 0.5f;
                    float cy = (y % cell) - cell * 0.5f + 0.5f;
                    float d = Mathf.Sqrt(cx * cx + cy * cy) - radius;
                    px[y * size + x] = White(Coverage(d));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Listras diagonais — hachura da skin Papel de Pão.</summary>
        public static Sprite Hatch(int size = 64, float angleDegrees = 56f, int spacing = 6, int thickness = 2)
        {
            string key = $"hatch:{size}:{angleDegrees}:{spacing}:{thickness}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, true);
            Color32[] px = new Color32[size * size];
            float rad = angleDegrees * Mathf.Deg2Rad;
            float dx = Mathf.Cos(rad);
            float dy = Mathf.Sin(rad);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float proj = x * dy - y * dx;
                    float m = Mathf.Repeat(proj, spacing);
                    float a = Mathf.Clamp01(thickness - m) + Mathf.Clamp01(thickness - (spacing - m));
                    px[y * size + x] = White(Mathf.Clamp01(a));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>
        /// Onda da pedra portuguesa do calçadão (a moldura do protótipo, <c>--calcada</c>).
        /// Sai como máscara branca da faixa escura: o creme do fundo fica por conta de quem pinta.
        /// Com <paramref name="vertical"/> a onda corre no eixo Y — as tiras laterais da moldura
        /// ladrilham o mesmo desenho sem precisar girar o RectTransform (giro quebraria o tiling).
        /// </summary>
        public static Sprite Calcada(int length = 96, int thickness = 28, bool vertical = false)
        {
            string key = $"calcada:{length}:{thickness}:{(vertical ? 'v' : 'h')}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            int width = vertical ? thickness : length;
            int height = vertical ? length : thickness;
            Texture2D tex = NewTexture(width, height, true);
            Color32[] px = new Color32[width * height];
            float band = thickness * 0.43f;          // espessura da faixa escura (12 de 28 no SVG)
            float swing = thickness * 0.215f;        // amplitude da onda (6 de 28)
            float middle = thickness * 0.285f;       // topo médio da onda (8 de 28)
            for (int along = 0; along < length; along++)
            {
                float top = middle + swing * Mathf.Cos(2f * Mathf.PI * along / length);
                float bottom = top + band;
                for (int across = 0; across < thickness; across++)
                {
                    // Antisserrilhado simples: cobertura do pixel entre as duas bordas da faixa.
                    float a = Mathf.Clamp01(across + 1f - top) * Mathf.Clamp01(bottom - across);
                    int index = vertical ? along * width + across : across * width + along;
                    px[index] = White(Mathf.Clamp01(a));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Vinheta: transparente no meio, escura nas bordas (o <c>#vinheta</c> do CSS).</summary>
        public static Sprite Vignette(int size = 256, float inner = 0.48f)
        {
            string key = $"vig:{size}:{inner}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt(Sqr((x + 0.5f - half) / half) + Sqr((y + 0.5f - half) / half)) * 0.7071f;
                    float t = Mathf.InverseLerp(inner, 1f, Mathf.Clamp01(r));
                    px[y * size + x] = White(t * t);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Faixa de areia com ondulações de vento — fundo da caixa de areia.</summary>
        public static Sprite SandBed(int width = 256, int height = 256)
        {
            string key = $"sand:{width}:{height}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            var rng = new System.Random(4242);
            Texture2D tex = NewTexture(width, height, false);
            Color32[] px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);      // 0 embaixo, 1 em cima
                Color baseColor = Gradient4(1f - v,
                    Palette.Areia, 0f,
                    Palette.Areia, 0.1f,
                    Palette.AreiaMolhada, 0.55f,
                    Palette.AreiaFundo, 1f);

                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);

                    // Ondulações elípticas varridas pelo vento (repeating-radial-gradient do CSS).
                    float ex = (u - 0.5f) / 0.6f;
                    float ey = (v - 0.9f) / 0.13f;
                    float ripple = Mathf.Sqrt(ex * ex + ey * ey);
                    float band = Mathf.Repeat(ripple * 7.5f, 1f);
                    float shade = band < 0.16f ? (0.16f - band) / 0.16f : 0f;

                    Color c = Color.Lerp(baseColor, Palette.AreiaSombra, shade * 0.22f);

                    // Brilho de grão: pontinhos claros esparsos.
                    if (rng.NextDouble() < 0.004)
                    {
                        c = Color.Lerp(c, Color.white, 0.55f);
                    }

                    px[y * width + x] = ToColor32(c, 1f);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Quadrado branco de 4×4 — base para barras, sombras e blocos de cor.</summary>
        public static Sprite Solid()
        {
            const string key = "solid";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(4, 4, false);
            Color32[] px = new Color32[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Triângulo apontando para a esquerda (rabicho do balão de fala).</summary>
        public static Sprite TriangleLeft(int size = 32)
        {
            string key = $"tri:{size}";
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D tex = NewTexture(size, size, false);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = Mathf.Abs(y / (float)(size - 1) - 0.5f) * 2f;
                    float a = Mathf.Clamp01((u - v) * size * 0.5f);
                    px[y * size + x] = White(a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Store(key, tex, Vector4.zero);
        }

        /// <summary>Libera as texturas geradas (chamado ao sair do app / trocar de cena).</summary>
        public static void Clear()
        {
            foreach (KeyValuePair<string, Sprite> entry in Cache)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                Texture texture = entry.Value.texture;
                Object.Destroy(entry.Value);
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }

            Cache.Clear();
        }

        // ------------------------------------------------------------------ utilidades

        private static Texture2D NewTexture(int width, int height, bool tileable)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = $"gen_{width}x{height}",
                filterMode = FilterMode.Bilinear,
                wrapMode = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave,
            };
            return tex;
        }

        private static Sprite Store(string key, Texture2D tex, Vector4 border)
        {
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = key;
            sprite.hideFlags = HideFlags.DontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Distância assinada até um retângulo arredondado centrado na origem.</summary>
        private static float RoundedBoxDistance(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius) - 1f;
            float qy = Mathf.Abs(py) - (halfH - radius) - 1f;
            float outside = Mathf.Sqrt(Sqr(Mathf.Max(qx, 0f)) + Sqr(Mathf.Max(qy, 0f)));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        /// <summary>Converte distância assinada em cobertura antisserrilhada de 1 pixel.</summary>
        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        private static float Sqr(float v) => v * v;

        private static Color32 White(float alpha)
        {
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            return new Color32(255, 255, 255, a);
        }

        private static Color32 ToColor32(Color c, float alpha)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
        }

        /// <summary>Gradiente de 4 paradas.</summary>
        private static Color Gradient4(float t, Color c0, float t0, Color c1, float t1, Color c2, float t2, Color c3, float t3)
        {
            if (t <= t0)
            {
                return c0;
            }

            if (t < t1)
            {
                return Color.Lerp(c0, c1, Mathf.InverseLerp(t0, t1, t));
            }

            if (t < t2)
            {
                return Color.Lerp(c1, c2, Mathf.InverseLerp(t1, t2, t));
            }

            if (t < t3)
            {
                return Color.Lerp(c2, c3, Mathf.InverseLerp(t2, t3, t));
            }

            return c3;
        }
    }
}
