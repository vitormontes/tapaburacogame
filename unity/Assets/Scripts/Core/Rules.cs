// TAPA BURACO — núcleo de regras (C# puro, sem dependência de UnityEngine).
// Portado do protótipo web index.html, mantendo as MESMAS regras.

namespace TapaBuraco.Core
{
    /// <summary>Variante de lance permitida em uma fileira.</summary>
    public enum Variant
    {
        /// <summary>Livre: quaisquer buracos abertos da fileira escolhida (Nim misère clássico).</summary>
        Livre = 0,

        /// <summary>Vizinhos: só buracos contíguos; tapar no meio parte a fileira em dois pedaços.</summary>
        Vizinhos = 1,
    }

    /// <summary>Força da máquina.</summary>
    public enum AiLevel
    {
        /// <summary>Turista: joga no chute.</summary>
        Turista = 0,

        /// <summary>Banhista de Domingo: acerta o lance vencedor em metade das vezes.</summary>
        Banhista = 1,

        /// <summary>Rato de Praia: estratégia perfeita (misère). Só erra se for obrigado.</summary>
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

    /// <summary>Resultado de uma tentativa de seleção de buraco.</summary>
    public enum SelectionResult
    {
        /// <summary>Buraco entrou na seleção.</summary>
        Added = 0,

        /// <summary>Buraco saiu da seleção.</summary>
        Removed = 1,

        /// <summary>Seleção reiniciada em outra fileira.</summary>
        RowChanged = 2,

        /// <summary>Recusado: na variante Vizinhos o buraco não encosta na seleção.</summary>
        RejectedNotAdjacent = 3,

        /// <summary>Recusado: não é a vez do humano, buraco já tapado ou partida travada.</summary>
        Rejected = 4,
    }

    /// <summary>Constantes compartilhadas do jogo.</summary>
    public static class Rules
    {
        /// <summary>Fileiras cavadas na areia.</summary>
        public const int RowCount = 7;

        /// <summary>Buracos totais: 1+2+3+4+5+6+7.</summary>
        public const int HoleCount = 28;

        /// <summary>Quem tapa o último buraco perde (misère).</summary>
        public const bool LastMoveLoses = true;
    }
}
