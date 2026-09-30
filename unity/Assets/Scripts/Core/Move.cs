using System;
using System.Collections.Generic;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Um lance: os buracos tapados em UMA fileira. <see cref="RowHoles"/> é a máscara
    /// dentro da fileira (bit 0 = primeiro buraco da fileira).
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int Row;
        public readonly uint RowHoles;

        public Move(int row, uint rowHoles)
        {
            Row = row;
            RowHoles = rowHoles;
        }

        /// <summary>Quantos buracos este lance tapa.</summary>
        public int Count => Board.PopCount(RowHoles);

        public bool IsEmpty => RowHoles == 0u;

        /// <summary>Índices (dentro da fileira) tapados, em ordem crescente.</summary>
        public int WriteIndices(Span<int> into)
        {
            int n = 0;
            for (int i = 0; i < Board.RowLength(Row); i++)
            {
                if ((RowHoles & (1u << i)) != 0u)
                {
                    into[n++] = i;
                }
            }

            return n;
        }

        /// <summary>Lance contíguo a partir de <paramref name="start"/>.</summary>
        public static Move Run(int row, int start, int length)
        {
            uint mask = ((1u << length) - 1u) << start;
            return new Move(row, mask);
        }

        public bool Equals(Move other) => Row == other.Row && RowHoles == other.RowHoles;

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public override int GetHashCode() => (Row * 397) ^ (int)RowHoles;

        public override string ToString() => $"fileira {Row + 1} x{Count} (0b{Convert.ToString(RowHoles, 2)})";
    }

    /// <summary>Geração e validação de lances legais.</summary>
    public static class MoveGenerator
    {
        /// <summary>
        /// Lances legais para a máquina avaliar.
        /// Vizinhos: todo bloco contíguo de buracos abertos.
        /// Livre: um representante por (fileira, quantidade) — a escolha de QUAIS buracos
        /// não muda o valor do jogo, então sorteia-se para dar variedade visual.
        /// </summary>
        public static void Generate(in Board board, Variant variant, List<Move> into, Random rng = null)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            Span<int> open = stackalloc int[Rules.RowCount];

            for (int row = 0; row < Rules.RowCount; row++)
            {
                int len = Board.RowLength(row);
                uint bits = board.RowMask(row);
                if (bits == 0u)
                {
                    continue;
                }

                if (variant == Variant.Vizinhos)
                {
                    for (int start = 0; start < len; start++)
                    {
                        if ((bits & (1u << start)) == 0u)
                        {
                            continue;
                        }

                        for (int end = start; end < len && (bits & (1u << end)) != 0u; end++)
                        {
                            into.Add(Move.Run(row, start, end - start + 1));
                        }
                    }

                    continue;
                }

                int openCount = 0;
                for (int i = 0; i < len; i++)
                {
                    if ((bits & (1u << i)) != 0u)
                    {
                        open[openCount++] = i;
                    }
                }

                for (int k = 1; k <= openCount; k++)
                {
                    if (rng != null)
                    {
                        // Fisher-Yates parcial: k buracos sorteados entre os abertos.
                        for (int i = openCount - 1; i > 0; i--)
                        {
                            int j = rng.Next(i + 1);
                            (open[i], open[j]) = (open[j], open[i]);
                        }
                    }

                    uint mask = 0u;
                    for (int i = 0; i < k; i++)
                    {
                        mask |= 1u << open[i];
                    }

                    into.Add(new Move(row, mask));
                }
            }
        }

        /// <summary>Valida um lance do humano contra o tabuleiro e a variante.</summary>
        public static bool IsLegal(in Board board, Variant variant, in Move move)
        {
            if (move.Row < 0 || move.Row >= Rules.RowCount || move.RowHoles == 0u)
            {
                return false;
            }

            uint open = board.RowMask(move.Row);
            if ((move.RowHoles & ~open) != 0u)
            {
                return false; // tentou tapar buraco já tapado ou fora da fileira
            }

            if (variant != Variant.Vizinhos)
            {
                return true;
            }

            return IsContiguous(move.RowHoles);
        }

        /// <summary>true se a máscara é um bloco único de bits ligados.</summary>
        public static bool IsContiguous(uint mask)
        {
            if (mask == 0u)
            {
                return false;
            }

            uint shifted = mask >> Board.TrailingZeros(mask);
            return (shifted & (shifted + 1u)) == 0u;
        }
    }
}
