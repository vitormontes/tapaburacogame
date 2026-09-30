using System;
using System.Runtime.CompilerServices;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Tabuleiro triangular de 7 fileiras (1..7 buracos, 28 no total) em UM único bitmask de 28 bits.
    /// Bit ligado = buraco ABERTO (ainda não tapado). Struct imutável, zero alocação.
    /// Índice global do buraco (fileira r, coluna i) = RowOffset(r) + i.
    /// </summary>
    public readonly struct Board : IEquatable<Board>
    {
        /// <summary>Máscara com os 28 buracos abertos.</summary>
        public const uint FullMask = (1u << Rules.HoleCount) - 1u;

        /// <summary>Bits abertos. Bit n = buraco global n.</summary>
        public readonly uint Mask;

        public Board(uint mask)
        {
            Mask = mask & FullMask;
        }

        /// <summary>Tabuleiro recém-cavado: os 28 buracos abertos.</summary>
        public static Board Dug => new Board(FullMask);

        /// <summary>Primeiro índice global da fileira (0,1,3,6,10,15,21).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RowOffset(int row) => (row * (row + 1)) >> 1;

        /// <summary>Quantidade de buracos cavados na fileira (row+1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RowLength(int row) => row + 1;

        /// <summary>Máscara de todos os buracos da fileira, alinhada no bit 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RowFullMask(int row) => (1u << (row + 1)) - 1u;

        /// <summary>Buracos AINDA ABERTOS da fileira, alinhados no bit 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint RowMask(int row) => (Mask >> RowOffset(row)) & RowFullMask(row);

        /// <summary>true se o buraco continua aberto.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOpen(int row, int index) => (Mask & (1u << (RowOffset(row) + index))) != 0u;

        /// <summary>Buracos abertos na fileira.</summary>
        public int RowOpenCount(int row) => PopCount(RowMask(row));

        /// <summary>Buracos abertos no tabuleiro inteiro.</summary>
        public int OpenCount => PopCount(Mask);

        /// <summary>Todos os buracos tapados: a partida acabou.</summary>
        public bool IsEmpty => Mask == 0u;

        /// <summary>Aplica um lance: tapa os buracos indicados. Não valida legalidade.</summary>
        public Board Apply(in Move move)
        {
            uint global = move.RowHoles << RowOffset(move.Row);
            return new Board(Mask & ~global);
        }

        /// <summary>Tapa um único buraco.</summary>
        public Board Cover(int row, int index) => new Board(Mask & ~(1u << (RowOffset(row) + index)));

        /// <summary>
        /// Comprimentos dos blocos contíguos de buracos abertos da fileira, em ordem.
        /// Retorna quantos blocos foram escritos em <paramref name="lengths"/> (máx. 4 em 7 buracos).
        /// </summary>
        public int RowSegments(int row, Span<int> lengths)
        {
            uint bits = RowMask(row);
            int len = RowLength(row);
            int count = 0;
            int run = 0;
            for (int i = 0; i < len; i++)
            {
                if ((bits & (1u << i)) != 0)
                {
                    run++;
                }
                else if (run > 0)
                {
                    lengths[count++] = run;
                    run = 0;
                }
            }

            if (run > 0)
            {
                lengths[count++] = run;
            }

            return count;
        }

        /// <summary>Índice do primeiro buraco aberto da fileira, ou -1.</summary>
        public int FirstOpenInRow(int row)
        {
            uint bits = RowMask(row);
            if (bits == 0u)
            {
                return -1;
            }

            return TrailingZeros(bits);
        }

        /// <summary>Posição canônica (multiconjunto de pedaços) usada pelo solver.</summary>
        public Position ToPosition(Variant variant)
        {
            Position position = default;
            Span<int> segments = stackalloc int[4];
            for (int row = 0; row < Rules.RowCount; row++)
            {
                if (variant == Variant.Vizinhos)
                {
                    int n = RowSegments(row, segments);
                    for (int s = 0; s < n; s++)
                    {
                        position = position.WithPart(segments[s]);
                    }
                }
                else
                {
                    int open = RowOpenCount(row);
                    if (open > 0)
                    {
                        position = position.WithPart(open);
                    }
                }
            }

            return position;
        }

        public bool Equals(Board other) => Mask == other.Mask;

        public override bool Equals(object obj) => obj is Board other && Equals(other);

        public override int GetHashCode() => (int)Mask;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(64);
            for (int row = 0; row < Rules.RowCount; row++)
            {
                for (int i = 0; i < RowLength(row); i++)
                {
                    sb.Append(IsOpen(row, i) ? 'o' : '.');
                }

                if (row < Rules.RowCount - 1)
                {
                    sb.Append('/');
                }
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int PopCount(uint value)
        {
            // Unity/Mono e IL2CPP não expõem System.Numerics.BitOperations em todos os perfis: conta na mão.
            value -= (value >> 1) & 0x55555555u;
            value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
            value = (value + (value >> 4)) & 0x0F0F0F0Fu;
            return (int)((value * 0x01010101u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TrailingZeros(uint value)
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
    }
}
