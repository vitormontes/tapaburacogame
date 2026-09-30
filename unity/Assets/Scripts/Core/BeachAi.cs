using System;
using System.Collections.Generic;

namespace TapaBuraco.Core
{
    /// <summary>
    /// A máquina. Três níveis, mesma regra do protótipo web:
    /// Turista sorteia, Rato de Praia joga perfeito, Banhista acerta metade das vezes.
    /// Sem jogo vencedor disponível, evita entregar o último buraco de graça.
    /// </summary>
    public sealed class BeachAi
    {
        private readonly Random _rng;
        private readonly MisereSolver _solver;
        private readonly List<Move> _moves = new List<Move>(96);
        private readonly List<Move> _winning = new List<Move>(96);
        private readonly List<Move> _safe = new List<Move>(96);

        public BeachAi(MisereSolver solver, Random rng = null)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _rng = rng ?? new Random();
        }

        /// <summary>Chance do Banhista enxergar o lance certo.</summary>
        public double BanhistaAccuracy { get; set; } = 0.5;

        /// <summary>Escolhe o lance. Lança se o tabuleiro já estiver vazio.</summary>
        public Move Choose(in Board board, Variant variant, AiLevel level)
        {
            MoveGenerator.Generate(board, variant, _moves, _rng);
            if (_moves.Count == 0)
            {
                throw new InvalidOperationException("Sem lances legais: o tabuleiro já está tapado.");
            }

            if (level == AiLevel.Turista)
            {
                return Pick(_moves);
            }

            _winning.Clear();
            _safe.Clear();
            for (int i = 0; i < _moves.Count; i++)
            {
                Move move = _moves[i];
                Board after = board.Apply(move);
                if (after.IsEmpty)
                {
                    continue; // tapar o último buraco = perder
                }

                _safe.Add(move);
                if (!_solver.CurrentPlayerWins(after.ToPosition(variant), variant))
                {
                    _winning.Add(move);
                }
            }

            if (_winning.Count > 0 && (level == AiLevel.Rato || _rng.NextDouble() < BanhistaAccuracy))
            {
                return Pick(_winning);
            }

            // Perdida a partida no papel: pelo menos não se suicida.
            return _safe.Count > 0 ? Pick(_safe) : Pick(_moves);
        }

        private Move Pick(List<Move> list) => list[_rng.Next(list.Count)];
    }
}
