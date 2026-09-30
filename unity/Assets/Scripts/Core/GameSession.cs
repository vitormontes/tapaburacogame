using System;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Máquina de estados de uma partida: tabuleiro, vez, seleção do humano, placar e fim de jogo.
    /// Camada 100% lógica — a apresentação (Unity) escuta os eventos e anima.
    /// </summary>
    public sealed class GameSession
    {
        private readonly MisereSolver _solver;
        private readonly BeachAi _ai;

        public GameSession(GameSettings settings, MisereSolver solver = null, Random rng = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.Sanitize();
            _solver = solver ?? new MisereSolver();
            _ai = new BeachAi(_solver, rng);
        }

        public GameSettings Settings { get; }

        public Board Board { get; private set; } = Board.Dug;

        /// <summary>0 = jogador 1 (humano), 1 = jogador 2 / máquina.</summary>
        public int CurrentPlayer { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Quem venceu a partida atual, ou -1 enquanto ela roda.</summary>
        public int Winner { get; private set; } = -1;

        /// <summary>Travado durante animações: nenhuma entrada é aceita.</summary>
        public bool IsBusy { get; set; }

        public int SelectedRow { get; private set; } = -1;

        /// <summary>Buracos marcados na fileira selecionada (máscara local da fileira).</summary>
        public uint SelectedHoles { get; private set; }

        public int SelectedCount => Board.PopCount(SelectedHoles);

        public bool IsMachineTurn => Settings.mode == GameMode.Cpu && CurrentPlayer == 1;

        /// <summary>Só o último buraco do tabuleiro continua aberto — quem tapar perde.</summary>
        public bool IsLastHoleOnBoard => Board.OpenCount == 1;

        public bool CanConfirm => !IsOver && !IsBusy && !IsMachineTurn && SelectedHoles != 0u;

        public event Action SelectionChanged;

        public event Action TurnChanged;

        /// <summary>Lance confirmado (jogador, lance, tabuleiro resultante).</summary>
        public event Action<int, Move, Board> MoveApplied;

        /// <summary>Fim de partida: quem venceu.</summary>
        public event Action<int> GameEnded;

        /// <summary>Recusas e trocas de fileira, para feedback de som/tela.</summary>
        public event Action<SelectionResult, int, int> SelectionFeedback;

        /// <summary>Reinicia o tabuleiro mantendo placar e alternando quem começa.</summary>
        public void NewGame()
        {
            Board = Board.Dug;
            IsOver = false;
            IsBusy = false;
            Winner = -1;
            SelectedRow = -1;
            SelectedHoles = 0u;
            CurrentPlayer = Settings.starter;
            SelectionChanged?.Invoke();
            TurnChanged?.Invoke();
        }

        /// <summary>Resolve antecipadamente a variante atual (evita travar no primeiro lance da máquina).</summary>
        public void PrewarmSolver() => _solver.Prewarm(Settings.variant);

        /// <summary>Toque do humano em um buraco. Replica exatamente as regras do protótipo.</summary>
        public SelectionResult ToggleHole(int row, int index)
        {
            if (IsOver || IsBusy || IsMachineTurn || row < 0 || row >= Rules.RowCount)
            {
                return Report(SelectionResult.Rejected, row, index);
            }

            if (index < 0 || index >= Board.RowLength(row) || !Board.IsOpen(row, index))
            {
                return Report(SelectionResult.Rejected, row, index);
            }

            uint bit = 1u << index;

            // Tocar em outra fileira limpa a marcação anterior.
            if (SelectedRow != row)
            {
                SelectedRow = row;
                SelectedHoles = bit;
                SelectionChanged?.Invoke();
                return Report(SelectionResult.RowChanged, row, index);
            }

            // Tocar de novo no mesmo buraco desmarca.
            if ((SelectedHoles & bit) != 0u)
            {
                SelectedHoles &= ~bit;
                if (Settings.variant == Variant.Vizinhos && SelectedHoles != 0u)
                {
                    SelectedHoles = LargestContiguousBlock(SelectedHoles);
                }

                if (SelectedHoles == 0u)
                {
                    SelectedRow = -1;
                }

                SelectionChanged?.Invoke();
                return Report(SelectionResult.Removed, row, index);
            }

            if (Settings.variant == Variant.Vizinhos && SelectedHoles != 0u && !TouchesSelection(index))
            {
                // Não encosta: recomeça a marcação nesse buraco (e avisa).
                SelectedHoles = bit;
                SelectionChanged?.Invoke();
                return Report(SelectionResult.RejectedNotAdjacent, row, index);
            }

            SelectedHoles |= bit;
            SelectionChanged?.Invoke();
            return Report(SelectionResult.Added, row, index);
        }

        /// <summary>Botão "Desfazer": limpa a marcação.</summary>
        public void ClearSelection()
        {
            if (SelectedHoles == 0u && SelectedRow < 0)
            {
                return;
            }

            SelectedRow = -1;
            SelectedHoles = 0u;
            SelectionChanged?.Invoke();
        }

        /// <summary>Lance montado a partir da marcação atual.</summary>
        public Move BuildSelectedMove() => new Move(SelectedRow, SelectedHoles);

        /// <summary>Confirma a marcação ("TAPAR"). Devolve false se o lance for ilegal.</summary>
        public bool ConfirmSelection()
        {
            if (!CanConfirm)
            {
                return false;
            }

            return Apply(BuildSelectedMove());
        }

        /// <summary>Lance escolhido pela máquina para a vez atual.</summary>
        public Move ChooseMachineMove() => _ai.Choose(Board, Settings.variant, Settings.level);

        /// <summary>Aplica um lance já validado, encerra a partida se foi o último buraco e passa a vez.</summary>
        public bool Apply(in Move move)
        {
            if (IsOver || !MoveGenerator.IsLegal(Board, Settings.variant, move))
            {
                return false;
            }

            int player = CurrentPlayer;
            Board = Board.Apply(move);
            SelectedRow = -1;
            SelectedHoles = 0u;
            SelectionChanged?.Invoke();
            MoveApplied?.Invoke(player, move, Board);

            if (Board.IsEmpty)
            {
                // Quem tapou o último buraco levou o caldo.
                EndGame(1 - player);
                return true;
            }

            CurrentPlayer = 1 - player;
            TurnChanged?.Invoke();
            return true;
        }

        /// <summary>Quem venceria daqui com jogo perfeito (usado por dicas e telemetria local).</summary>
        public bool CurrentPlayerIsWinning() => _solver.CurrentPlayerWins(Board, Settings.variant);

        private void EndGame(int winner)
        {
            IsOver = true;
            Winner = winner;
            Settings.score[winner]++;
            Settings.gamesFinished++;
            Settings.starter = 1 - Settings.starter; // alterna quem começa
            GameEnded?.Invoke(winner);
        }

        private bool TouchesSelection(int index)
        {
            int min = Board.TrailingZeros(SelectedHoles);
            int max = 31 - LeadingZeros(SelectedHoles);
            return index == min - 1 || index == max + 1;
        }

        /// <summary>Depois de tirar um buraco do meio, fica o maior bloco contíguo.</summary>
        internal static uint LargestContiguousBlock(uint mask)
        {
            uint best = 0u;
            int bestLen = 0;
            int i = 0;
            while (i < 32)
            {
                if ((mask & (1u << i)) == 0u)
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < 32 && (mask & (1u << i)) != 0u)
                {
                    i++;
                }

                int len = i - start;
                if (len > bestLen)
                {
                    bestLen = len;
                    best = ((1u << len) - 1u) << start;
                }
            }

            return best;
        }

        private static int LeadingZeros(uint value)
        {
            if (value == 0u)
            {
                return 32;
            }

            int n = 0;
            while ((value & 0x80000000u) == 0u)
            {
                value <<= 1;
                n++;
            }

            return n;
        }

        private SelectionResult Report(SelectionResult result, int row, int index)
        {
            SelectionFeedback?.Invoke(result, row, index);
            return result;
        }
    }
}
