using System.Collections.Generic;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Resolve a partida por busca exaustiva memoizada — nada de heurística.
    /// Regra misère: quem tapa o ÚLTIMO buraco perde, então um sucessor vazio nunca é lance vencedor.
    ///
    /// Livre  → pedaços são pilhas de Nim; há fórmula fechada (ver <see cref="MisereNimWins"/>),
    ///          validada contra a busca exaustiva nos testes.
    /// Vizinhos → tapar um bloco contíguo pode PARTIR o pedaço em dois; a busca memoizada cobre isso.
    ///          (O espaço de estados é minúsculo: partições de ≤28 com partes ≤7.)
    /// </summary>
    public sealed class MisereSolver
    {
        private readonly Dictionary<ulong, bool>[] _memo =
        {
            new Dictionary<ulong, bool>(4096),
            new Dictionary<ulong, bool>(4096),
        };

        /// <summary>Desligue para forçar a busca exaustiva também na variante Livre (usado nos testes).</summary>
        public bool UseClosedFormForLivre { get; set; } = true;

        /// <summary>Estados distintos já resolvidos (diagnóstico).</summary>
        public int MemoSize(Variant variant) => _memo[(int)variant].Count;

        /// <summary>true se quem está na vez vence com jogo perfeito.</summary>
        public bool CurrentPlayerWins(Position position, Variant variant)
        {
            if (position.IsEmpty)
            {
                // Tabuleiro vazio: o lance anterior tapou o último buraco e perdeu.
                return true;
            }

            if (variant == Variant.Livre && UseClosedFormForLivre)
            {
                return MisereNimWins(position);
            }

            return Search(position, variant);
        }

        /// <summary>true se quem está na vez vence a partir deste tabuleiro.</summary>
        public bool CurrentPlayerWins(in Board board, Variant variant)
            => CurrentPlayerWins(board.ToPosition(variant), variant);

        /// <summary>
        /// Lance vencedor: deixa o tabuleiro NÃO vazio e o adversário perdendo.
        /// Tapar o último buraco (sucessor vazio) é derrota imediata.
        /// </summary>
        public bool IsWinningMove(in Board board, Variant variant, in Move move)
        {
            Board after = board.Apply(move);
            if (after.IsEmpty)
            {
                return false;
            }

            return !CurrentPlayerWins(after.ToPosition(variant), variant);
        }

        /// <summary>Resolve antecipadamente o tabuleiro cheio (aquece o memo fora do frame crítico).</summary>
        public void Prewarm(Variant variant)
        {
            CurrentPlayerWins(Board.Dug.ToPosition(variant), variant);
        }

        /// <summary>
        /// Fórmula fechada do Nim misère (Bouton): com alguma pilha ≥ 2 vale o XOR normal;
        /// com todas as pilhas de tamanho 1 vence quem enxerga um número PAR de pilhas.
        /// </summary>
        public static bool MisereNimWins(Position position)
        {
            if (position.IsEmpty)
            {
                return true;
            }

            if (position.HasBigPart)
            {
                return position.NimXor != 0;
            }

            return (position.CountOf(1) & 1) == 0;
        }

        private bool Search(Position position, Variant variant)
        {
            Dictionary<ulong, bool> memo = _memo[(int)variant];
            if (memo.TryGetValue(position.Key, out bool cached))
            {
                return cached;
            }

            bool win = false;
            for (int size = 1; size <= Rules.RowCount && !win; size++)
            {
                if (position.CountOf(size) == 0)
                {
                    continue;
                }

                Position rest = position.WithoutPart(size);

                if (variant == Variant.Vizinhos)
                {
                    // Tapa um bloco contíguo: sobram um pedaço à esquerda e outro à direita.
                    for (int left = 0; left < size && !win; left++)
                    {
                        for (int right = 0; left + right < size && !win; right++)
                        {
                            Position next = rest.WithPart(left).WithPart(right);
                            if (next.IsEmpty)
                            {
                                continue; // tapou o último buraco: derrota, não conta como lance vencedor
                            }

                            if (!Search(next, variant))
                            {
                                win = true;
                            }
                        }
                    }
                }
                else
                {
                    // Livre: a fileira encolhe para qualquer tamanho menor.
                    for (int remaining = 0; remaining < size && !win; remaining++)
                    {
                        Position next = rest.WithPart(remaining);
                        if (next.IsEmpty)
                        {
                            continue;
                        }

                        if (!Search(next, variant))
                        {
                            win = true;
                        }
                    }
                }
            }

            memo[position.Key] = win;
            return win;
        }
    }
}
