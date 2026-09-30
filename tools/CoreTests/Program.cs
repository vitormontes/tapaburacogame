using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TapaBuraco.Core;

namespace TapaBuraco.CoreTests
{
    /// <summary>
    /// Verificação headless do núcleo de regras — roda sem o editor Unity.
    /// Cruza o solver empacotado contra uma reimplementação ingênua (a mesma do protótipo web)
    /// e joga milhares de partidas para conferir as regras misère.
    /// </summary>
    internal static class Program
    {
        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            var sw = Stopwatch.StartNew();

            TestBitLayout();
            TestSegmentsAndPositions();
            TestMoveGeneration();
            TestSolverAgainstNaive(Variant.Livre);
            TestSolverAgainstNaive(Variant.Vizinhos);
            TestClosedFormMisereNim();
            TestSelectionRules();
            TestPerfectPlay(Variant.Livre);
            TestPerfectPlay(Variant.Vizinhos);
            TestGameAlwaysEnds();
            ReportSolverStats();

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

        // ---------------------------------------------------------------- testes

        private static void TestBitLayout()
        {
            Section("layout de bits");
            Check(Board.Dug.OpenCount == 28, "tabuleiro cavado tem 28 buracos");
            Check(Board.Dug.IsEmpty == false, "tabuleiro cavado não está vazio");

            int total = 0;
            for (int row = 0; row < Rules.RowCount; row++)
            {
                Check(Board.RowLength(row) == row + 1, $"fileira {row} tem {row + 1} buracos");
                Check(Board.RowOffset(row) == total, $"offset da fileira {row}");
                Check(Board.Dug.RowOpenCount(row) == row + 1, $"fileira {row} começa cheia");
                total += row + 1;
            }

            Check(total == Rules.HoleCount, "soma das fileiras = 28");

            // Tapar não pode vazar para fileira vizinha.
            Board b = Board.Dug.Cover(3, 0);
            Check(!b.IsOpen(3, 0), "buraco tapado fica fechado");
            Check(b.OpenCount == 27, "tapar um buraco tira exatamente um");
            for (int row = 0; row < Rules.RowCount; row++)
            {
                int expected = row == 3 ? row : row + 1;
                Check(b.RowOpenCount(row) == expected, $"fileira {row} intacta após tapar (3,0)");
            }

            var rng = new Random(7);
            for (int t = 0; t < 2000; t++)
            {
                uint mask = (uint)rng.Next() & Board.FullMask;
                var board = new Board(mask);
                Check(board.OpenCount == CountBitsNaive(mask), "PopCount confere com contagem ingênua");
                if (mask != 0)
                {
                    Check(Board.TrailingZeros(mask) == TrailingZerosNaive(mask), "TrailingZeros confere");
                }
            }
        }

        private static void TestSegmentsAndPositions()
        {
            Section("segmentos e posição canônica");
            var rng = new Random(11);
            for (int t = 0; t < 3000; t++)
            {
                uint mask = (uint)rng.Next() & Board.FullMask;
                var board = new Board(mask);

                // Modelo ingênuo: lista de listas de bool, igual ao protótipo web.
                var rows = ToNaive(board);

                Span<int> buffer = stackalloc int[4];
                for (int row = 0; row < Rules.RowCount; row++)
                {
                    int n = board.RowSegments(row, buffer);
                    var expected = NaiveSegments(rows[row]);
                    Check(n == expected.Count, $"qtd de blocos da fileira {row}");
                    for (int i = 0; i < n && i < expected.Count; i++)
                    {
                        Check(buffer[i] == expected[i], $"tamanho do bloco {i} da fileira {row}");
                    }
                }

                CheckPositionMatches(board, rows, Variant.Livre);
                CheckPositionMatches(board, rows, Variant.Vizinhos);
            }
        }

        private static void CheckPositionMatches(in Board board, List<bool>[] rows, Variant variant)
        {
            Position packed = board.ToPosition(variant);
            List<int> naive = NaiveParts(rows, variant);
            Span<int> parts = stackalloc int[Rules.HoleCount];
            int n = packed.WriteParts(parts);

            naive.Sort();
            var got = new List<int>();
            for (int i = 0; i < n; i++)
            {
                got.Add(parts[i]);
            }

            got.Sort();
            Check(got.SequenceEqual(naive), $"pedaços canônicos ({variant}) conferem com o modelo ingênuo");
            Check(packed.TotalHoles == board.OpenCount, $"soma dos pedaços = buracos abertos ({variant})");
        }

        private static void TestMoveGeneration()
        {
            Section("geração e validação de lances");
            var rng = new Random(23);
            var moves = new List<Move>();

            for (int t = 0; t < 800; t++)
            {
                uint mask = (uint)rng.Next() & Board.FullMask;
                if (mask == 0)
                {
                    continue;
                }

                var board = new Board(mask);

                foreach (Variant variant in new[] { Variant.Livre, Variant.Vizinhos })
                {
                    MoveGenerator.Generate(board, variant, moves, rng);
                    Check(moves.Count > 0, "há lance legal enquanto sobrar buraco");
                    foreach (Move move in moves)
                    {
                        Check(MoveGenerator.IsLegal(board, variant, move), $"lance gerado é legal ({variant})");
                        Check(move.Count >= 1, "todo lance tapa pelo menos um buraco");
                        Board after = board.Apply(move);
                        Check(after.OpenCount == board.OpenCount - move.Count, "aplicar tapa exatamente os escolhidos");
                        Check((after.Mask & move.RowHoles << Board.RowOffset(move.Row)) == 0u, "buracos do lance ficam fechados");
                    }

                    if (variant == Variant.Vizinhos)
                    {
                        foreach (Move move in moves)
                        {
                            Check(MoveGenerator.IsContiguous(move.RowHoles), "lance da variante Vizinhos é contíguo");
                        }
                    }
                }
            }

            // Ilegalidades explícitas.
            Board dug = Board.Dug;
            Check(!MoveGenerator.IsLegal(dug, Variant.Livre, new Move(0, 0u)), "lance vazio é ilegal");
            Check(!MoveGenerator.IsLegal(dug, Variant.Livre, new Move(0, 0b10u)), "buraco inexistente na fileira 1 é ilegal");
            Check(MoveGenerator.IsLegal(dug, Variant.Livre, new Move(6, 0b1010101u)), "subconjunto solto é legal na Livre");
            Check(!MoveGenerator.IsLegal(dug, Variant.Vizinhos, new Move(6, 0b1010101u)), "subconjunto solto é ilegal na Vizinhos");
            Check(MoveGenerator.IsLegal(dug, Variant.Vizinhos, new Move(6, 0b0011100u)), "bloco contíguo é legal na Vizinhos");
            Board hole = dug.Cover(6, 3);
            Check(!MoveGenerator.IsLegal(hole, Variant.Vizinhos, new Move(6, 0b0011100u)), "bloco atravessando buraco tapado é ilegal");
        }

        private static void TestSolverAgainstNaive(Variant variant)
        {
            Section($"solver memoizado x implementação ingênua ({variant})");
            var solver = new MisereSolver { UseClosedFormForLivre = false };
            var naive = new NaiveSolver(variant);

            int compared = 0;
            foreach (List<int> parts in AllPartMultisets())
            {
                Position position = default;
                foreach (int p in parts)
                {
                    position = position.WithPart(p);
                }

                bool packedWins = solver.CurrentPlayerWins(position, variant);
                bool naiveWins = naive.Wins(parts);
                Check(packedWins == naiveWins, $"veredito diverge em {position}");
                compared++;
            }

            Console.WriteLine($"  {compared} posições comparadas; memo = {solver.MemoSize(variant)} estados");
        }

        private static void TestClosedFormMisereNim()
        {
            Section("fórmula fechada do Nim misère (variante Livre)");
            var brute = new MisereSolver { UseClosedFormForLivre = false };
            int compared = 0;
            foreach (List<int> parts in AllPartMultisets())
            {
                Position position = default;
                foreach (int p in parts)
                {
                    position = position.WithPart(p);
                }

                bool closed = MisereSolver.MisereNimWins(position);
                bool exhaustive = brute.CurrentPlayerWins(position, Variant.Livre);
                Check(closed == exhaustive, $"fórmula fechada erra em {position}");
                compared++;
            }

            Console.WriteLine($"  {compared} posições; fórmula = busca exaustiva");

            // Casos-âncora escritos na mão.
            Check(!MisereSolver.MisereNimWins(new Position().WithPart(1)), "{1}: quem joga perde");
            Check(MisereSolver.MisereNimWins(new Position().WithPart(1).WithPart(1)), "{1,1}: quem joga ganha");
            Check(MisereSolver.MisereNimWins(new Position().WithPart(2)), "{2}: quem joga ganha");
            Check(!MisereSolver.MisereNimWins(new Position().WithPart(2).WithPart(2)), "{2,2}: quem joga perde");
        }

        private static void TestSelectionRules()
        {
            Section("regras de marcação (toque)");

            var settings = new GameSettings { mode = GameMode.DoisJogadores, variant = Variant.Livre };
            var session = new GameSession(settings);
            session.NewGame();

            Check(session.ToggleHole(6, 0) == SelectionResult.RowChanged, "primeiro toque escolhe a fileira");
            Check(session.ToggleHole(6, 2) == SelectionResult.Added, "Livre aceita buraco solto");
            Check(session.SelectedCount == 2, "dois buracos marcados");
            Check(session.ToggleHole(6, 2) == SelectionResult.Removed, "tocar de novo desmarca");
            Check(session.SelectedCount == 1, "sobrou um marcado");
            Check(session.ToggleHole(5, 0) == SelectionResult.RowChanged, "outra fileira reinicia a marcação");
            Check(session.SelectedRow == 5 && session.SelectedCount == 1, "marcação reiniciada na nova fileira");
            session.ClearSelection();
            Check(session.SelectedCount == 0 && session.SelectedRow == -1, "desfazer limpa tudo");

            var vizinhos = new GameSettings { mode = GameMode.DoisJogadores, variant = Variant.Vizinhos };
            var s2 = new GameSession(vizinhos);
            s2.NewGame();
            Check(s2.ToggleHole(6, 2) == SelectionResult.RowChanged, "marca o buraco 2 da fileira 7");
            Check(s2.ToggleHole(6, 3) == SelectionResult.Added, "vizinho à direita entra");
            Check(s2.ToggleHole(6, 1) == SelectionResult.Added, "vizinho à esquerda entra");
            Check(s2.ToggleHole(6, 5) == SelectionResult.RejectedNotAdjacent, "buraco distante é recusado");
            Check(s2.SelectedCount == 1, "recusa reinicia a marcação no buraco tocado");

            // Tirar o miolo mantém o maior bloco contíguo.
            Check(GameSession.LargestContiguousBlock(0b0110111u) == 0b0000111u, "maior bloco contíguo (3 bits)");
            Check(GameSession.LargestContiguousBlock(0b1100011u) == 0b0000011u, "empate fica com o bloco mais à direita");
            Check(GameSession.LargestContiguousBlock(0b0010000u) == 0b0010000u, "bloco único é preservado");

            // Máquina joga: humano não pode marcar.
            var cpu = new GameSession(new GameSettings { mode = GameMode.Cpu, variant = Variant.Livre });
            cpu.NewGame();
            cpu.Apply(new Move(0, 0b1u));
            Check(cpu.IsMachineTurn, "depois do humano, é a vez da máquina");
            Check(cpu.ToggleHole(6, 0) == SelectionResult.Rejected, "toque é ignorado na vez da máquina");
        }

        private static void TestPerfectPlay(Variant variant)
        {
            Section($"jogo perfeito ({variant})");
            var solver = new MisereSolver();
            bool firstPlayerWins = solver.CurrentPlayerWins(Board.Dug.ToPosition(variant), variant);
            Console.WriteLine($"  tabuleiro cheio: quem começa {(firstPlayerWins ? "GANHA" : "PERDE")} com jogo perfeito");

            // O Rato de Praia nunca pode perder quando a teoria lhe dá a vitória.
            int ratoWins = 0;
            const int games = 300;
            for (int g = 0; g < games; g++)
            {
                var rng = new Random(1000 + g);
                int ratoSeat = firstPlayerWins ? 0 : 1;
                int winner = PlayGame(variant, rng, ratoSeat, AiLevel.Rato, AiLevel.Turista, solver);
                if (winner == ratoSeat)
                {
                    ratoWins++;
                }
            }

            Check(ratoWins == games, $"Rato de Praia venceu {ratoWins}/{games} partidas na cadeira vencedora");

            // Na cadeira perdedora, o Rato ainda deve aproveitar o erro do Turista com frequência.
            int stolen = 0;
            for (int g = 0; g < games; g++)
            {
                var rng = new Random(5000 + g);
                int ratoSeat = firstPlayerWins ? 1 : 0;
                int winner = PlayGame(variant, rng, ratoSeat, AiLevel.Rato, AiLevel.Turista, solver);
                if (winner == ratoSeat)
                {
                    stolen++;
                }
            }

            Console.WriteLine($"  Rato na cadeira perdedora: {stolen}/{games} vitórias contra o Turista");
            Check(stolen > games / 2, "Rato pune os erros do Turista na maioria das partidas");
        }

        private static void TestGameAlwaysEnds()
        {
            Section("partidas sempre terminam e o último a tapar perde");
            var solver = new MisereSolver();
            foreach (Variant variant in new[] { Variant.Livre, Variant.Vizinhos })
            {
                for (int g = 0; g < 400; g++)
                {
                    var rng = new Random(9000 + g);
                    var settings = new GameSettings { mode = GameMode.DoisJogadores, variant = variant };
                    var session = new GameSession(settings, solver, rng);
                    session.NewGame();

                    int lastPlayer = -1;
                    int guard = 0;
                    var moves = new List<Move>();
                    while (!session.IsOver)
                    {
                        MoveGenerator.Generate(session.Board, variant, moves, rng);
                        Move move = moves[rng.Next(moves.Count)];
                        lastPlayer = session.CurrentPlayer;
                        Check(session.Apply(move), "lance legal é aceito");
                        if (++guard > Rules.HoleCount + 2)
                        {
                            break;
                        }
                    }

                    Check(session.IsOver, "a partida terminou");
                    Check(session.Board.IsEmpty, "tabuleiro totalmente tapado no fim");
                    Check(session.Winner == 1 - lastPlayer, "quem tapou o último buraco perdeu");
                    Check(settings.score[session.Winner] == 1, "placar do vencedor subiu");
                    Check(settings.starter == 1, "quem começa alterna depois da partida");
                }
            }
        }

        private static void ReportSolverStats()
        {
            Section("custo do solver (pior caso: primeiro lance do tabuleiro cheio)");
            foreach (Variant variant in new[] { Variant.Livre, Variant.Vizinhos })
            {
                var solver = new MisereSolver();
                var sw = Stopwatch.StartNew();
                solver.Prewarm(variant);
                sw.Stop();
                Console.WriteLine($"  {variant}: {sw.Elapsed.TotalMilliseconds:F2} ms, {solver.MemoSize(variant)} estados memoizados");

                var ai = new BeachAi(solver, new Random(3));
                var sw2 = Stopwatch.StartNew();
                ai.Choose(Board.Dug, variant, AiLevel.Rato);
                sw2.Stop();
                Console.WriteLine($"  {variant}: escolha do Rato no tabuleiro cheio = {sw2.Elapsed.TotalMilliseconds:F2} ms");
                Check(sw2.Elapsed.TotalMilliseconds < 250, $"escolha da máquina cabe num frame de carregamento ({variant})");
            }
        }

        // ---------------------------------------------------------------- utilidades

        private static int PlayGame(Variant variant, Random rng, int smartSeat, AiLevel smart, AiLevel dumb, MisereSolver solver)
        {
            var settings = new GameSettings { mode = GameMode.DoisJogadores, variant = variant };
            var session = new GameSession(settings, solver, rng);
            session.NewGame();

            var smartAi = new BeachAi(solver, rng);
            var dumbAi = new BeachAi(solver, rng);

            int guard = 0;
            while (!session.IsOver && guard++ <= Rules.HoleCount + 2)
            {
                bool isSmart = session.CurrentPlayer == smartSeat;
                Move move = isSmart
                    ? smartAi.Choose(session.Board, variant, smart)
                    : dumbAi.Choose(session.Board, variant, dumb);
                session.Apply(move);
            }

            return session.Winner;
        }

        private static IEnumerable<List<int>> AllPartMultisets()
        {
            // Todo multiconjunto de pedaços de 1..7 somando de 1 a 28 (superconjunto das posições possíveis).
            var current = new List<int>();
            foreach (List<int> item in Build(Rules.RowCount, Rules.HoleCount, current))
            {
                yield return item;
            }

            static IEnumerable<List<int>> Build(int maxPart, int budget, List<int> acc)
            {
                if (acc.Count > 0)
                {
                    yield return new List<int>(acc);
                }

                for (int part = Math.Min(maxPart, budget); part >= 1; part--)
                {
                    acc.Add(part);
                    foreach (List<int> item in Build(part, budget - part, acc))
                    {
                        yield return item;
                    }

                    acc.RemoveAt(acc.Count - 1);
                }
            }
        }

        private static List<bool>[] ToNaive(in Board board)
        {
            var rows = new List<bool>[Rules.RowCount];
            for (int r = 0; r < Rules.RowCount; r++)
            {
                rows[r] = new List<bool>();
                for (int i = 0; i < Board.RowLength(r); i++)
                {
                    rows[r].Add(board.IsOpen(r, i));
                }
            }

            return rows;
        }

        private static List<int> NaiveSegments(List<bool> row)
        {
            var outp = new List<int>();
            int c = 0;
            foreach (bool open in row)
            {
                if (open)
                {
                    c++;
                }
                else if (c > 0)
                {
                    outp.Add(c);
                    c = 0;
                }
            }

            if (c > 0)
            {
                outp.Add(c);
            }

            return outp;
        }

        private static List<int> NaiveParts(List<bool>[] rows, Variant variant)
        {
            var parts = new List<int>();
            foreach (List<bool> row in rows)
            {
                if (variant == Variant.Vizinhos)
                {
                    parts.AddRange(NaiveSegments(row));
                }
                else
                {
                    int c = row.Count(o => o);
                    if (c > 0)
                    {
                        parts.Add(c);
                    }
                }
            }

            return parts;
        }

        private static int CountBitsNaive(uint value)
        {
            int n = 0;
            while (value != 0)
            {
                n += (int)(value & 1u);
                value >>= 1;
            }

            return n;
        }

        private static int TrailingZerosNaive(uint value)
        {
            int n = 0;
            while ((value & 1u) == 0u)
            {
                value >>= 1;
                n++;
            }

            return n;
        }

        /// <summary>
        /// Reimplementação literal do algoritmo do protótipo web (listas + chave em string),
        /// usada só para cruzar resultados com o solver empacotado.
        /// </summary>
        private sealed class NaiveSolver
        {
            private readonly Dictionary<string, bool> _memo = new Dictionary<string, bool>();
            private readonly Variant _variant;

            public NaiveSolver(Variant variant) => _variant = variant;

            public bool Wins(List<int> parts)
            {
                string key = Key(parts);
                if (_memo.TryGetValue(key, out bool hit))
                {
                    return hit;
                }

                bool res = false;
                foreach (List<int> s in Successors(parts))
                {
                    if (s.Count == 0)
                    {
                        continue;
                    }

                    if (!Wins(s))
                    {
                        res = true;
                        break;
                    }
                }

                _memo[key] = res;
                return res;
            }

            private static string Key(List<int> parts)
            {
                var copy = new List<int>(parts);
                copy.Sort();
                return string.Join(",", copy);
            }

            private IEnumerable<List<int>> Successors(List<int> p)
            {
                for (int i = 0; i < p.Count; i++)
                {
                    var rest = new List<int>(p);
                    rest.RemoveAt(i);
                    int n = p[i];

                    if (_variant == Variant.Vizinhos)
                    {
                        for (int ini = 0; ini < n; ini++)
                        {
                            for (int len = 1; len <= n - ini; len++)
                            {
                                int esq = ini;
                                int dir = n - ini - len;
                                var novo = new List<int>(rest);
                                if (esq > 0)
                                {
                                    novo.Add(esq);
                                }

                                if (dir > 0)
                                {
                                    novo.Add(dir);
                                }

                                yield return novo;
                            }
                        }
                    }
                    else
                    {
                        for (int v = 0; v < n; v++)
                        {
                            var novo = new List<int>(rest);
                            if (v > 0)
                            {
                                novo.Add(v);
                            }

                            yield return novo;
                        }
                    }
                }
            }
        }
    }
}
