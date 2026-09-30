using TapaBuraco.Core;
using UnityEngine;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Toca e mistura os sons do jogo — é o papel que o objeto <c>Som</c> do protótipo fazia
    /// com o grafo Web Audio ligado ao <c>AudioContext</c>.
    ///
    /// Correspondência com o original:
    /// <list type="bullet">
    ///   <item><description><c>master</c> (0.9) → <see cref="GameSettings.masterVolume"/> multiplicado em toda voz.</description></item>
    ///   <item><description><c>gSfx</c> → o interruptor <see cref="GameSettings.sfx"/>; lá ele era um ganho fixo em 1 com
    ///   <c>if(!CFG.sfx) return</c> na entrada de cada efeito, então aqui também é liga/desliga seco.</description></item>
    ///   <item><description><c>gAmb</c>/<c>gMus</c> → <see cref="_ambienceGain"/>/<see cref="_musicGain"/>, que
    ///   perseguem o alvo em <see cref="Update"/> com a mesma curva do <c>setTargetAtTime</c>
    ///   (constantes de 0.5 s e 0.4 s, iguais às do protótipo).</description></item>
    /// </list>
    ///
    /// Os clipes vêm todos do <see cref="ProceduralAudio"/> (cache estático), e as gaivotas
    /// são agendadas por tempo acumulado — sem corrotina e sem alocar por quadro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundDirector : MonoBehaviour
    {
        /// <summary>Vozes simultâneas de efeito. Oito cobre o pior caso (rajada de areia + fim de jogo).</summary>
        private const int PoolSize = 8;

        /// <summary>Ganho do barramento de ambiente ligado — o <c>gAmb</c> do protótipo ia a 0.9.</summary>
        private const float AmbienceOn = 0.9f;

        /// <summary>Ganho do barramento de música ligado — o <c>gMus</c> ia a 0.85.</summary>
        private const float MusicOn = 0.85f;

        /// <summary>Constantes de tempo do <c>setTargetAtTime</c> original.</summary>
        private const float AmbienceTau = 0.5f;
        private const float MusicTau = 0.4f;

        /// <summary>Abaixo disso o barramento é considerado mudo e a fonte pode parar.</summary>
        private const float Silence = 0.0015f;

        private GameSettings _settings;

        private AudioSource[] _pool;
        private int _nextVoice;
        private AudioSource _wavesSource;
        private AudioSource _musicSource;

        private float _master = 0.9f;
        private bool _sfxOn = true;
        private bool _ambienceOn;

        private float _ambienceGain;
        private float _musicGain;
        private float _ambienceTarget;
        private float _musicTarget;

        /// <summary>Segundos até a próxima rajada de gaivota (o <c>setTimeout</c> de 7–20 s).</summary>
        private float _gullTimer;

        /// <summary>Cria o diretor de som como filho de <paramref name="parent"/>, já com o pool montado.</summary>
        public static SoundDirector Create(Transform parent)
        {
            GameObject go = new GameObject("SoundDirector");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go.AddComponent<SoundDirector>();
        }

        /// <summary>Guarda as preferências do jogador e já aplica a mixagem.</summary>
        public void Bind(GameSettings settings)
        {
            _settings = settings;
            ApplyMix();
        }

        /// <summary>
        /// Relê <c>sfx</c>/<c>ambience</c>/<c>music</c>/<c>masterVolume</c> e move os barramentos —
        /// mesmo papel do <c>Som.aplicaMix()</c>, inclusive o fade suave (que acontece no
        /// <see cref="Update"/>, porque aqui não há agendamento no relógio do áudio).
        /// </summary>
        public void ApplyMix()
        {
            bool sfx = _settings == null || _settings.sfx;
            bool ambience = _settings != null && _settings.ambience;
            bool music = _settings != null && _settings.music;

            _master = _settings == null ? 0.9f : Mathf.Clamp01(_settings.masterVolume);
            _sfxOn = sfx;
            _ambienceOn = ambience;
            _ambienceTarget = ambience ? AmbienceOn : 0f;
            _musicTarget = music ? MusicOn : 0f;

            // Ligar um barramento acorda a fonte em laço; desligar deixa o fade terminar
            // antes de parar (ver Update), para não cortar a onda no meio.
            if (ambience)
            {
                StartLoop(_wavesSource, ProceduralAudio.Waves());
            }

            if (music)
            {
                StartLoop(_musicSource, ProceduralAudio.Music());
            }

            ApplyVolumes();
        }

        /// <summary>"fshhh" da areia — <c>Som.areia(forca)</c>, com sorteio de variante e de afinação.</summary>
        public void Sand(float strength)
        {
            if (!_sfxOn)
            {
                return;
            }

            AudioSource voice = NextVoice();

            // O protótipo variava o playbackRate do ruído em 0.8–1.3; aqui o mesmo sorteio
            // cai no pitch da fonte, e a variante do clipe cuida da abertura do bandpass.
            voice.pitch = Random.Range(0.8f, 1.3f);
            voice.PlayOneShot(ProceduralAudio.Sand(Random.Range(0, ProceduralAudio.SandVariantCount)),
                _master * Mathf.Clamp(strength, 0.15f, 1.5f));
        }

        /// <summary>Clique de seleção — <c>Som.toque(alt)</c>.</summary>
        public void Tap(bool high)
        {
            if (!_sfxOn)
            {
                return;
            }

            NextVoice().PlayOneShot(ProceduralAudio.Tap(high), _master);
        }

        /// <summary>Recusa (jogada inválida) — <c>Som.erro()</c>.</summary>
        public void Error()
        {
            if (!_sfxOn)
            {
                return;
            }

            NextVoice().PlayOneShot(ProceduralAudio.Error(), _master);
        }

        /// <summary>Arpejo de vitória — <c>Som.vitoria()</c>.</summary>
        public void Victory()
        {
            if (!_sfxOn)
            {
                return;
            }

            NextVoice().PlayOneShot(ProceduralAudio.Victory(), _master);
        }

        /// <summary>Descida de derrota com "caldo" — <c>Som.derrota()</c>.</summary>
        public void Defeat()
        {
            if (!_sfxOn)
            {
                return;
            }

            NextVoice().PlayOneShot(ProceduralAudio.Defeat(), _master);
        }

        private void Awake()
        {
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = NewSource(false);
            }

            _wavesSource = NewSource(true);
            _musicSource = NewSource(true);

            _gullTimer = Random.Range(7f, 20f);
        }

        private void Update()
        {
            // unscaledDeltaTime: o som não deve congelar junto com o tabuleiro em pausa.
            float dt = Time.unscaledDeltaTime;

            bool moved = Fade(ref _ambienceGain, _ambienceTarget, AmbienceTau, dt);
            if (Fade(ref _musicGain, _musicTarget, MusicTau, dt))
            {
                moved = true;
            }

            if (moved)
            {
                ApplyVolumes();
            }

            // Gaivotas: o protótipo reagendava o setTimeout sempre, mas só cantava com o
            // ambiente ligado. Mesmo comportamento, num contador simples.
            _gullTimer -= dt;
            if (_gullTimer <= 0f)
            {
                _gullTimer = Random.Range(7f, 20f);
                if (_ambienceOn && _ambienceGain > Silence)
                {
                    NextVoice().PlayOneShot(ProceduralAudio.Gull(Random.Range(0, ProceduralAudio.GullVariantCount)),
                        _master * _ambienceGain);
                }
            }
        }

        /// <summary>Aproxima <paramref name="value"/> do alvo com a mesma curva do <c>setTargetAtTime</c>.</summary>
        private static bool Fade(ref float value, float target, float tau, float dt)
        {
            float diff = target - value;
            if (diff < 0.0002f && diff > -0.0002f)
            {
                if (value != target)
                {
                    value = target;
                    return true;
                }

                return false;
            }

            value += diff * (1f - Mathf.Exp(-dt / tau));
            return true;
        }

        private AudioSource NewSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D puro: é interface, não cena 3D
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            source.volume = 1f;
            return source;
        }

        /// <summary>Próxima voz do pool, sempre com afinação neutra (a areia altera depois).</summary>
        private AudioSource NextVoice()
        {
            AudioSource voice = _pool[_nextVoice];
            _nextVoice++;
            if (_nextVoice >= _pool.Length)
            {
                _nextVoice = 0;
            }

            voice.pitch = 1f;
            return voice;
        }

        private static void StartLoop(AudioSource source, AudioClip clip)
        {
            if (source == null)
            {
                return;
            }

            if (source.clip != clip)
            {
                source.clip = clip;
            }

            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void ApplyVolumes()
        {
            if (_wavesSource != null)
            {
                _wavesSource.volume = _master * _ambienceGain;
                if (_ambienceGain <= Silence && _wavesSource.isPlaying && !_ambienceOn)
                {
                    // Terminado o fade, parar economiza a mixagem de um laço de 12 s inaudível.
                    _wavesSource.Stop();
                }
            }

            if (_musicSource != null)
            {
                _musicSource.volume = _master * _musicGain;
                if (_musicGain <= Silence && _musicSource.isPlaying && _musicTarget <= 0f)
                {
                    _musicSource.Stop();
                }
            }
        }
    }
}
