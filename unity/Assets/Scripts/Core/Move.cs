using System;
using System.Collections.Generic;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Todos os trechos possíveis: buracos contíguos de UMA fileira (horizontal) ou de UMA
    /// coluna (vertical). São 140, e a ORDEM é contrato do livro de abertura e do protótipo web:
    /// fileiras de cima para baixo, depois colunas da esquerda para a direita (só as de 2+
    /// buracos); em cada linha, para cada início a, para cada fim b ≥ a, o trecho a..b entra se
    /// ainda não apareceu (o buraco solto de uma coluna repete o da fileira).
    /// </summary>
    public static class Segments
    {
        /// <summary>Máscara global de cada trecho.</summary>
        private static readonly uint[] Masks;

        /// <summary>Buracos de cada trecho, na ordem da linha.</summary>
        private static readonly int[][] Cells;

        /// <summary>true se o trecho está numa fileira (ou é um buraco solto).</summary>
        private static readonly bool[] Horizontal;

        /// <summary>Ordem de busca: trechos maiores primeiro, empate pelo índice.</summary>
        private static readonly int[] SearchOrder;

        private static readonly Dictionary<uint, int> ByMask;

        static Segments()
        {
            var lines = new List<int[]>(14);
            var horizontalLine = new List<bool>(14);
            for (int r = 0; r < Rules.RowCount; r++)
            {
                var line = new int[Board.RowLength(r)];
                for (int c = 0; c < line.Length; c++)
                {
                    line[c] = Board.Index(r, c);
                }

                lines.Add(line);
                horizontalLine.Add(true);
            }

            for (int c = 0; c < Rules.RowCount; c++)
            {
                int length = Board.ColumnLength(c);
                if (length <= 1)
                {
                    continue;
                }

                var line = new int[length];
                for (int r = 0; r < length; r++)
                {
                    line[r] = Board.Index(r, c);
                }

                lines.Add(line);
                horizontalLine.Add(false);
            }

            var masks = new List<uint>(Rules.SegmentCount);
            var cells = new List<int[]>(Rules.SegmentCount);
            var horizontal = new List<bool>(Rules.SegmentCount);
            ByMask = new Dictionary<uint, int>(Rules.SegmentCount);

            for (int l = 0; l < lines.Count; l++)
            {
                int[] line = lines[l];
                for (int a = 0; a < line.Length; a++)
                {
                    uint mask = 0u;
                    for (int b = a; b < line.Length; b++)
                    {
                        mask |= 1u << line[b];
                        if (ByMask.ContainsKey(mask))
                        {
                            continue;
                        }

                        ByMask.Add(mask, masks.Count);
                        masks.Add(mask);
                        var run = new int[b - a + 1];
                        Array.Copy(line, a, run, 0, run.Length);
                        cells.Add(run);
                        horizontal.Add(horizontalLine[l] || run.Length == 1);
                    }
                }
            }

            Masks = masks.ToArray();
            Cells = cells.ToArray();
            Horizontal = horizontal.ToArray();

            SearchOrder = new int[Masks.Length];
            for (int i = 0; i < SearchOrder.Length; i++)
            {
                SearchOrder[i] = i;
            }

            Array.Sort(SearchOrder, (x, y) =>
            {
                int bySize = Board.PopCount(Masks[y]) - Board.PopCount(Masks[x]);
                return bySize != 0 ? bySize : x - y;
            });
        }

        /// <summary>Quantos trechos existem (140).</summary>
        public static int Count => Masks.Length;

        /// <summary>Máscara global do trecho.</summary>
        public static uint MaskOf(int segment) => Masks[segment];

        /// <summary>Buracos do trecho, na ordem da linha (não alterar o array devolvido).</summary>
        public static int[] CellsOf(int segment) => Cells[segment];

        /// <summary>true se o trecho é horizontal (fileira) ou um buraco solto.</summary>
        public static bool IsHorizontal(int segment) => Horizontal[segment];

        /// <summary>k-ésimo trecho na ordem de busca (maiores primeiro).</summary>
        public static int InSearchOrder(int k) => SearchOrder[k];

        /// <summary>Índice do trecho com esta máscara, ou -1 se a máscara não é um trecho.</summary>
        public static int IndexOf(uint mask) => ByMask.TryGetValue(mask, out int index) ? index : -1;
    }

    /// <summary>Um lance: tapar um trecho em linha reta (índice em <see cref="Segments"/>).</summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int Segment;

        public Move(int segment)
        {
            Segment = segment;
        }

        /// <summary>Buracos tapados pelo lance (máscara global).</summary>
        public uint Mask => Segments.MaskOf(Segment);

        /// <summary>Quantos buracos este lance tapa.</summary>
        public int Count => Segments.CellsOf(Segment).Length;

        /// <summary>true na horizontal (fileira) ou buraco solto; false na vertical.</summary>
        public bool IsHorizontal => Segments.IsHorizontal(Segment);

        /// <summary>Buracos do lance, na ordem da linha (não alterar o array devolvido).</summary>
        public int[] Cells => Segments.CellsOf(Segment);

        /// <summary>Lance que tapa exatamente estes buracos, se eles formam um trecho.</summary>
        public static bool TryFromMask(uint mask, out Move move)
        {
            int index = Segments.IndexOf(mask);
            move = new Move(index);
            return index >= 0;
        }

        public bool Equals(Move other) => Segment == other.Segment;

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public override int GetHashCode() => Segment;

        public override string ToString()
        {
            int[] cells = Cells;
            int first = cells[0];
            return $"trecho {Segment}: ({Board.RowOf(first)},{Board.ColumnOf(first)}) x{cells.Length} {(IsHorizontal ? "horizontal" : "vertical")}";
        }
    }

    /// <summary>Geração e validação de lances legais.</summary>
    public static class MoveGenerator
    {
        /// <summary>Todos os trechos inteiramente abertos, em ordem de índice.</summary>
        public static void Generate(in Board board, List<Move> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            uint open = board.Mask;
            for (int i = 0; i < Segments.Count; i++)
            {
                uint s = Segments.MaskOf(i);
                if ((s & open) == s)
                {
                    into.Add(new Move(i));
                }
            }
        }

        /// <summary>Lance legal: trecho existente com todos os buracos ainda abertos.</summary>
        public static bool IsLegal(in Board board, in Move move)
        {
            if ((uint)move.Segment >= (uint)Segments.Count)
            {
                return false;
            }

            uint s = move.Mask;
            return (s & board.Mask) == s;
        }
    }
}
