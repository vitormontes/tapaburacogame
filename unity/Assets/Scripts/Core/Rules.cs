// TAPA BURACO — núcleo de regras (C# puro, sem dependência de UnityEngine).
// Portado do protótipo web index.html, mantendo as MESMAS regras.

namespace TapaBuraco.Core
{
    /// <summary>Força da máquina.</summary>
    public enum AiLevel
    {
        /// <summary>Turista: joga no chute.</summary>
        Turista = 0,

        /// <summary>Banhista de Domingo: metade das vezes chuta sem se entregar, metade pensa como o Rato.</summary>
        Banhista = 1,

        /// <summary>Rato de Praia: livro de abertura + busca exata com tempo-limite. Quase não erra.</summary>
        Rato = 2,
    }

    /// <summary>Contra a máquina ou dois humanos no mesmo aparelho.</summary>
    public enum GameMode
    {
        Cpu = 0,
        DoisJogadores = 1,
    }

    /// <summary>Direção de arte.</summary>
    public enum Skin
    {
        /// <summary>Areia de Copacabana — cartaz serigráfico.</summary>
        Praia = 0,

        /// <summary>Papel de Pão — caneta azul em papel pardo.</summary>
        Papel = 1,
    }

    /// <summary>Resultado de um toque num buraco (o <c>clicaBuraco</c> do protótipo).</summary>
    public enum SelectionResult
    {
        /// <summary>Seleção vazia: o buraco virou o começo da linha.</summary>
        Started = 0,

        /// <summary>A linha cresceu até o buraco tocado.</summary>
        Extended = 1,

        /// <summary>Buraco marcado tocado de novo: saiu da ponta ou partiu a linha (fica o lado maior).</summary>
        Removed = 2,

        /// <summary>Fora da linha reta: a seleção recomeçou no buraco tocado.</summary>
        RestartedNotStraight = 3,

        /// <summary>Diagonal a partir de um único buraco: a seleção recomeçou no buraco tocado.</summary>
        RestartedDiagonal = 4,

        /// <summary>Buraco tapado no caminho: a seleção recomeçou no buraco tocado.</summary>
        Blocked = 5,

        /// <summary>Recusado: não é a vez do humano, buraco já tapado ou partida travada.</summary>
        Rejected = 6,
    }

    /// <summary>Constantes compartilhadas do jogo.</summary>
    public static class Rules
    {
        /// <summary>Fileiras cavadas na areia (e colunas: a escada é simétrica).</summary>
        public const int RowCount = 7;

        /// <summary>Buracos totais: 7+6+5+4+3+2+1.</summary>
        public const int HoleCount = 28;

        /// <summary>Trechos em linha reta distintos (lances possíveis no tabuleiro cheio).</summary>
        public const int SegmentCount = 140;

        /// <summary>Quem tapa o último buraco perde (misère).</summary>
        public const bool LastMoveLoses = true;
    }
}
