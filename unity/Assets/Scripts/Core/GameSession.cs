using System;
using System.Threading;
using System.Threading.Tasks;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Máquina de estados de uma partida: tabuleiro, vez, seleção do humano, placar e fim de jogo.
    /// Camada 100% lógica — a apresentação (Unity) escuta os eventos e anima.
    /// </summary>
    public sealed class GameSession
    {
        private readonly BeachAi _ai;

        /// <summary>Buracos marcados, em ordem ao longo da linha (índice global crescente).</summary>
        private readonly int[] _sel = new int[Rules.RowCount];
        private readonly int[] _line = new int[Rules.RowCount];
        private int _selCount;

        /// <summary>Pensadas da máquina, em fila: o solver não aceita duas buscas ao mesmo tempo.</summary>
        private Task _thinkChain = Task.CompletedTask;
        private Task<Move> _thinking;
        private CancellationTokenSource _thinkCts;

        public GameSession(GameSettings settings, MisereSolver solver = null, Random rng = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.Sanitize();
            _ai = new BeachAi(solver ?? new MisereSolver(), rng);
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

        /// <summary>Quantos buracos estão marcados.</summary>
        public int SelectedCount => _selCount;

        /// <summary>Máscara global dos buracos marcados.</summary>
        public uint SelectedMask
        {
            get
            {
                uint mask = 0u;
                for (int i = 0; i < _selCount; i++)
                {
                    mask |= 1u << _sel[i];
                }

                return mask;
            }
        }

        /// <summary>k-ésimo buraco marcado (índice global), na ordem da linha.</summary>
        public int SelectedCell(int k) => _sel[k];

        /// <summary>Com 2+ marcados: true se a linha é horizontal (fileira), false se vertical.</summary>
        public bool SelectionIsHorizontal => _selCount < 2 || Board.RowOf(_sel[0]) == Board.RowOf(_sel[1]);

        public bool IsMachineTurn => Settings.mode == GameMode.Cpu && CurrentPlayer == 1;

        /// <summary>A máquina ainda está calculando o lance pedido em <see cref="ThinkMachineMove"/>.</summary>
        public bool IsMachineThinking => _thinking != null && !_thinking.IsCompleted;

        /// <summary>Só o último buraco do tabuleiro continua aberto — quem tapar perde.</summary>
        public bool IsLastHoleOnBoard => Board.OpenCount == 1;

        public bool CanConfirm => !IsOver && !IsBusy && !IsMachineTurn && _selCount > 0;

        public event Action SelectionChanged;

        public event Action TurnChanged;

        /// <summary>Lance confirmado (jogador, lance, tabuleiro resultante).</summary>
        public event Action<int, Move, Board> MoveApplied;

        /// <summary>Fim de partida: quem venceu.</summary>
        public event Action<int> GameEnded;

        /// <summary>Reinicia o tabuleiro mantendo placar e alternando quem começa.</summary>
        public void NewGame()
        {
            CancelMachineThinking();
            Board = Board.Dug;
            IsOver = false;
            IsBusy = false;
            Winner = -1;
            _selCount = 0;
            CurrentPlayer = Settings.starter;
            SelectionChanged?.Invoke();
            TurnChanged?.Invoke();
        }

        /// <summary>
        /// Toque do humano em um buraco — o <c>clicaBuraco</c> do protótipo. A seleção é sempre
        /// um trecho reto e contínuo de buracos abertos; tocar no começo e no fim marca tudo.
        /// </summary>
        public SelectionResult ToggleHole(int row, int column)
        {
            if (IsOver || IsBusy || IsMachineTurn || !Board.Exists(row, column) || !Board.IsOpen(row, column))
            {
                return SelectionResult.Rejected;
            }

            int k = Board.Index(row, column);
            if (_selCount == 0)
            {
                SelectOnly(k);
                return SelectionResult.Started;
            }

            int pos = Array.IndexOf(_sel, k, 0, _selCount);
            if (pos >= 0)
            {
                // Desmarcar: na ponta encurta; no meio fica o pedaço maior (a linha não pode ter buraco).
                if (pos == _selCount - 1)
                {
                    _selCount--;
                }
                else if (pos == 0)
                {
                    Array.Copy(_sel, 1, _sel, 0, _selCount - 1);
                    _selCount--;
                }
                else
                {
                    int left = pos;
                    int right = _selCount - pos - 1;
                    if (left >= right)
                    {
                        _selCount = left;
                    }
                    else
                    {
                        Array.Copy(_sel, pos + 1, _sel, 0, right);
                        _selCount = right;
                    }
                }

                SelectionChanged?.Invoke();
                return SelectionResult.Removed;
            }

            // Estender: a nova linha vai da ponta mais distante até o buraco tocado.
            int first = _sel[0];
            int length = LineBetween(Math.Min(first, k), Math.Max(_sel[_selCount - 1], k), _line);
            // A linha tem de conter o buraco tocado E a seleção inteira.
            bool aligned = length > 0 && Array.IndexOf(_line, k, 0, length) >= 0;
            for (int i = 0; aligned && i < _selCount; i++)
            {
                aligned = Array.IndexOf(_line, _sel[i], 0, length) >= 0;
            }

            if (!aligned)
            {
                bool diagonal = _selCount == 1
                    && Math.Abs(Board.RowOf(first) - row) == Math.Abs(Board.ColumnOf(first) - column);
                SelectOnly(k);
                return diagonal ? SelectionResult.RestartedDiagonal : SelectionResult.RestartedNotStraight;
            }

            for (int i = 0; i < length; i++)
            {
                if (!Board.IsOpen(_line[i]))
                {
                    SelectOnly(k);
                    return SelectionResult.Blocked;
                }
            }

            Array.Copy(_line, _sel, length);
            _selCount = length;
            SelectionChanged?.Invoke();
            return SelectionResult.Extended;
        }

        /// <summary>Botão "Limpar seleção": limpa a marcação.</summary>
        public void ClearSelection()
        {
            if (_selCount == 0)
            {
                return;
            }

            _selCount = 0;
            SelectionChanged?.Invoke();
        }

        /// <summary>Lance montado a partir da marcação atual (sempre um trecho válido quando há marcação).</summary>
        public bool TryBuildSelectedMove(out Move move)
        {
            move = default;
            return _selCount > 0 && Move.TryFromMask(SelectedMask, out move);
        }

        /// <summary>Confirma a marcação (pazinha "TAPAR n BURACOS"). Devolve false se o lance for ilegal.</summary>
        public bool ConfirmSelection()
        {
            return CanConfirm && TryBuildSelectedMove(out Move move) && Apply(move);
        }

        /// <summary>
        /// Pede o lance da máquina para o tabuleiro atual, calculado fora da thread que chamou.
        /// A busca do Rato pode levar segundos; quem chamou acompanha a <see cref="Task"/> sem travar.
        /// Uma pensada nova cancela a anterior.
        /// </summary>
        public Task<Move> ThinkMachineMove()
        {
            CancelMachineThinking();
            var cts = new CancellationTokenSource();
            _thinkCts = cts;

            Board board = Board;
            AiLevel level = Settings.level;
            Task<Move> task = _thinkChain.ContinueWith(
                _ => _ai.Choose(board, level, BeachAi.ThinkMilliseconds, cts.Token),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            _thinkChain = task;
            _thinking = task;
            return task;
        }

        /// <summary>Interrompe a pensada em curso (troca de partida, volta ao menu).</summary>
        public void CancelMachineThinking()
        {
            if (_thinkCts != null)
            {
                _thinkCts.Cancel();
                _thinkCts = null;
            }

            _thinking = null;
        }

        /// <summary>Aplica um lance legal, encerra a partida se foi o último buraco e passa a vez.</summary>
        public bool Apply(in Move move)
        {
            if (IsOver || !MoveGenerator.IsLegal(Board, move))
            {
                return false;
            }

            int player = CurrentPlayer;
            Board = Board.Apply(move);
            _selCount = 0;
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

        /// <summary>
        /// Buracos da linha reta entre os índices <paramref name="a"/> ≤ <paramref name="b"/>
        /// (inclusive), em ordem crescente; -1 se não dividem fileira nem coluna.
        /// </summary>
        public static int LineBetween(int a, int b, int[] into)
        {
            int ra = Board.RowOf(a), ca = Board.ColumnOf(a);
            int rb = Board.RowOf(b), cb = Board.ColumnOf(b);
            int n = 0;
            if (ra == rb)
            {
                for (int c = Math.Min(ca, cb); c <= Math.Max(ca, cb); c++)
                {
                    into[n++] = Board.Index(ra, c);
                }

                return n;
            }

            if (ca == cb)
            {
                for (int r = Math.Min(ra, rb); r <= Math.Max(ra, rb); r++)
                {
                    into[n++] = Board.Index(r, ca);
                }

                return n;
            }

            return -1;
        }

        private void SelectOnly(int cell)
        {
            _sel[0] = cell;
            _selCount = 1;
            SelectionChanged?.Invoke();
        }

        private void EndGame(int winner)
        {
            IsOver = true;
            Winner = winner;
            Settings.score[winner]++;
            Settings.gamesFinished++;
            Settings.starter = 1 - Settings.starter; // alterna quem começa
            GameEnded?.Invoke(winner);
        }
    }
}
