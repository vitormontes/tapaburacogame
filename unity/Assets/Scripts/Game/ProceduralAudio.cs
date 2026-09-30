using UnityEngine;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Síntese dos sons do jogo — nenhum arquivo de áudio no projeto, exatamente como o
    /// protótipo web faz com Web Audio ("100% Web Audio, zero arquivos").
    ///
    /// Aqui não existe grafo em tempo real: cada som do objeto <c>Som</c> vira um
    /// <see cref="AudioClip"/> renderizado UMA vez em <c>float[]</c> e guardado em cache
    /// estático. O que no navegador eram nós (<c>BufferSource</c>, <c>BiquadFilter</c>,
    /// <c>Gain</c> com <c>exponentialRampToValueAtTime</c>) vira, neste arquivo,
    /// respectivamente: ruído semeado, <see cref="Biquad"/> por amostra e
    /// <see cref="ExpRamp"/>. Os poucos parâmetros que o protótipo sorteava a cada toque
    /// (ruído da areia, grito da gaivota) viram um punhado de variantes pré-renderizadas —
    /// é o preço de trocar um grafo vivo por clipes, e custa menos CPU no celular.
    ///
    /// A mixagem (barramentos sfx/ambiente/música) mora no <see cref="SoundDirector"/>.
    /// </summary>
    public static class ProceduralAudio
    {
        /// <summary>Quantas versões do "fshhh" existem — o sorteio substitui o ruído vivo do protótipo.</summary>
        public const int SandVariantCount = 4;

        /// <summary>Quantas rajadas de gaivota existem (cada uma já traz 2–3 gritos).</summary>
        public const int GullVariantCount = 4;

        /// <summary>Piso dos envelopes exponenciais — é o mesmo 0.0001 usado no protótipo.</summary>
        private const float Floor = 0.0001f;

        private static int _rate;

        private static readonly AudioClip[] SandClips = new AudioClip[SandVariantCount];
        private static readonly AudioClip[] GullClips = new AudioClip[GullVariantCount];
        private static AudioClip _tapHigh;
        private static AudioClip _tapLow;
        private static AudioClip _error;
        private static AudioClip _victory;
        private static AudioClip _defeat;
        private static AudioClip _waves;
        private static AudioClip _music;

        /// <summary>Formas de onda equivalentes aos <c>OscillatorNode.type</c> do protótipo.</summary>
        private enum Wave
        {
            Sine = 0,
            Triangle = 1,
            Saw = 2,
            Square = 3,
        }

        /// <summary>
        /// Taxa de amostragem usada na renderização. Segue a saída real do Unity para que o
        /// clipe toque sem reamostragem; se o motor de áudio estiver desligado, cai em 44100.
        /// </summary>
        private static int Rate
        {
            get
            {
                if (_rate <= 0)
                {
                    _rate = AudioSettings.outputSampleRate;
                    if (_rate <= 0)
                    {
                        _rate = 44100;
                    }
                }

                return _rate;
            }
        }

        /// <summary>"fshhh" da areia caindo — <c>Som.areia</c>. Cada variante muda o grão e a abertura do bandpass.</summary>
        public static AudioClip Sand(int variant)
        {
            int index = ((variant % SandVariantCount) + SandVariantCount) % SandVariantCount;
            if (SandClips[index] == null)
            {
                SandClips[index] = BuildSand(index);
            }

            return SandClips[index];
        }

        /// <summary>Clique de seleção — <c>Som.toque(alt)</c>: triangular 880 Hz (agudo) ou 620 Hz.</summary>
        public static AudioClip Tap(bool high)
        {
            if (high)
            {
                if (_tapHigh == null)
                {
                    _tapHigh = BuildTap(true);
                }

                return _tapHigh;
            }

            if (_tapLow == null)
            {
                _tapLow = BuildTap(false);
            }

            return _tapLow;
        }

        /// <summary>Recusa — <c>Som.erro</c>: quadrada caindo de 190 para 120 Hz.</summary>
        public static AudioClip Error()
        {
            if (_error == null)
            {
                _error = BuildError();
            }

            return _error;
        }

        /// <summary>Arpejo de vitória — <c>Som.vitoria</c>.</summary>
        public static AudioClip Victory()
        {
            if (_victory == null)
            {
                _victory = BuildVictory();
            }

            return _victory;
        }

        /// <summary>Descida de derrota + "o caldo" — <c>Som.derrota</c>.</summary>
        public static AudioClip Defeat()
        {
            if (_defeat == null)
            {
                _defeat = BuildDefeat();
            }

            return _defeat;
        }

        /// <summary>
        /// Laço de ondas — <c>Som.montaAmbiente</c>. O clipe dura um período inteiro do LFO de
        /// 0.085 Hz, então o vai-e-vem do swell fecha certinho quando o <see cref="AudioSource"/> repete.
        /// </summary>
        public static AudioClip Waves()
        {
            if (_waves == null)
            {
                _waves = BuildWaves();
            }

            return _waves;
        }

        /// <summary>Rajada de gaivota — <c>Som.gaivota</c>: 2 ou 3 gritos espaçados de 0.24 s.</summary>
        public static AudioClip Gull(int variant)
        {
            int index = ((variant % GullVariantCount) + GullVariantCount) % GullVariantCount;
            if (GullClips[index] == null)
            {
                GullClips[index] = BuildGull(index);
            }

            return GullClips[index];
        }

        /// <summary>
        /// Musiquinha — <c>Som.agendaMusica</c>. O protótipo agendava passo a passo com
        /// <c>setInterval</c>; aqui os 32 passos (Am7 → D → Fmaj7 → E7) viram um clipe único
        /// em laço, que é ritmicamente idêntico e não custa nada por quadro.
        /// </summary>
        public static AudioClip Music()
        {
            if (_music == null)
            {
                _music = BuildMusic();
            }

            return _music;
        }

        /// <summary>Descarta o cache (trocas de cena não precisam disso; serve para testes e recarga de domínio).</summary>
        public static void Clear()
        {
            for (int i = 0; i < SandClips.Length; i++)
            {
                Release(ref SandClips[i]);
            }

            for (int i = 0; i < GullClips.Length; i++)
            {
                Release(ref GullClips[i]);
            }

            Release(ref _tapHigh);
            Release(ref _tapLow);
            Release(ref _error);
            Release(ref _victory);
            Release(ref _defeat);
            Release(ref _waves);
            Release(ref _music);
        }

        // ------------------------------------------------------------------
        // Construção de cada som
        // ------------------------------------------------------------------

        /// <summary>
        /// Ruído branco → bandpass varrendo 3600(+variante)→520 Hz (Q 0.7) → highpass 300 Hz,
        /// com envelope 0.035 s de ataque e 0.46 s de queda, mais o "tump" abafado da pazinha.
        /// </summary>
        private static AudioClip BuildSand(int variant)
        {
            int rate = Rate;
            const float dur = 0.5f;
            const float vol = 0.30f;
            const float attack = 0.035f;
            const float release = 0.46f;
            const float sweep = 0.40f;

            float[] data = new float[Mathf.CeilToInt(dur * rate)];
            System.Random rnd = new System.Random(7001 + variant);

            // O protótipo sorteava 3600 + rand*900; cada variante fixa um ponto dessa faixa.
            float start = 3600f + 900f * variant / Mathf.Max(1, SandVariantCount - 1);

            Biquad bp = default;
            Biquad hp = default;
            hp.HighPass(300f, 1f, rate);

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float freq = t < sweep ? ExpRamp(start, 520f, t / sweep) : 520f;
                bp.BandPass(freq, 0.7f, rate);

                float env = t < attack
                    ? ExpRamp(Floor, vol, t / attack)
                    : ExpRamp(vol, Floor, (t - attack) / (release - attack));

                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                data[i] = hp.Process(bp.Process(n)) * env;
            }

            // "tump": senoide 160→60 Hz em 0.12 s, morrendo em 0.16 s.
            Sweep(data, 0f, 160f, 60f, 0.12f, 0.16f, Wave.Sine, 0.18f, 0f, 0.18f);

            return Finish("tb-areia-" + variant, data);
        }

        /// <summary>Triangular curta, ganho 0.14 morrendo em 0.09 s (o clique do protótipo ataca cheio, sem rampa).</summary>
        private static AudioClip BuildTap(bool high)
        {
            float[] data = new float[Mathf.CeilToInt(0.1f * Rate)];
            Note(data, 0f, high ? 880f : 620f, 0.09f, Wave.Triangle, 0.14f, 0f);
            return Finish(high ? "tb-toque-alto" : "tb-toque-baixo", data);
        }

        /// <summary>Quadrada 190→120 Hz com ganho 0.10 morrendo em 0.16 s.</summary>
        private static AudioClip BuildError()
        {
            float[] data = new float[Mathf.CeilToInt(0.17f * Rate)];
            Sweep(data, 0f, 190f, 120f, 0.14f, 0.16f, Wave.Square, 0.10f, 0f, 0.17f);
            return Finish("tb-erro", data);
        }

        /// <summary>Arpejo maior subindo + um harmônico senoidal no fim, como <c>Som.vitoria</c>.</summary>
        private static AudioClip BuildVictory()
        {
            float[] data = new float[Mathf.CeilToInt(1.12f * Rate)];
            Note(data, 0.00f, 523.25f, 0.34f, Wave.Triangle, 0.22f);
            Note(data, 0.12f, 659.25f, 0.34f, Wave.Triangle, 0.22f);
            Note(data, 0.24f, 783.99f, 0.34f, Wave.Triangle, 0.22f);
            Note(data, 0.36f, 1046.5f, 0.34f, Wave.Triangle, 0.22f);
            Note(data, 0.52f, 1318.5f, 0.50f, Wave.Sine, 0.16f);
            return Finish("tb-vitoria", data);
        }

        /// <summary>
        /// Quatro serras descendo e, por cima, "o caldo": ruído num lowpass que abre
        /// 400→2600 Hz e fecha em 300 Hz enquanto o ganho vai a 0.32 e volta a zero.
        /// </summary>
        private static AudioClip BuildDefeat()
        {
            int rate = Rate;
            float[] data = new float[Mathf.CeilToInt(2.0f * rate)];

            Note(data, 0.00f, 523.25f, 0.3f, Wave.Saw, 0.13f);
            Note(data, 0.14f, 466.16f, 0.3f, Wave.Saw, 0.13f);
            Note(data, 0.28f, 392.00f, 0.3f, Wave.Saw, 0.13f);
            Note(data, 0.42f, 311.13f, 0.3f, Wave.Saw, 0.13f);

            System.Random rnd = new System.Random(4242);
            Biquad lp = default;
            int from = Mathf.RoundToInt(0.5f * rate);
            int to = Mathf.Min(data.Length, Mathf.RoundToInt(1.9f * rate));
            for (int i = from; i < to; i++)
            {
                float t = (float)i / rate - 0.5f; // tempo relativo ao início do caldo

                float freq;
                if (t < 0.35f)
                {
                    freq = ExpRamp(400f, 2600f, t / 0.35f);
                }
                else if (t < 1.2f)
                {
                    freq = ExpRamp(2600f, 300f, (t - 0.35f) / 0.85f);
                }
                else
                {
                    freq = 300f;
                }

                lp.LowPass(freq, 1f, rate);

                float env = t < 0.35f
                    ? ExpRamp(Floor, 0.32f, t / 0.35f)
                    : ExpRamp(0.32f, Floor, (t - 0.35f) / 1.05f);

                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                data[i] += lp.Process(n) * env;
            }

            return Finish("tb-derrota", data);
        }

        /// <summary>
        /// Ruído → lowpass 480 Hz (Q 0.6) → ganho com swell (0.22 ± 0.16 a 0.085 Hz) → lowpass 1500 Hz.
        /// O laço dura exatamente um ciclo do LFO e ainda leva uma cauda extra que é
        /// misturada na cabeça (crossfade de potência constante) para não estalar na emenda.
        /// </summary>
        private static AudioClip BuildWaves()
        {
            int rate = Rate;

            // Período do LFO em amostras inteiras; a frequência real é derivada dele para
            // que a fase em i e em i+loop seja idêntica — é o que torna o laço perfeito.
            int loop = Mathf.RoundToInt(rate / 0.085f);
            float lfoHz = (float)rate / loop;
            int tail = Mathf.RoundToInt(0.6f * rate);

            float[] work = new float[loop + tail];
            System.Random rnd = new System.Random(919);

            Biquad lp1 = default;
            Biquad lp2 = default;
            lp1.LowPass(480f, 0.6f, rate);
            lp2.LowPass(1500f, 1f, rate);

            // Aquecimento: sem isso os primeiros milissegundos carregariam o transitório
            // dos filtros partindo do repouso, coisa que o grafo web (sempre ligado) não tem.
            int warm = Mathf.RoundToInt(0.25f * rate);
            for (int i = 0; i < warm; i++)
            {
                lp2.Process(lp1.Process((float)(rnd.NextDouble() * 2.0 - 1.0)));
            }

            float step = Mathf.PI * 2f * lfoHz / rate;
            for (int i = 0; i < work.Length; i++)
            {
                float swell = 0.22f + 0.16f * Mathf.Sin(step * i);
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                work[i] = lp2.Process(lp1.Process(n) * swell);
            }

            float[] data = new float[loop];
            System.Array.Copy(work, data, loop);
            for (int i = 0; i < tail; i++)
            {
                // Dois ruídos descorrelacionados: potência constante (seno/cosseno) mantém o
                // volume do mar estável durante a emenda.
                float u = (float)i / tail * Mathf.PI * 0.5f;
                data[i] = data[i] * Mathf.Sin(u) + work[loop + i] * Mathf.Cos(u);
            }

            return Finish("tb-ondas", data);
        }

        /// <summary>
        /// Rajada de gaivota: 2 ou 3 serras caindo para 55% da nota em 0.17 s, cada uma
        /// filtrada por um bandpass de 1800 Hz e Q 3, espaçadas de 0.24 s.
        /// </summary>
        private static AudioClip BuildGull(int variant)
        {
            int rate = Rate;
            System.Random rnd = new System.Random(3300 + variant);
            int cries = 2 + (variant & 1);

            float[] data = new float[Mathf.CeilToInt(cries * 0.24f * rate)];
            for (int c = 0; c < cries; c++)
            {
                float f0 = 1150f + (float)rnd.NextDouble() * 260f;
                float start = c * 0.24f;

                Biquad bp = default;
                bp.BandPass(1800f, 3f, rate);

                int from = Mathf.RoundToInt(start * rate);
                int to = Mathf.Min(data.Length, from + Mathf.RoundToInt(0.24f * rate));
                float phase = 0f;
                for (int i = from; i < to; i++)
                {
                    float t = (float)(i - from) / rate;
                    float freq = t < 0.17f ? ExpRamp(f0, f0 * 0.55f, t / 0.17f) : f0 * 0.55f;
                    float env = t < 0.03f
                        ? ExpRamp(Floor, 0.09f, t / 0.03f)
                        : ExpRamp(0.09f, Floor, (t - 0.03f) / 0.17f);

                    float dt = freq / rate;
                    data[i] += bp.Process(Sample(Wave.Saw, phase, dt)) * env;
                    phase += dt;
                    if (phase >= 1f)
                    {
                        phase -= 1f;
                    }
                }
            }

            return Finish("tb-gaivota-" + variant, data);
        }

        /// <summary>
        /// Skank reggae a 76 bpm: 32 colcheias, baixo gordo nos passos 0 e 6 de cada compasso,
        /// acorde no contratempo e caixa seca no passo 4 — a mesma grade Am7/D/Fmaj7/E7.
        /// </summary>
        private static AudioClip BuildMusic()
        {
            int rate = Rate;
            const float spb = 60f / 76f;
            const float step = spb * 0.5f;

            // Comprimento em amostras inteiras: o laço precisa cair exatamente na colcheia 33.
            int length = Mathf.RoundToInt(32 * step * rate);
            float[] data = new float[length];

            // Grade harmônica (baixo + tríade), idêntica a Som.grade.
            float[] bass = { 110.00f, 146.83f, 174.61f, 164.81f };
            float[,] chord =
            {
                { 261.63f, 329.63f, 392.00f }, // Am7
                { 293.66f, 369.99f, 440.00f }, // D
                { 261.63f, 349.23f, 440.00f }, // Fmaj7
                { 246.94f, 329.63f, 415.30f }, // E7
            };

            System.Random rnd = new System.Random(76076);

            for (int s = 0; s < 32; s++)
            {
                float t = s * step;
                int comp = (s >> 3) % 4;
                int inBar = s % 8;

                if (inBar == 0 || inBar == 6)
                {
                    Note(data, t, bass[comp], 0.5f, Wave.Sine, 0.22f, 0.03f);
                }

                if ((s & 1) == 1)
                {
                    for (int v = 0; v < 3; v++)
                    {
                        Note(data, t, chord[comp, v], 0.17f, Wave.Triangle, 0.05f, 0.012f);
                    }
                }

                if (inBar == 4)
                {
                    Biquad bp = default;
                    bp.BandPass(2100f, 1.4f, rate);
                    int from = Mathf.RoundToInt(t * rate);
                    int to = Mathf.Min(length, from + Mathf.RoundToInt(0.1f * rate));
                    for (int i = from; i < to; i++)
                    {
                        float u = (float)(i - from) / rate;
                        float env = ExpRamp(0.06f, Floor, u / 0.1f);
                        float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                        data[i] += bp.Process(n) * env;
                    }
                }
            }

            return Finish("tb-musica", data);
        }

        // ------------------------------------------------------------------
        // Blocos de síntese
        // ------------------------------------------------------------------

        /// <summary>
        /// Equivalente ao <c>Som.nota</c>: oscilador de frequência fixa com envelope
        /// exponencial (ataque curto até <paramref name="peak"/> e queda até o piso).
        /// Ataque 0 reproduz o <c>setValueAtTime</c> seco dos cliques.
        /// </summary>
        private static void Note(float[] data, float start, float freq, float dur, Wave wave, float peak, float attack = 0.02f)
        {
            Sweep(data, start, freq, freq, dur, dur, wave, peak, attack, dur);
        }

        /// <summary>
        /// Oscilador com varredura exponencial de frequência e envelope exponencial —
        /// cobre de uma vez os <c>OscillatorNode</c> + <c>GainNode</c> do protótipo.
        /// </summary>
        /// <param name="glide">Tempo da rampa de frequência (depois dela a nota segura <paramref name="toFreq"/>).</param>
        /// <param name="fall">Tempo da queda do ganho, contado a partir do fim do ataque.</param>
        /// <param name="length">Quanto do clipe a voz ocupa.</param>
        private static void Sweep(float[] data, float start, float fromFreq, float toFreq, float glide, float fall,
            Wave wave, float peak, float attack, float length)
        {
            int rate = Rate;
            int from = Mathf.RoundToInt(start * rate);
            if (from < 0)
            {
                from = 0;
            }

            int to = Mathf.Min(data.Length, from + Mathf.RoundToInt(length * rate));
            float phase = 0f;
            float release = Mathf.Max(0.0001f, fall - attack);

            for (int i = from; i < to; i++)
            {
                float t = (float)(i - from) / rate;
                float freq = glide > 0f && t < glide ? ExpRamp(fromFreq, toFreq, t / glide) : toFreq;
                float env = attack > 0f && t < attack
                    ? ExpRamp(Floor, peak, t / attack)
                    : ExpRamp(peak, Floor, (t - attack) / release);

                float dt = freq / rate;
                data[i] += Sample(wave, phase, dt) * env;
                phase += dt;
                if (phase >= 1f)
                {
                    phase -= 1f;
                }
            }
        }

        /// <summary>
        /// Uma amostra da forma de onda. Serra e quadrada levam PolyBLEP para não serrilhar
        /// o agudo — o <c>OscillatorNode</c> do navegador é limitado em banda por definição.
        /// A fase inicial segue a do Web Audio (valor 0 subindo).
        /// </summary>
        private static float Sample(Wave wave, float phase, float dt)
        {
            switch (wave)
            {
                case Wave.Sine:
                    return Mathf.Sin(Mathf.PI * 2f * phase);

                case Wave.Triangle:
                {
                    float p = phase + 0.75f;
                    if (p >= 1f)
                    {
                        p -= 1f;
                    }

                    return 4f * Mathf.Abs(p - 0.5f) - 1f;
                }

                case Wave.Saw:
                {
                    float p = phase + 0.5f;
                    if (p >= 1f)
                    {
                        p -= 1f;
                    }

                    return 2f * p - 1f - Blep(p, dt);
                }

                default:
                {
                    float s = phase < 0.5f ? 1f : -1f;
                    float p = phase + 0.5f;
                    if (p >= 1f)
                    {
                        p -= 1f;
                    }

                    return s + Blep(phase, dt) - Blep(p, dt);
                }
            }
        }

        /// <summary>Correção PolyBLEP de um degrau na fase 0 (descontinuidade de amplitude 2).</summary>
        private static float Blep(float t, float dt)
        {
            if (dt <= 0f)
            {
                return 0f;
            }

            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }

            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }

            return 0f;
        }

        /// <summary>
        /// Rampa exponencial do Web Audio: v(u) = from * (to/from)^u, com u em 0..1.
        /// Serve tanto para ganho quanto para frequência (é o mesmo nó lá).
        /// </summary>
        private static float ExpRamp(float from, float to, float u)
        {
            if (u <= 0f)
            {
                return from;
            }

            if (u >= 1f)
            {
                return to;
            }

            return from * Mathf.Pow(to / from, u);
        }

        /// <summary>Fecha o buffer num <see cref="AudioClip"/> mono, sem streaming e fora do save.</summary>
        private static AudioClip Finish(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i];
                if (v > 1f)
                {
                    v = 1f;
                }
                else if (v < -1f)
                {
                    v = -1f;
                }

                data[i] = v;
            }

            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        /// <summary>Libera um clipe gerado (o modo de edição exige DestroyImmediate).</summary>
        private static void Release(ref AudioClip clip)
        {
            if (clip != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(clip);
                }
                else
                {
                    Object.DestroyImmediate(clip, true);
                }
            }

            clip = null;
        }

        /// <summary>
        /// Biquad RBJ com as mesmas fórmulas do <c>BiquadFilterNode</c>. Detalhe importante:
        /// na especificação Web Audio o Q de lowpass/highpass é em DECIBÉIS
        /// (alfa = sen(w0) / (2 * 10^(Q/20))), enquanto o do bandpass é linear — por isso as
        /// duas entradas são separadas aqui.
        /// </summary>
        private struct Biquad
        {
            private float _b0;
            private float _b1;
            private float _b2;
            private float _a1;
            private float _a2;
            private float _x1;
            private float _x2;
            private float _y1;
            private float _y2;

            public void LowPass(float freq, float qDb, int rate)
            {
                float w0 = Omega(freq, rate);
                float cos = Mathf.Cos(w0);
                float alpha = Mathf.Sin(w0) / (2f * Mathf.Pow(10f, qDb / 20f));
                float b0 = (1f - cos) * 0.5f;
                Set(b0, 1f - cos, b0, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public void HighPass(float freq, float qDb, int rate)
            {
                float w0 = Omega(freq, rate);
                float cos = Mathf.Cos(w0);
                float alpha = Mathf.Sin(w0) / (2f * Mathf.Pow(10f, qDb / 20f));
                float b0 = (1f + cos) * 0.5f;
                Set(b0, -(1f + cos), b0, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public void BandPass(float freq, float q, int rate)
            {
                float w0 = Omega(freq, rate);
                float cos = Mathf.Cos(w0);
                float alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.0001f, q));
                Set(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public float Process(float x)
            {
                float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }

            private static float Omega(float freq, int rate)
            {
                float f = Mathf.Clamp(freq, 10f, rate * 0.49f);
                return Mathf.PI * 2f * f / rate;
            }

            private void Set(float b0, float b1, float b2, float a0, float a1, float a2)
            {
                float inv = 1f / a0;
                _b0 = b0 * inv;
                _b1 = b1 * inv;
                _b2 = b2 * inv;
                _a1 = a1 * inv;
                _a2 = a2 * inv;
            }
        }
    }
}
