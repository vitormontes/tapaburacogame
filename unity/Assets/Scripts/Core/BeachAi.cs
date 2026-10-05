using System;
using System.Threading;

namespace TapaBuraco.Core
{
    /// <summary>
    /// A máquina — o <c>lanceDaMaquina()</c> do protótipo web, com os mesmos três níveis:
    /// Turista sorteia qualquer lance; Banhista, metade das vezes, sorteia um lance que não
    /// entrega o último buraco e, na outra metade, pensa como o Rato; Rato de Praia usa o
    /// livro de abertura e depois a busca exata com tempo-limite. Sem vitória provada,
    /// nunca tapa o último buraco de graça (e o Rato tapa o mínimo possível).
    ///
    /// Não é thread-safe: uma escolha por vez (o solver e o sorteio são compartilhados).
    /// </summary>
    public sealed class BeachAi
    {
        /// <summary>Prazo de pensamento da máquina (PRAZO_MS do protótipo).</summary>
        public const int ThinkMilliseconds = 2500;

        private readonly Random _rng;
        private readonly MisereSolver _solver;
        private readonly int[] _legal = new int[Rules.SegmentCount];
        private readonly int[] _pool = new int[Rules.SegmentCount];

        public BeachAi(MisereSolver solver, Random rng = null)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _rng = rng ?? new Random();
        }

        /// <summary>
        /// Escolhe o lance. <paramref name="timeLimitMs"/> limita a busca do Rato
        /// (<see cref="MisereSolver.NoTimeLimit"/> = sem prazo). Lança se o tabuleiro já estiver vazio.
        /// </summary>
        public Move Choose(in Board board, AiLevel level, int timeLimitMs = ThinkMilliseconds, CancellationToken token = default)
        {
            uint mask = board.Mask;
            int legalCount = Legal(mask, _legal);
            if (legalCount == 0)
            {
                throw new InvalidOperationException("Sem lances legais: o tabuleiro já está tapado.");
            }

            if (level == AiLevel.Turista)
            {
                return new Move(_legal[_rng.Next(legalCount)]);
            }

            if (level == AiLevel.Banhista && _rng.NextDouble() < 0.5)
            {
                return new Move(SafeMove(mask, legalCount, false));
            }

            int book = OpeningBook.Lookup(mask);
            if (book >= 0)
            {
                return new Move(book);
            }

            // Ordem embaralhada: entre vários lances vencedores, qualquer um pode sair.
            Array.Copy(_legal, _pool, legalCount);
            for (int i = legalCount - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
            }

            int? winning = _solver.FindWinningMove(mask, _pool, legalCount, timeLimitMs, token);
            if (winning.HasValue && winning.Value >= 0)
            {
                return new Move(winning.Value);
            }

            return new Move(SafeMove(mask, legalCount, level == AiLevel.Rato));
        }

        /// <summary>
        /// Sem vitória conhecida: nunca tapa o último buraco de graça (o <c>semEntregar</c>).
        /// Com <paramref name="shortest"/>, tapa o mínimo e deixa o tabuleiro complicado.
        /// Usa <see cref="_legal"/>, já preenchido com <paramref name="legalCount"/> lances.
        /// </summary>
        private int SafeMove(uint mask, int legalCount, bool shortest)
        {
            int count = 0;
            int minSize = int.MaxValue;
            for (int i = 0; i < legalCount; i++)
            {
                int segment = _legal[i];
                uint s = Segments.MaskOf(segment);
                if (s == mask)
                {
                    continue; // tapar tudo o que resta = perder
                }

                int size = Board.PopCount(s);
                if (shortest && size > minSize)
                {
                    continue;
                }

                if (shortest && size < minSize)
                {
                    minSize = size;
                    count = 0;
                }

                _pool[count++] = segment;
            }

            if (count == 0)
            {
                return _legal[_rng.Next(legalCount)];
            }

            return _pool[_rng.Next(count)];
        }

        private static int Legal(uint mask, int[] into)
        {
            int n = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                uint s = Segments.MaskOf(i);
                if ((s & mask) == s)
                {
                    into[n++] = i;
                }
            }

            return n;
        }
    }
}
