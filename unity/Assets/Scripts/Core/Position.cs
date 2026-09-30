using System;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Posição canônica para a teoria de jogos: multiconjunto dos PEDAÇOS em aberto.
    /// Na variante Livre um pedaço é a fileira inteira; na Vizinhos, cada bloco contíguo.
    /// A ordem das fileiras é irrelevante, então guardamos só a contagem de pedaços de
    /// cada tamanho 1..7 (8 bits por tamanho) em um único ulong — chave de memo barata.
    /// </summary>
    public readonly struct Position : IEquatable<Position>
    {
        /// <summary>Contagens empacotadas: bits (tamanho-1)*8 .. +7.</summary>
        public readonly ulong Key;

        public Position(ulong key)
        {
            Key = key;
        }

        public bool IsEmpty => Key == 0ul;

        /// <summary>Quantos pedaços de um tamanho existem.</summary>
        public int CountOf(int size) => (int)((Key >> ((size - 1) * 8)) & 0xFFul);

        /// <summary>Acrescenta um pedaço (tamanho 0 é ignorado: pedaço vazio some).</summary>
        public Position WithPart(int size)
        {
            if (size <= 0)
            {
                return this;
            }

            return new Position(Key + (1ul << ((size - 1) * 8)));
        }

        /// <summary>Remove um pedaço daquele tamanho.</summary>
        public Position WithoutPart(int size)
        {
            if (size <= 0)
            {
                return this;
            }

            return new Position(Key - (1ul << ((size - 1) * 8)));
        }

        /// <summary>Número de pedaços.</summary>
        public int PartCount
        {
            get
            {
                int n = 0;
                for (int size = 1; size <= Rules.RowCount; size++)
                {
                    n += CountOf(size);
                }

                return n;
            }
        }

        /// <summary>Buracos ainda abertos.</summary>
        public int TotalHoles
        {
            get
            {
                int n = 0;
                for (int size = 1; size <= Rules.RowCount; size++)
                {
                    n += CountOf(size) * size;
                }

                return n;
            }
        }

        /// <summary>XOR de Nim dos tamanhos (cada pedaço é uma pilha).</summary>
        public int NimXor
        {
            get
            {
                int x = 0;
                for (int size = 1; size <= Rules.RowCount; size++)
                {
                    if ((CountOf(size) & 1) != 0)
                    {
                        x ^= size;
                    }
                }

                return x;
            }
        }

        /// <summary>true se existe pedaço com 2 ou mais buracos.</summary>
        public bool HasBigPart
        {
            get
            {
                for (int size = 2; size <= Rules.RowCount; size++)
                {
                    if (CountOf(size) > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Escreve os tamanhos dos pedaços; devolve quantos foram escritos.</summary>
        public int WriteParts(Span<int> into)
        {
            int n = 0;
            for (int size = 1; size <= Rules.RowCount; size++)
            {
                int c = CountOf(size);
                for (int i = 0; i < c; i++)
                {
                    into[n++] = size;
                }
            }

            return n;
        }

        public bool Equals(Position other) => Key == other.Key;

        public override bool Equals(object obj) => obj is Position other && Equals(other);

        public override int GetHashCode() => Key.GetHashCode();

        public override string ToString()
        {
            Span<int> parts = stackalloc int[Rules.HoleCount];
            int n = WriteParts(parts);
            if (n == 0)
            {
                return "{}";
            }

            var sb = new System.Text.StringBuilder(2 * n + 2);
            sb.Append('{');
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(parts[i]);
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
