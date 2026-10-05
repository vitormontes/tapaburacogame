namespace TapaBuraco.Core
{
    /// <summary>
    /// Livro de abertura da máquina — os mesmos LIVRO_ABRE/LIVRO_RESP do protótipo web.
    /// Com 24+ buracos abertos a prova exata pode levar minutos; as duas primeiras decisões
    /// vêm pré-calculadas (busca exaustiva offline). Os índices são de <see cref="Segments"/>:
    /// a ordem dos trechos é contrato deste livro.
    /// </summary>
    public static class OpeningBook
    {
        /// <summary>Lance de abertura no tabuleiro cheio (fileira do topo, buracos 0..5).</summary>
        public const int Opening = 5;

        /// <summary>
        /// Replies[h] = resposta vencedora ao primeiro lance h do adversário;
        /// negativo = não há entrada (o lance h vence ou a prova passou do limite) → busca ao vivo.
        /// </summary>
        public static readonly int[] Replies =
        {
            11, 16, 20, 38, 53, -1, 54, -1, 60, 52, 57, 42, 41, -1, 32, 33, 38, 53, 33, 109,
            101, 57, -1, 3, 123, 4, -1, -1, -1, 9, 52, 23, 14, 15, -1, 131, 137, 4, 3, 89,
            94, 12, 4, 57, 8, 62, -1, 2, -1, -1, 24, 101, 16, 4, 6, 33, 17, 10, -1, 37,
            38, -1, 87, -1, 109, 32, 38, 21, 126, -1, 53, -1, -1, 10, -1, -1, 2, -1, 92, -1,
            87, 45, -1, -1, 97, 100, 113, 123, -1, 39, 128, 122, 126, 116, 115, 108, 109, 113, 123, 33,
            21, 126, 86, 53, -1, 91, 122, 102, 95, 96, 66, 76, 87, 86, 12, 94, 87, 90, 129, 85,
            103, 21, 97, 87, 109, 98, 92, 112, 113, 4, 108, 113, 101, -1, 123, -1, -1, 85, 10, 118,
        };

        /// <summary>Trecho do livro para este tabuleiro, ou -1 se a posição não está no livro.</summary>
        public static int Lookup(uint mask)
        {
            if (mask == Board.FullMask)
            {
                return Opening;
            }

            int h = Segments.IndexOf(Board.FullMask ^ mask);
            return h >= 0 && Replies[h] >= 0 ? Replies[h] : -1;
        }
    }
}
