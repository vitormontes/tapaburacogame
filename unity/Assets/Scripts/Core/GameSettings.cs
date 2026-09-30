using System;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Preferências persistidas (equivalente ao CFG do protótipo web).
    /// Campos públicos e [Serializable] para o JsonUtility do Unity ler/gravar direto.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public GameMode mode = GameMode.Cpu;
        public AiLevel level = AiLevel.Banhista;
        public Variant variant = Variant.Livre;
        public Skin skin = Skin.Praia;

        public bool sfx = true;
        public bool ambience = true;
        public bool music = false;

        /// <summary>Volume geral (0..1), somado aos interruptores acima.</summary>
        public float masterVolume = 0.9f;

        /// <summary>Menos partículas e telas mais calmas — acessibilidade e aparelhos fracos.</summary>
        public bool reducedMotion = false;

        /// <summary>Vibração leve ao tapar.</summary>
        public bool haptics = true;

        /// <summary>Placar acumulado [jogador 1, jogador 2/máquina].</summary>
        public int[] score = { 0, 0 };

        /// <summary>Quem começa a próxima partida (alterna a cada fim).</summary>
        public int starter = 0;

        /// <summary>Quantas partidas já terminaram (usado para dicas e review prompt).</summary>
        public int gamesFinished = 0;

        /// <summary>Guarda o estado de som antes do mudo rápido do HUD.</summary>
        public bool[] mutedBackup = { true, true, false };

        public bool AnySound => sfx || ambience || music;

        public GameSettings Clone()
        {
            return new GameSettings
            {
                mode = mode,
                level = level,
                variant = variant,
                skin = skin,
                sfx = sfx,
                ambience = ambience,
                music = music,
                masterVolume = masterVolume,
                reducedMotion = reducedMotion,
                haptics = haptics,
                score = new[] { score[0], score[1] },
                starter = starter,
                gamesFinished = gamesFinished,
                mutedBackup = new[] { mutedBackup[0], mutedBackup[1], mutedBackup[2] },
            };
        }

        /// <summary>Conserta valores fora de faixa vindos de um save antigo ou corrompido.</summary>
        public void Sanitize()
        {
            if (score == null || score.Length != 2)
            {
                score = new[] { 0, 0 };
            }

            if (mutedBackup == null || mutedBackup.Length != 3)
            {
                mutedBackup = new[] { true, true, false };
            }

            if (score[0] < 0) score[0] = 0;
            if (score[1] < 0) score[1] = 0;
            if (starter != 0 && starter != 1) starter = 0;
            if (gamesFinished < 0) gamesFinished = 0;
            if (masterVolume < 0f) masterVolume = 0f;
            if (masterVolume > 1f) masterVolume = 1f;
            if (!Enum.IsDefined(typeof(GameMode), mode)) mode = GameMode.Cpu;
            if (!Enum.IsDefined(typeof(AiLevel), level)) level = AiLevel.Banhista;
            if (!Enum.IsDefined(typeof(Variant), variant)) variant = Variant.Livre;
            if (!Enum.IsDefined(typeof(Skin), skin)) skin = Skin.Praia;
        }
    }
}
