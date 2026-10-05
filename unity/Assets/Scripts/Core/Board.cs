using System;
using System.Runtime.CompilerServices;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Escada invertida alinhada à esquerda, em UM único bitmask de 28 bits: a fileira do topo
    /// (r = 0) tem 7 buracos e a de baixo tem 1. O buraco (r, c) existe se c &lt; 7 - r, então as
    /// colunas também têm 7, 6 … 1 buracos. Bit ligado = buraco ABERTO (ainda não tapado).
    /// Índice global do buraco (r, c) = <see cref="RowOffset"/>(r) + c — o mesmo do protótipo web.
    /// Struct imutável, zero alocação.
    /// </summary>
    public readonly struct Board : IEquatable<Board>
    {
        /// <summary>Máscara com os 28 buracos abertos.</summary>
        public const uint FullMask = (1u << Rules.HoleCount) - 1u;

        /// <summary>Primeiro índice global de cada fileira: 0, 7, 13, 18, 22, 25, 27.</summary>
        private static readonly int[] Offsets = { 0, 7, 13, 18, 22, 25, 27 };

        /// <summary>Fileira de cada índice global.</summary>
        private static readonly int[] RowOfCell = BuildRowOfCell();

        /// <summary>Espelho pela diagonal, 4 tabelas de 8 bits (o <c>espelho()</c> do protótipo).</summary>
        private static readonly uint[] TransposeBytes = BuildTransposeBytes();

        /// <summary>Bits abertos. Bit n = buraco global n.</summary>
        public readonly uint Mask;

        public Board(uint mask)
        {
            Mask = mask & FullMask;
        }

        /// <summary>Tabuleiro recém-cavado: os 28 buracos abertos.</summary>
        public static Board Dug => new Board(FullMask);

        /// <summary>Primeiro índice global da fileira.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RowOffset(int row) => Offsets[row];

        /// <summary>Buracos cavados na fileira (7 - row).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RowLength(int row) => Rules.RowCount - row;

        /// <summary>Buracos cavados na coluna (7 - column) — a escada é simétrica.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColumnLength(int column) => Rules.RowCount - column;

        /// <summary>true se o buraco (row, column) existe na escada.</summary>
        public static bool Exists(int row, int column)
            => (uint)row < (uint)Rules.RowCount && (uint)column < (uint)RowLength(row);

        /// <summary>Índice global do buraco (row, column).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int row, int column) => Offsets[row] + column;

        /// <summary>Fileira do índice global.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RowOf(int cell) => RowOfCell[cell];

        /// <summary>Coluna do índice global.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColumnOf(int cell) => cell - Offsets[RowOfCell[cell]];

        /// <summary>Espelha uma máscara pela diagonal: (r, c) ↔ (c, r).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Transpose(uint mask)
            => TransposeBytes[mask & 0xFFu]
               | TransposeBytes[256 + ((mask >> 8) & 0xFFu)]
               | TransposeBytes[512 + ((mask >> 16) & 0xFFu)]
               | TransposeBytes[768 + ((mask >> 24) & 0xFFu)];

        /// <summary>true se o buraco continua aberto.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOpen(int row, int column) => (Mask & (1u << (Offsets[row] + column))) != 0u;

        /// <summary>true se o buraco de índice global <paramref name="cell"/> continua aberto.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOpen(int cell) => (Mask & (1u << cell)) != 0u;

        /// <summary>Buracos abertos no tabuleiro inteiro.</summary>
        public int OpenCount => PopCount(Mask);

        /// <summary>Todos os buracos tapados: a partida acabou.</summary>
        public bool IsEmpty => Mask == 0u;

        /// <summary>Aplica um lance: tapa os buracos do trecho. Não valida legalidade.</summary>
        public Board Apply(in Move move) => new Board(Mask & ~move.Mask);

        /// <summary>Tapa um único buraco.</summary>
        public Board Cover(int row, int column) => new Board(Mask & ~(1u << (Offsets[row] + column)));

        public bool Equals(Board other) => Mask == other.Mask;

        public override bool Equals(object obj) => obj is Board other && Equals(other);

        public override int GetHashCode() => (int)Mask;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(40);
            for (int row = 0; row < Rules.RowCount; row++)
            {
                for (int c = 0; c < RowLength(row); c++)
                {
                    sb.Append(IsOpen(row, c) ? 'o' : '.');
                }

                if (row < Rules.RowCount - 1)
                {
                    sb.Append('/');
                }
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value)
        {
            // Unity/Mono e IL2CPP não expõem System.Numerics.BitOperations em todos os perfis: conta na mão.
            value -= (value >> 1) & 0x55555555u;
            value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
            value = (value + (value >> 4)) & 0x0F0F0F0Fu;
            return (int)((value * 0x01010101u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeros(uint value)
        {
            if (value == 0u)
            {
                return 32;
            }

            int n = 0;
            while ((value & 1u) == 0u)
            {
                value >>= 1;
                n++;
            }

            return n;
        }

        private static int[] BuildRowOfCell()
        {
            var rows = new int[Rules.HoleCount];
            for (int row = 0; row < Rules.RowCount; row++)
            {
                for (int c = 0; c < Rules.RowCount - row; c++)
                {
                    rows[Offsets[row] + c] = row;
                }
            }

            return rows;
        }

        private static uint[] BuildTransposeBytes()
        {
            var cellImage = new int[Rules.HoleCount];
            for (int row = 0; row < Rules.RowCount; row++)
            {
                for (int c = 0; c < Rules.RowCount - row; c++)
                {
                    cellImage[Offsets[row] + c] = Offsets[c] + row;
                }
            }

            var table = new uint[4 * 256];
            for (int b = 0; b < 4; b++)
            {
                for (int v = 0; v < 256; v++)
                {
                    uint image = 0u;
                    for (int i = 0; i < 8; i++)
                    {
                        int bit = (b * 8) + i;
                        if (bit < Rules.HoleCount && ((v >> i) & 1) != 0)
                        {
                            image |= 1u << cellImage[bit];
                        }
                    }

                    table[(b * 256) + v] = image;
                }
            }

            return table;
        }
    }
}
