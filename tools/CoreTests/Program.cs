using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TapaBuraco.Core;

namespace TapaBuraco.CoreTests
{
    /// <summary>
    /// Verificação headless do núcleo de regras — roda sem o editor Unity.
    /// Cruza trechos, gerador de lances e solver contra reimplementações ingênuas e
    /// independentes, confere a seleção por toque, a máquina e o livro de abertura.
    /// </summary>
    internal static class Program
    {
        /// <summary>Teto de tempo para provar as entradas do livro (o resto do teste é rápido).</summary>
        private const int BookProofBudgetMs = 60000;

        /// <summary>Teto de cada prova individual do livro.</summary>
        private const int BookProofPerEntryMs = 3000;

        private static int _failures;
        private static int _checks;

        /// <summary>Um solver para o teste todo: a tabela de 20 MB é alocada uma vez só.</summary>
        private static readonly MisereSolver Solver = new MisereSolver();

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var sw = Stopwatch.StartNew();

            TestBitLayout();
            TestSegmentContract();
            TestMoveGeneration();
            TestBlockedLines();
            TestSelectionRules();
            TestMisereTerminal();
            TestSolverAgainstBruteForce();
            TestTransposeSymmetry();
            TestMachineNeverGivesAway();
            TestRatoConvertsWins();
            TestMachineThinkingOffThread();
            TestGameAlwaysEnds();
            TestOpeningBook();

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine($"{_checks} verificações em {sw.ElapsedMilliseconds} ms — {_failures} falha(s).");
            return _failures == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- infra

        private static void Check(bool condition, string what)
        {
            _checks++;
            if (!condition)
            {
                _failures++;
                Console.WriteLine($"  FALHOU: {what}");
            }
        }

        private static void Section(string title) => Console.WriteLine($"[{title}]");

        // ---------------------------------------------------------------- modelo ingênuo

        /// <summary>O buraco (r, c) existe na escada invertida?</summary>
        private static bool Exists(int r, int c) => r >= 0 && c >= 0 && c < 7 - r;

        /// <summary>Índice global pela soma direta dos tamanhos das fileiras de cima.</summary>
        private static int NaiveIndex(int r, int c)
        {
            int index = 0;
            for (int k = 0; k < r; k++)
            {
                index += 7 - k;
            }

            return index + c;
        }

        /// <summary>Linhas de buracos: 7 fileiras e 7 colunas (inclui as de 1 buraco).</summary>
        private static List<List<int>> NaiveLines()
        {
            var lines = new List<List<int>>();
            for (int r = 0; r < 7; r++)
            {
                lines.Add(Enumerable.Range(0, 7 - r).Select(c => NaiveIndex(r, c)).ToList());
            }

            for (int c = 0; c < 7; c++)
            {
                lines.Add(Enumerable.Range(0, 7 - c).Select(r => NaiveIndex(r, c)).ToList());
            }

            return lines;
        }

        /// <summary>Lances legais por definição: caminha cada linha e junta os trechos sem buraco tapado.</summary>
        private static HashSet<uint> NaiveLegal(uint open)
        {
            var result = new HashSet<uint>();
            foreach (List<int> line in NaiveLines())
            {
                for (int a = 0; a < line.Count; a++)
                {
                    uint run = 0u;
                    for (int b = a; b < line.Count && ((open >> line[b]) & 1u) != 0u; b++)
                    {
                        run |= 1u << line[b];
                        result.Add(run);
                    }
                }
            }

            return result;
        }

        /// <summary>Minimax misère ingênuo: memo simples por máscara, sem espelho, sem ordem.</summary>
        private sealed class BruteForce
        {
            private readonly Dictionary<uint, bool> _memo = new Dictionary<uint, bool>();

            public bool Wins(uint open)
            {
                if (open == 0u)
                {
                    return true; // o adversário tapou o último buraco
                }

                if (_memo.TryGetValue(open, out bool known))
                {
                    return known;
                }

                bool wins = false;
                foreach (uint move in NaiveLegal(open))
                {
                    if (!Wins(open & ~move))
                    {
                        wins = true;
                        break;
                    }
                }

                _memo[open] = wins;
                return wins;
            }
        }

        private static uint RandomMask(Random rng, int maxOpen)
        {
            int target = rng.Next(1, maxOpen + 1);
            uint mask = 0u;
            while (Board.PopCount(mask) < target)
            {
                mask |= 1u << rng.Next(Rules.HoleCount);
            }

            return mask;
        }

        private static bool SolverWins(uint mask)
        {
            bool? wins = Solver.CurrentPlayerWins(mask);
            Check(wins.HasValue, "solver sem prazo sempre devolve resposta");
            return wins ?? false;
        }

        // ---------------------------------------------------------------- testes

        private static void TestBitLayout()
        {
            Section("layout de bits da escada");
            int[] offsets = { 0, 7, 13, 18, 22, 25, 27 };
            for (int r = 0; r < Rules.RowCount; r++)
            {
                Check(Board.RowOffset(r) == offsets[r], $"offset da fileira {r} = {offsets[r]}");
                Check(Board.RowLength(r) == 7 - r, $"fileira {r} tem {7 - r} buracos");
                Check(Board.ColumnLength(r) == 7 - r, $"coluna {r} tem {7 - r} buracos");
                for (int c = 0; c < 8; c++)
                {
                    Check(Board.Exists(r, c) == Exists(r, c), $"existência do buraco ({r},{c})");
                    if (!Exists(r, c))
                    {
                        continue;
                    }

                    int index = Board.Index(r, c);
                    Check(index == NaiveIndex(r, c), $"índice de ({r},{c})");
                    Check(Board.RowOf(index) == r && Board.ColumnOf(index) == c, $"índice {index} volta para ({r},{c})");
                    Check(Board.Transpose(1u << index) == 1u << NaiveIndex(c, r), $"espelho leva ({r},{c}) em ({c},{r})");
                }
            }

            Check(Board.Dug.OpenCount == 28 && Board.FullMask == (1u << 28) - 1u, "tabuleiro cavado tem 28 buracos");
            Check(Board.Transpose(Board.FullMask) == Board.FullMask, "espelho do tabuleiro cheio é ele mesmo");

            Board b = Board.Dug.Cover(3, 2);
            Check(!b.IsOpen(3, 2) && b.OpenCount == 27, "tapar um buraco tira exatamente um");

            var rng = new Random(7);
            for (int t = 0; t < 3000; t++)
            {
                uint mask = (uint)rng.Next() & Board.FullMask;
                Check(Board.Transpose(Board.Transpose(mask)) == mask, "espelho é involução");
                Check(Board.PopCount(Board.Transpose(mask)) == Board.PopCount(mask), "espelho preserva a contagem");
            }
        }

        private static void TestSegmentContract()
        {
            Section("140 trechos na ordem do contrato");
            Check(Segments.Count == 140 && Rules.SegmentCount == 140, $"{Segments.Count} trechos");

            // Reimplementação literal do protótipo: fileiras, depois colunas com 2+ buracos.
            var expected = new List<uint>();
            var seen = new HashSet<uint>();
            foreach (List<int> line in NaiveLines().Where((l, i) => i < 7 || l.Count > 1))
            {
                for (int a = 0; a < line.Count; a++)
                {
                    uint m = 0u;
                    for (int b = a; b < line.Count; b++)
                    {
                        m |= 1u << line[b];
                        if (seen.Add(m))
                        {
                            expected.Add(m);
                        }
                    }
                }
            }

            Check(expected.Count == Segments.Count, "modelo ingênuo também tem 140 trechos");
            bool sameOrder = expected.Count == Segments.Count;
            for (int i = 0; sameOrder && i < expected.Count; i++)
            {
                sameOrder = expected[i] == Segments.MaskOf(i);
            }

            Check(sameOrder, "ordem idêntica à do protótipo web");
            Check(Segments.MaskOf(0) == 1u, "trecho 0 = buraco (0,0)");
            Check(Segments.MaskOf(5) == 0x3Fu, "trecho 5 = fileira 0, buracos 0..5 (abertura do livro)");
            Check(Segments.MaskOf(6) == 0x7Fu, "trecho 6 = fileira 0 inteira");
            Check(Segments.MaskOf(28) == 1u << 7, "trecho 28 = buraco (1,0), primeiro da fileira 1");
            Check(Segments.MaskOf(84) == ((1u << 0) | (1u << 7)), "trecho 84 = primeiro vertical: (0,0)-(1,0)");
            Check(Segments.IsHorizontal(83) && !Segments.IsHorizontal(84), "trechos 0..83 horizontais, 84 em diante verticais");

            var all = NaiveLegal(Board.FullMask);
            Check(all.Count == 140 && Enumerable.Range(0, 140).All(i => all.Contains(Segments.MaskOf(i))),
                "trechos = todas as linhas retas contínuas do tabuleiro cheio");

            bool cellsMatch = true;
            for (int i = 0; i < Segments.Count; i++)
            {
                int[] cells = Segments.CellsOf(i);
                uint m = 0u;
                for (int k = 0; k < cells.Length; k++)
                {
                    m |= 1u << cells[k];
                    cellsMatch &= k == 0 || cells[k] > cells[k - 1];
                }

                cellsMatch &= m == Segments.MaskOf(i) && Segments.IndexOf(m) == i;
            }

            Check(cellsMatch, "buracos de cada trecho em ordem crescente, máscara e índice batem");
            Check(Segments.IndexOf(0b101u) == -1, "máscara com buraco no meio não é trecho");

            bool ordered = true;
            for (int k = 1; k < Segments.Count; k++)
            {
                int x = Segments.InSearchOrder(k - 1), y = Segments.InSearchOrder(k);
                int px = Board.PopCount(Segments.MaskOf(x)), py = Board.PopCount(Segments.MaskOf(y));
                ordered &= px > py || (px == py && x < y);
            }

            Check(ordered, "ordem de busca: maiores primeiro, empate pelo índice");
        }

        private static void TestMoveGeneration()
        {
            Section("gerador de lances x força bruta");
            var rng = new Random(23);
            var moves = new List<Move>();
            for (int t = 0; t < 5000; t++)
            {
                uint mask = (uint)rng.Next() & Board.FullMask;
                if (t % 3 == 0)
                {
                    mask = Board.FullMask & ~(uint)rng.Next() & ~(uint)rng.Next(); // tabuleiros mais vazios
                }

                var board = new Board(mask);
                MoveGenerator.Generate(board, moves);
                HashSet<uint> naive = NaiveLegal(mask);
                var generated = new HashSet<uint>(moves.Select(m => m.Mask));
                Check(generated.Count == moves.Count, "sem lance repetido");
                Check(generated.SetEquals(naive), $"lances de {board} conferem com a força bruta");
                Check(moves.All(m => MoveGenerator.IsLegal(board, m)), "todo lance gerado é legal");
            }

            Check(!MoveGenerator.IsLegal(Board.Dug, new Move(-1)), "trecho inexistente é ilegal");
            Check(!MoveGenerator.IsLegal(Board.Dug, new Move(140)), "trecho fora da faixa é ilegal");
        }

        private static void TestBlockedLines()
        {
            Section("buraco tapado bloqueia a linha");
            // Fileira 0: O O O X O O O
            Board row = Board.Dug.Cover(0, 3);
            Check(!Move.TryFromMask(0x7Fu, out Move whole) || !MoveGenerator.IsLegal(row, whole), "fileira inteira não passa pelo X");
            Check(Move.TryFromMask(0x07u, out Move left) && MoveGenerator.IsLegal(row, left), "dá para tapar até o X (0..2)");
            Check(Move.TryFromMask(0x70u, out Move right) && MoveGenerator.IsLegal(row, right), "depois do X é outro trecho (4..6)");
            Check(Move.TryFromMask(0x1Fu, out Move cross) && !MoveGenerator.IsLegal(row, cross), "trecho 0..4 atravessa o X: ilegal");

            // Coluna 0: (0,0) (1,0) [X em (2,0)] (3,0) …
            Board col = Board.Dug.Cover(2, 0);
            uint upper = (1u << Board.Index(0, 0)) | (1u << Board.Index(1, 0));
            uint through = upper | (1u << Board.Index(2, 0)) | (1u << Board.Index(3, 0));
            Check(Move.TryFromMask(upper, out Move up) && MoveGenerator.IsLegal(col, up), "coluna: tapa até o X");
            Check(Move.TryFromMask(through, out Move th) && !MoveGenerator.IsLegal(col, th), "coluna: não atravessa o X");

            var moves = new List<Move>();
            MoveGenerator.Generate(row, moves);
            Check(moves.All(m => (m.Mask & (1u << 3)) == 0u), "nenhum lance gerado toca o buraco tapado");
            Check(moves.Count == NaiveLegal(row.Mask).Count, "quantidade de lances após o X confere");
        }

        private static void TestSelectionRules()
        {
            Section("marcação por toque (clicaBuraco)");
            var settings = new GameSettings { mode = GameMode.DoisJogadores, starter = 0 };
            var s = new GameSession(settings);
            s.NewGame();

            Check(s.ToggleHole(0, 1) == SelectionResult.Started, "primeiro toque começa a linha");
            Check(s.ToggleHole(0, 4) == SelectionResult.Extended, "tocar o fim marca o trecho todo");
            Check(s.SelectedCount == 4 && s.SelectedMask == 0b11110u && s.SelectionIsHorizontal, "4 buracos na horizontal");
            Check(s.ToggleHole(0, 0) == SelectionResult.Extended && s.SelectedCount == 5, "estende para o outro lado");
            Check(s.ToggleHole(0, 4) == SelectionResult.Removed && s.SelectedMask == 0b01111u, "tirar a ponta encurta");
            Check(s.ToggleHole(0, 1) == SelectionResult.Removed && s.SelectedMask == 0b01100u, "tirar o meio deixa o lado maior (direito)");
            s.ClearSelection();
            s.ToggleHole(0, 0);
            s.ToggleHole(0, 4);
            Check(s.ToggleHole(0, 2) == SelectionResult.Removed && s.SelectedMask == 0b00011u, "empate no meio fica o lado esquerdo");

            s.ClearSelection();
            s.ToggleHole(1, 1);
            Check(s.ToggleHole(3, 3) == SelectionResult.RestartedDiagonal, "diagonal a partir de um buraco é recusada");
            Check(s.SelectedCount == 1 && s.SelectedMask == 1u << Board.Index(3, 3), "seleção recomeça no buraco tocado");
            Check(s.ToggleHole(0, 3) == SelectionResult.Extended && !s.SelectionIsHorizontal, "linha vertical (coluna 3)");
            Check(s.SelectedCount == 4, "coluna 3 de (0,3) a (3,3)");
            Check(s.ToggleHole(2, 0) == SelectionResult.RestartedNotStraight, "fora da linha recomeça");
            Check(s.SelectedCount == 1, "recomeçou com 1 buraco");

            // Tocar a coluna da seleção vertical fora dela, mas num buraco de outra coluna
            s.ClearSelection();
            s.ToggleHole(0, 0);
            s.ToggleHole(2, 0);
            Check(s.ToggleHole(1, 1) == SelectionResult.RestartedNotStraight, "buraco entre as pontas, mas fora da coluna, recomeça");

            Check(s.ToggleHole(9, 0) == SelectionResult.Rejected, "fileira inexistente é recusada");
            Check(s.ToggleHole(6, 1) == SelectionResult.Rejected, "buraco fora da escada é recusado");

            // Bloqueio: tapa (0,3) e tenta marcar 0..4
            s.ClearSelection();
            s.ToggleHole(0, 3);
            Check(s.ConfirmSelection(), "confirmar 1 buraco aplica o lance");
            Check(!s.Board.IsOpen(0, 3) && s.CurrentPlayer == 1, "buraco tapado e vez passada");
            Check(s.ToggleHole(0, 3) == SelectionResult.Rejected, "buraco tapado não marca");
            s.ToggleHole(0, 0);
            Check(s.ToggleHole(0, 5) == SelectionResult.Blocked, "trecho com buraco tapado no caminho é recusado");
            Check(s.SelectedCount == 1 && s.SelectedMask == 1u << Board.Index(0, 5), "bloqueio recomeça no buraco tocado");
            Check(s.TryBuildSelectedMove(out Move single) && single.Count == 1, "seleção vira lance válido");

            var cpu = new GameSession(new GameSettings { mode = GameMode.Cpu, starter = 1 });
            cpu.NewGame();
            Check(cpu.ToggleHole(0, 0) == SelectionResult.Rejected, "na vez da máquina o toque é recusado");
            cpu.IsBusy = true;
            Check(!cpu.CanConfirm, "ocupado não confirma");
        }

        private static void TestMisereTerminal()
        {
            Section("misère: quem tapa o último perde");
            Check(SolverWins(0u), "tabuleiro vazio: o outro tapou o último, quem está na vez venceu");
            for (int cell = 0; cell < Rules.HoleCount; cell++)
            {
                Check(!SolverWins(1u << cell), $"só o buraco {cell}: obrigado a tapar, perde");
            }

            uint pair = (1u << Board.Index(0, 0)) | (1u << Board.Index(0, 1));
            Check(SolverWins(pair), "dois buracos lado a lado: tapa um e deixa o último");
            uint apart = (1u << Board.Index(0, 0)) | (1u << Board.Index(1, 1));
            Check(SolverWins(apart), "dois soltos: tapa um, o outro fica com o último");
            uint three = apart | (1u << Board.Index(2, 2));
            Check(!SolverWins(three), "três soltos (diagonal não vale): perde");
            Check(SolverWins(three | (1u << Board.Index(0, 1))), "três soltos + vizinho: vence");

            var s = new GameSession(new GameSettings { mode = GameMode.DoisJogadores, starter = 0 });
            s.NewGame();
            Move.TryFromMask(Board.FullMask & ~(1u << 27), out Move notAMove);
            Check(notAMove.Segment == -1, "27 buracos não formam um trecho");
            int finishedBefore = s.Settings.gamesFinished;
            for (int r = 0; r < Rules.RowCount; r++)
            {
                Move.TryFromMask(RowMask(r), out Move rowMove);
                s.Apply(rowMove);
            }

            Check(s.IsOver && s.Board.IsEmpty, "sete fileiras tapadas acabam a partida");
            Check(s.Winner == 1, "jogador 1 tapou a última fileira (7 lances) e perdeu");
            Check(s.Settings.score[1] == 1 && s.Settings.gamesFinished == finishedBefore + 1, "placar e contador sobem");
            Check(s.Settings.starter == 1, "quem começa alterna");
            Check(!s.Apply(new Move(0)), "partida acabada não aceita lance");
        }

        private static uint RowMask(int r)
        {
            uint m = 0u;
            for (int c = 0; c < Board.RowLength(r); c++)
            {
                m |= 1u << Board.Index(r, c);
            }

            return m;
        }

        private static void TestSolverAgainstBruteForce()
        {
            Section("solver x minimax ingênuo (até 14 buracos abertos)");
            var brute = new BruteForce();
            var rng = new Random(99);
            int compared = 0, wins = 0;
            for (int t = 0; t < 1500; t++)
            {
                uint mask = RandomMask(rng, 14);
                bool expected = brute.Wins(mask);
                Check(SolverWins(mask) == expected, $"posição {new Board(mask)}");
                compared++;
                wins += expected ? 1 : 0;
            }

            Console.WriteLine($"  {compared} posições comparadas ({wins} vencedoras); tabela = {Solver.TableCount} entradas");
            Check(wins > 0 && wins < compared, "amostra tem posições vencedoras e perdedoras");

            // Mesma prova com tabela vazia: a resposta não depende do que já estava guardado.
            Solver.Clear();
            for (int t = 0; t < 200; t++)
            {
                uint mask = RandomMask(rng, 12);
                Check(SolverWins(mask) == brute.Wins(mask), "tabela zerada dá a mesma resposta");
            }
        }

        private static void TestTransposeSymmetry()
        {
            Section("simetria pela diagonal");
            var brute = new BruteForce();
            var rng = new Random(5);
            for (int t = 0; t < 600; t++)
            {
                uint mask = RandomMask(rng, 13);
                uint mirror = Board.Transpose(mask);
                bool expected = brute.Wins(mask);
                Check(brute.Wins(mirror) == expected, "força bruta: posição e espelho têm o mesmo valor");
                Solver.Clear();
                Check(SolverWins(mirror) == expected, "solver no espelho (tabela zerada) = força bruta no original");
            }
        }

        private static void TestMachineNeverGivesAway()
        {
            Section("a máquina não se entrega de graça");
            var ai = new BeachAi(Solver, new Random(3));
            var rng = new Random(31);
            var moves = new List<Move>();
            for (int t = 0; t < 3000; t++)
            {
                uint mask = RandomMask(rng, 16);
                var board = new Board(mask);
                MoveGenerator.Generate(board, moves);
                bool hasSafe = moves.Any(m => m.Mask != mask);

                foreach (AiLevel level in new[] { AiLevel.Turista, AiLevel.Banhista, AiLevel.Rato })
                {
                    Move chosen = ai.Choose(board, level, 200);
                    Check(MoveGenerator.IsLegal(board, chosen), $"{level} escolhe lance legal");
                    if (level != AiLevel.Turista && hasSafe)
                    {
                        Check(chosen.Mask != mask, $"{level} não tapa o último buraco quando há outra saída ({board})");
                    }
                }
            }

            Check(new BeachAi(Solver).Choose(Board.Dug, AiLevel.Rato).Segment == OpeningBook.Opening,
                "Rato abre com o lance do livro (trecho 5)");
            int h = Array.FindIndex(OpeningBook.Replies, r => r >= 0);
            Check(new BeachAi(Solver).Choose(new Board(Board.FullMask ^ Segments.MaskOf(h)), AiLevel.Rato).Segment == OpeningBook.Replies[h],
                "Rato responde pelo livro ao primeiro lance do adversário");
        }

        private static void TestRatoConvertsWins()
        {
            Section("Rato de Praia converte posição vencedora contra o Turista");
            var rng = new Random(77);
            var rato = new BeachAi(Solver, new Random(1));
            var turista = new BeachAi(Solver, new Random(2));
            int games = 0;
            while (games < 300)
            {
                uint mask = RandomMask(rng, 14);
                if (!SolverWins(mask))
                {
                    continue;
                }

                games++;
                var board = new Board(mask);
                int mover = 0; // 0 = Rato
                int lastMover = -1;
                while (!board.IsEmpty)
                {
                    Move move = mover == 0
                        ? rato.Choose(board, AiLevel.Rato, MisereSolver.NoTimeLimit)
                        : turista.Choose(board, AiLevel.Turista);
                    board = board.Apply(move);
                    lastMover = mover;
                    mover = 1 - mover;
                }

                Check(lastMover == 1, $"Rato venceu a partida {games} (posição {new Board(mask)})");
            }
        }

        private static void TestMachineThinkingOffThread()
        {
            Section("pensada da máquina fora da thread e cancelamento");
            var settings = new GameSettings { mode = GameMode.Cpu, level = AiLevel.Rato, starter = 1 };
            var s = new GameSession(settings, Solver, new Random(8));
            s.NewGame();
            var task = s.ThinkMachineMove();
            Check(task.Wait(5000) && task.Result.Segment == OpeningBook.Opening, "tabuleiro cheio: abertura do livro");
            Check(!s.IsMachineThinking, "pensada concluída");
            Check(s.Apply(task.Result) && s.CurrentPlayer == 0, "lance da máquina aplicado, vez do humano");

            // Posição fora do livro, cheia: a busca levaria segundos — cancelar tem de soltar logo.
            int miss = Array.FindIndex(OpeningBook.Replies, r => r < 0);
            var hard = new GameSession(new GameSettings { mode = GameMode.DoisJogadores, level = AiLevel.Rato }, Solver, new Random(9));
            hard.NewGame();
            hard.Apply(new Move(miss));
            Solver.Clear();
            var sw = Stopwatch.StartNew();
            var slow = hard.ThinkMachineMove();
            Thread.Sleep(50);
            Check(hard.IsMachineThinking || slow.IsCompleted, "estado de pensamento exposto");
            hard.CancelMachineThinking();
            Check(!hard.IsMachineThinking, "cancelar limpa o estado de pensamento");
            Check(slow.Wait(2000), $"busca cancelada termina rápido ({sw.ElapsedMilliseconds} ms)");
            Check(MoveGenerator.IsLegal(hard.Board, slow.Result), "mesmo cancelada devolve lance legal");

            // Duas pensadas em fila não se atropelam no solver.
            var a = hard.ThinkMachineMove();
            var b = hard.ThinkMachineMove();
            Check(Task.WaitAll(new Task[] { a, b }, 10000), "pensadas em fila terminam");
            Check(MoveGenerator.IsLegal(hard.Board, b.Result), "segunda pensada devolve lance legal");
        }

        private static void TestGameAlwaysEnds()
        {
            Section("partidas sempre terminam e o último a tapar perde");
            var rng = new Random(4);
            var moves = new List<Move>();
            for (int g = 0; g < 2000; g++)
            {
                var s = new GameSession(new GameSettings { mode = GameMode.DoisJogadores, starter = g & 1 });
                s.NewGame();
                int lastMover = -1;
                int plies = 0;
                while (!s.IsOver && plies < 40)
                {
                    MoveGenerator.Generate(s.Board, moves);
                    lastMover = s.CurrentPlayer;
                    Check(s.Apply(moves[rng.Next(moves.Count)]), "lance sorteado aplicado");
                    plies++;
                }

                Check(s.IsOver && s.Board.IsEmpty, "partida acabou com o tabuleiro vazio");
                Check(s.Winner == 1 - lastMover, "quem tapou o último perdeu");
            }
        }

        private static void TestOpeningBook()
        {
            Section("livro de abertura");
            Check(OpeningBook.Replies.Length == Segments.Count, $"{OpeningBook.Replies.Length} respostas (uma por trecho)");
            Check(MoveGenerator.IsLegal(Board.Dug, new Move(OpeningBook.Opening)), "abertura é legal no tabuleiro cheio");
            Check(OpeningBook.Lookup(Board.FullMask) == OpeningBook.Opening, "consulta no tabuleiro cheio = abertura");

            int entries = 0, proven = 0, timedOut = 0;
            var budget = Stopwatch.StartNew();
            Solver.Clear();
            for (int h = 0; h < Segments.Count; h++)
            {
                int reply = OpeningBook.Replies[h];
                uint after = Board.FullMask ^ Segments.MaskOf(h);
                if (reply < 0)
                {
                    Check(OpeningBook.Lookup(after) == -1, $"sem entrada para o trecho {h}: busca ao vivo");
                    continue;
                }

                entries++;
                Check(reply < Segments.Count && MoveGenerator.IsLegal(new Board(after), new Move(reply)),
                    $"resposta {reply} ao trecho {h} é legal");
                Check(OpeningBook.Lookup(after) == reply, $"consulta do trecho {h} devolve {reply}");

                long left = BookProofBudgetMs - budget.ElapsedMilliseconds;
                if (left <= 0)
                {
                    timedOut++;
                    continue;
                }

                // Teto por entrada: as provas difíceis não comem o tempo das fáceis.
                int limit = (int)Math.Min(left, BookProofPerEntryMs);
                bool? opponentWins = Solver.CurrentPlayerWins(after ^ Segments.MaskOf(reply), limit);
                if (opponentWins == null)
                {
                    timedOut++;
                    continue;
                }

                proven++;
                Check(opponentWins == false, $"resposta {reply} ao trecho {h} deixa o adversário perdido");
            }

            Console.WriteLine($"  {entries} entradas; {proven} provadas perdedoras para o adversário em {budget.ElapsedMilliseconds} ms; {timedOut} sem prova no teto de {BookProofBudgetMs / 1000} s");
        }
    }
}
