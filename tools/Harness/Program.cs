// TAPA BURACO — harness headless.
// Roda o jogo INTEIRO (núcleo + camada de apresentação) contra o shim de UnityEngine, sem
// editor e sem janela: boot → título → "Contra o computador" → adversário → partida completa
// → fim → revanche → menu, e uma partida de 2 jogadores até o "Menu" do fim.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TapaBuraco.Core;
using TapaBuraco.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace TapaBuraco.Harness
{
    internal static class Program
    {
        private static string Hint() => Private<Text>(Private<GameScreen>(_app, "_game"), "_hintText")?.text ?? string.Empty;

        private static string TextOf(string name) => Node(name)?.GetComponent<Text>()?.text ?? string.Empty;

        private const float FrameStep = 1f / 60f;

        /// <summary>Teto de quadros de uma partida inteira (≈100 s de tempo virtual), sem contar a pensada da máquina.</summary>
        private const int MatchFrameBudget = 6000;

        /// <summary>Teto de tempo REAL de uma partida: a busca do Rato roda numa thread de verdade.</summary>
        private const int MatchWallClockMs = 180000;

        private static int _ok;
        private static int _fail;

        private static GameApp _app;
        private static GameSession _session;
        private static GameSettings _settings;

        private static int _lastMover = -1;
        private static int _movesPlayed;

        private static int Main(string[] args)
        {
            Debug.Verbose = args.Contains("--verbose");

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("== TAPA BURACO — harness headless (shim de UnityEngine) ==");
            Console.WriteLine();

            try
            {
                Run();
            }
            catch (Exception e)
            {
                _fail++;
                Console.WriteLine($"FALHOU: exceção não tratada — {e}");
            }

            Console.WriteLine();
            Console.WriteLine($"verificações: {_ok + _fail} | OK: {_ok} | FALHOU: {_fail}");
            if (Debug.Errors.Count > 0)
            {
                Console.WriteLine($"Debug.LogError durante a execução: {Debug.Errors.Count}");
            }

            return _fail == 0 ? 0 : 1;
        }

        private static void Run()
        {
            Phase("1. boot e tela de título");
            Boot();
            Frames(3);

            _app = UnityRuntime.FindFirst<GameApp>();
            Check(_app != null, "[RuntimeInitializeOnLoadMethod] criou o GameApp sem cena nem prefab");
            _session = Private<GameSession>(_app, "_session");
            _settings = Private<GameSettings>(_app, "_settings");
            Check(_session != null && _settings != null, "GameApp montou sessão e preferências");

            _session.MoveApplied += (player, move, board) =>
            {
                _lastMover = player;
                _movesPlayed++;
            };

            var canvas = UnityRuntime.FindFirst<Canvas>();
            Check(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "canvas em ScreenSpaceOverlay");
            Rect canvasRect = ((RectTransform)canvas.transform).rect;
            Check(
                Mathf.Approximately(canvasRect.width, 1080f) && Mathf.Approximately(canvasRect.height, 1920f),
                $"raiz do canvas mede {canvasRect.width:F0}×{canvasRect.height:F0}");

            Check(Visible("tela-titulo"), "tela de título ativa logo após o boot");
            Check(Node("tela-setup") == null, "não existe mais tela de setup");
            Check(Visible("ir-cpu") && Visible("ir-2p"), "botões \"Contra o computador\" e \"2 jogadores\" visíveis");
            Check(!Visible("escolha-nivel") && !Visible("nivel-voltar"), "escolha de adversário e \"← Voltar\" escondidos");
            Check(Visible("ir-regras") && Visible("ir-ajustes"), "links \"Como joga\" e \"Ajustes\" visíveis");
            Check(!Visible("tela-jogo"), "tela de jogo escondida");
            Check(!Visible("tela-fim"), "tela de fim escondida");
            Check(!Visible("modal-regras") && !Visible("modal-ajustes"), "modais fechados");

            Phase("2. título: Como joga e Ajustes");
            Check(Click("ir-regras"), "link \"Como joga\" clicado");
            Frames(2);
            Check(Visible("modal-regras"), "modal de regras aberto");
            Check(TextOf("li3").Contains("O O O X O"), "regras explicam o bloqueio da linha (O O O X O)");
            Check(TextOf("li2").Contains("Diagonal não vale"), "regras dizem que diagonal não vale");
            Check(Click("fechar-regras"), "botão \"Entendi\" clicado");
            Frames(2);
            Check(!Visible("modal-regras"), "modal de regras fechado");

            Check(Click("ir-ajustes"), "link \"Ajustes\" clicado");
            Frames(2);
            Check(Visible("modal-ajustes"), "modal de ajustes aberto");
            Check(Node("g-skin") != null && Node("g-som") != null, "ajustes têm Estilo e Som");
            bool musicBefore = _settings.music;
            Check(Click("mus"), "opção \"Musiquinha\" clicada");
            Check(_settings.music != musicBefore, "musiquinha alternou");
            Check(SettingsStore.Load().music == _settings.music, "ajuste de som persistido");
            Click("mus");
            Skin skinAntes = Palette.Skin;
            Check(Click(skinAntes == Skin.Praia ? "papel" : "praia"), "opção de estilo clicada");
            Frames(2);
            Check(Palette.Skin != skinAntes && _settings.skin == Palette.Skin, $"estilo trocou para {Palette.Skin}");
            Click(skinAntes == Skin.Praia ? "praia" : "papel");
            Frames(2);
            Check(Palette.Skin == skinAntes, "estilo voltou");
            Check(Click("fechar-ajustes"), "botão \"Pronto\" clicado");
            Frames(2);
            Check(!Visible("modal-ajustes"), "modal de ajustes fechado");

            Phase("3. título → Contra o computador → adversário");
            _settings.level = AiLevel.Banhista;
            _settings.mode = GameMode.Cpu;
            _settings.score[0] = 3;
            _settings.score[1] = 2;
            _settings.starter = 0;

            Check(Click("ir-cpu"), "clique em \"Contra o computador\" aceito");
            Frames(2);
            Check(Visible("escolha-nivel") && !Visible("escolha-modo"), "os dois botões viraram os três adversários");
            Check(Visible("nivel-voltar"), "link \"← Voltar\" visível");
            Check(Visible("turista") && Visible("banhista") && Visible("rato"), "Turista, Banhista e Rato de Praia");
            OptionView[] niveis = Private<OptionView[]>(Private<TitleScreen>(_app, "_title"), "_niveis");
            Check(niveis != null && niveis[(int)AiLevel.Banhista].Pressed && !niveis[(int)AiLevel.Rato].Pressed,
                "último adversário usado (Banhista) aceso");

            Check(Click("nivel-voltar"), "\"← Voltar\" clicado");
            Frames(2);
            Check(Visible("escolha-modo") && !Visible("escolha-nivel") && !Visible("nivel-voltar"), "voltou para os dois modos");

            Click("ir-cpu");
            Frames(2);
            int finishedBefore = _settings.gamesFinished;
            Check(Click("rato"), "\"Rato de Praia\" clicado");
            Frames(2);
            Check(_settings.mode == GameMode.Cpu && _settings.level == AiLevel.Rato, $"modo = {_settings.mode}, nível = {_settings.level}");
            Check(_settings.score[0] == 0 && _settings.score[1] == 0, "trocar de adversário zerou o placar");
            Check(Visible("tela-jogo") && !Visible("tela-titulo"), "o adversário começa a partida na hora");

            GameObject[] holes = HoleObjects();
            Check(holes.Length == Rules.HoleCount, $"{holes.Length} buracos montados no tabuleiro");
            Check(_session.Board.OpenCount == Rules.HoleCount, $"{_session.Board.OpenCount} buracos abertos");
            CheckStaircase(holes);

            BoardView board = UnityRuntime.FindFirst<BoardView>();
            Check(board != null && board.Unit > 0f, $"tabuleiro dimensionado (--u = {board?.Unit ?? 0f:F1} px de canvas)");
            Check(board != null && board.Root.rect.width > 0f && board.Root.rect.height > 0f,
                $"caixa de areia mede {board?.Root.rect.width ?? 0f:F0}×{board?.Root.rect.height ?? 0f:F0}");

            int clickable = holes.Count(h => h.GetComponent<Button>().IsInteractable());
            Check(clickable == Rules.HoleCount, $"{clickable} buracos clicáveis na vez do humano");

            Phase("4. marcação por toque");
            CheckSelectionByClicks(holes);

            Phase("5. partida completa (humano vs Rato de Praia)");
            int[] scoreBefore = { _settings.score[0], _settings.score[1] };
            _movesPlayed = 0;
            PlayUntilEnd(holes);
            Check(_session.IsOver, $"partida terminou em {_movesPlayed} lances");
            Check(_movesPlayed > 1, "os dois lados jogaram");

            Phase("6. fim de partida");
            Check(Visible("tela-fim"), "tela de fim visível");
            Check(!Visible("tela-jogo"), "tela de jogo escondida");
            Check(_session.Board.IsEmpty, "tabuleiro sem nenhum buraco aberto");
            Check(_session.Winner == 1 - _lastMover,
                $"quem tapou o último buraco (jogador {_lastMover + 1}) perdeu; venceu o jogador {_session.Winner + 1}");

            int winner = _session.Winner;
            Check(_settings.score[winner] == scoreBefore[winner] + 1,
                $"placar do vencedor subiu: {scoreBefore[0]}×{scoreBefore[1]} → {_settings.score[0]}×{_settings.score[1]}");
            Check(_settings.score[1 - winner] == scoreBefore[1 - winner], "placar do perdedor ficou igual");
            Check(_settings.gamesFinished == finishedBefore + 1, "contador de partidas terminadas subiu");

            GameSettings persisted = SettingsStore.Load();
            Check(
                persisted.score[0] == _settings.score[0] && persisted.score[1] == _settings.score[1],
                $"placar persistido no PlayerPrefs: {persisted.score[0]}×{persisted.score[1]}");
            Check(persisted.level == _settings.level && persisted.mode == _settings.mode,
                "modo e adversário persistidos junto com o placar");

            int sounds = UnityRuntime.FindAll<AudioSource>().Sum(s => s.PlayCount);
            Check(sounds > 0, $"{sounds} disparos de áudio procedural durante a partida");

            Phase("7. revanche");
            Check(Click("fim-denovo"), "clique em \"Jogar de novo\" aceito");
            Frames(3);
            Check(Visible("tela-jogo") && !Visible("tela-fim"), "voltou para a tela de jogo");
            Check(_session.Board.OpenCount == Rules.HoleCount,
                $"tabuleiro recavado com {_session.Board.OpenCount} buracos abertos");
            Check(HoleObjects().Length == Rules.HoleCount, "28 buracos na hierarquia da revanche");
            Check(!_session.IsOver, "partida nova em andamento");
            Check(_session.CurrentPlayer == 1 && _session.IsMachineTurn, "na revanche quem começa é a máquina (alterna)");
            Check(_settings.score[winner] == scoreBefore[winner] + 1, "revanche mantém o placar");

            Phase("8. HUD: estilo, regras e mudo");
            Skin skinBefore = Palette.Skin;
            Check(Click("b-skin"), "botão de estilo clicado");
            Frames(2);
            Check(Palette.Skin != skinBefore, $"skin trocou para {Palette.Skin}");
            Check(_settings.skin == Palette.Skin, "preferência de skin acompanhou a troca");
            Check(Click("b-skin"), "botão de estilo clicado de novo");
            Frames(2);
            Check(Palette.Skin == skinBefore, $"skin voltou para {Palette.Skin}");

            Check(Click("b-regras2"), "botão de regras do HUD clicado");
            Frames(2);
            Check(Visible("modal-regras"), "modal de regras aberto");
            Check(Click("fechar-regras"), "botão \"Entendi\" clicado");
            Frames(2);
            Check(!Visible("modal-regras"), "modal de regras fechado");

            Click("b-regras2");
            Frames(2);
            Input.PressKey(KeyCode.Escape);
            Frames(2);
            Check(!Visible("modal-regras"), "Esc também fecha o modal de regras");

            bool soundBefore = _settings.AnySound;
            Check(Click("b-som"), "botão de mudo clicado");
            Frames(2);
            Check(_settings.AnySound != soundBefore, $"som geral agora {(_settings.AnySound ? "ligado" : "mudo")}");
            Check(Click("b-som"), "botão de mudo clicado de novo");
            Frames(2);
            Check(_settings.AnySound == soundBefore, "som geral restaurado");

            Phase("9. virada de layout (paisagem ↔ retrato)");
            BoardView playing = UnityRuntime.FindFirst<BoardView>();
            var side = (RectTransform)Node("lateral").transform;
            var holder = (RectTransform)Node("areia-holder").transform;
            float unitPortrait = playing.Unit;
            Check(Mathf.Approximately(side.anchorMin.x, 0f), "em retrato a lateral fica na faixa de baixo");

            Resize(1920, 1080);
            Frames(3);
            Check(Mathf.Approximately(side.anchorMin.x, 1f), "em paisagem a lateral vai para a direita");
            Check(holder.offsetMax.x < 0f, $"tabuleiro cede {-holder.offsetMax.x:F0} px de canvas para a lateral");
            float unitLandscape = playing.Unit;
            Check(
                unitLandscape > 0f && !Mathf.Approximately(unitLandscape, unitPortrait),
                $"--u recalculado na virada: {unitPortrait:F1} → {unitLandscape:F1}");
            Check(
                HoleObjects().Length == Rules.HoleCount && playing.Root.rect.width > 0f,
                $"tabuleiro segue com 28 buracos numa caixa de {playing.Root.rect.width:F0}×{playing.Root.rect.height:F0}");

            Resize(1080, 1920);
            Frames(3);
            Check(Mathf.Approximately(side.anchorMin.x, 0f), "de volta ao retrato, a lateral desce");
            Check(Mathf.Approximately(playing.Unit, unitPortrait), "--u volta ao valor de retrato");

            Phase("10. menu no meio da vez da máquina");
            // Garante que a máquina está na vez (pensando ou animando) quando o menu é tocado.
            if (!_session.IsMachineTurn || _session.IsOver)
            {
                Check(false, "a revanche deveria estar na vez da máquina");
            }

            int movesBeforeMenu = _movesPlayed;
            Check(Click("b-menu"), "botão de menu do HUD clicado");
            Frames(2);
            Check(Visible("tela-titulo") && !Visible("tela-jogo"), "voltou para o título");
            Check(Visible("escolha-modo") && !Visible("escolha-nivel"), "título mostra os dois modos");
            Check(!_session.IsBusy && !_session.IsMachineThinking, "pensada da máquina cancelada");
            for (int i = 0; i < 300; i++)
            {
                Thread.Sleep(1);
                Frame();
            }

            Check(_movesPlayed == movesBeforeMenu, "nenhum lance atrasado da máquina chegou depois do menu");

            Phase("11. 2 jogadores");
            Check(Click("ir-2p"), "clique em \"2 jogadores\" aceito");
            Frames(2);
            Check(Visible("tela-jogo") && !Visible("tela-titulo"), "partida passa-e-joga começou na hora");
            Check(_settings.mode == GameMode.DoisJogadores, $"modo = {_settings.mode}");
            Check(_settings.score[0] == 0 && _settings.score[1] == 0, "trocar de modo zerou o placar");
            Check(_session.Board.OpenCount == Rules.HoleCount && !_session.IsMachineTurn, "tabuleiro cheio e vez de um humano");
            Check(SettingsStore.Load().mode == GameMode.DoisJogadores, "modo persistido");

            holes = HoleObjects();
            _movesPlayed = 0;
            PlayUntilEnd(holes);
            Check(_session.IsOver && Visible("tela-fim"), $"partida de 2 jogadores terminou em {_movesPlayed} lances");
            Check(_session.Winner == 1 - _lastMover, "quem tapou o último perdeu");

            Check(Click("fim-menu"), "\"Menu\" do fim clicado");
            Frames(2);
            Check(Visible("tela-titulo") && Visible("escolha-modo") && !Visible("tela-fim"), "\"Menu\" volta para o título com os dois modos");

            Phase("12. saúde geral");
            Check(Debug.Errors.Count == 0, $"nenhum Debug.LogError ({Debug.Errors.Count})");
            Check(Debug.Warnings.Count == 0, $"nenhum Debug.LogWarning ({Debug.Warnings.Count})");
        }

        /// <summary>Escada invertida alinhada à esquerda: colunas retas, fileiras de 7 a 1.</summary>
        private static void CheckStaircase(GameObject[] holes)
        {
            bool columnsStraight = true;
            bool rowsLevel = true;
            for (int r = 0; r < Rules.RowCount; r++)
            {
                for (int c = 0; c < Board.RowLength(r); c++)
                {
                    Vector2 p = ((RectTransform)holes[Board.Index(r, c)].transform).anchoredPosition;
                    Vector2 top = ((RectTransform)holes[Board.Index(0, c)].transform).anchoredPosition;
                    Vector2 first = ((RectTransform)holes[Board.Index(r, 0)].transform).anchoredPosition;
                    columnsStraight &= Mathf.Abs(p.x - top.x) < 0.01f;
                    rowsLevel &= Mathf.Abs(p.y - first.y) < 0.01f;
                }
            }

            Check(columnsStraight, "buracos da mesma coluna alinhados na vertical (escada à esquerda)");
            Check(rowsLevel, "buracos da mesma fileira na mesma altura");
            Vector2 a = ((RectTransform)holes[Board.Index(0, 0)].transform).anchoredPosition;
            Vector2 b = ((RectTransform)holes[Board.Index(6, 0)].transform).anchoredPosition;
            Check(a.y > b.y, "fileira de 7 em cima, de 1 embaixo");

            string[] labels = Everything().Where(go => go.name == "estaca")
                .Select(go => go.transform.Find("numero")?.GetComponent<Text>()?.text).ToArray();
            Check(labels.SequenceEqual(new[] { "7", "6", "5", "4", "3", "2", "1" }), $"estacas mostram o tamanho: {string.Join(",", labels)}");
        }

        /// <summary>Seleção por cliques reais: trecho, diagonal, linha bloqueada e Desfazer.</summary>
        private static void CheckSelectionByClicks(GameObject[] holes)
        {
            GameObject H(int r, int c) => holes[Board.Index(r, c)];

            Check(Click(H(0, 1)) && Click(H(0, 4)), "tocar começo e fim da fileira");
            Check(_session.SelectedCount == 4 && _session.SelectionIsHorizontal, $"4 buracos marcados na horizontal ({_session.SelectedCount})");
            Check(Hint().Contains("4") && Hint().Contains("horizontal"), $"dica: {Hint()}");
            Check(Click(H(3, 1)), "tocar fora da linha");
            Check(_session.SelectedCount == 1 && Hint().Contains("Recomeçou"), $"recomeçou no buraco tocado — dica: {Hint()}");
            Check(Click(H(4, 2)), "tocar na diagonal");
            Check(_session.SelectedCount == 1 && Hint().Contains("Diagonal"), $"diagonal recusada — dica: {Hint()}");
            Check(Click(H(0, 2)), "tocar o topo da coluna");
            Check(_session.SelectedCount == 5 && !_session.SelectionIsHorizontal, "coluna 2 inteira marcada na vertical");
            Check(Hint().Contains("vertical"), $"dica: {Hint()}");
            Check(Click("btn-desfazer"), "\"Desfazer\" clicado");
            Check(_session.SelectedCount == 0 && Hint().Contains("linha reta"), $"seleção limpa — dica: {Hint()}");
            Check(Click(H(5, 0)), "um buraco marcado");
            Check(Hint().Contains("siga na horizontal ou na vertical"), $"dica: {Hint()}");
            Click("btn-desfazer");
        }

        // ------------------------------------------------------------------ partida

        private static void PlayUntilEnd(GameObject[] holes)
        {
            var legal = new List<Move>(Rules.SegmentCount);
            var rng = new System.Random(4242);
            var wall = System.Diagnostics.Stopwatch.StartNew();

            for (int frame = 0; frame < MatchFrameBudget && wall.ElapsedMilliseconds < MatchWallClockMs;)
            {
                if (_session.IsOver)
                {
                    // Deixa o cartão de fim entrar (EndCardDelaySeconds).
                    for (int i = 0; i < 120 && !Visible("tela-fim"); i++)
                    {
                        Frame();
                    }

                    return;
                }

                if (_session.IsMachineThinking)
                {
                    // A busca roda numa thread de verdade: espera em tempo real, sem gastar quadros.
                    Thread.Sleep(1);
                    Frame();
                    continue;
                }

                bool humanTurn = !_session.IsBusy && !_session.IsMachineTurn;
                if (humanTurn)
                {
                    MoveGenerator.Generate(_session.Board, legal);
                    if (legal.Count == 0)
                    {
                        Check(false, "o núcleo não gerou nenhum lance legal");
                        return;
                    }

                    Move move = legal[rng.Next(legal.Count)];
                    if (!PlayHumanMove(holes, move))
                    {
                        return;
                    }
                }

                Frame();
                frame++;
            }

            Check(false, $"a partida não terminou em {MatchFrameBudget} quadros / {MatchWallClockMs / 1000} s");
        }

        /// <summary>Toca o começo e o fim do trecho (marca a linha toda) e depois TAPAR, como um dedo faria.</summary>
        private static bool PlayHumanMove(GameObject[] holes, Move move)
        {
            int[] cells = move.Cells;
            int[] taps = cells.Length == 1 ? new[] { cells[0] } : new[] { cells[0], cells[cells.Length - 1] };
            foreach (int cell in taps)
            {
                if (!Click(holes[cell]))
                {
                    Check(false, $"buraco ({Board.RowOf(cell)},{Board.ColumnOf(cell)}) não estava clicável");
                    return false;
                }
            }

            if (_session.SelectedMask != move.Mask)
            {
                Check(false, $"seleção não bateu: {_session.SelectedCount} marcados para o {move}");
                return false;
            }

            if (!Click("btn-tapar"))
            {
                Check(false, "botão TAPAR não estava clicável com seleção válida");
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ motor

        /// <summary>Dispara os [RuntimeInitializeOnLoadMethod] do jogo, como o player faria.</summary>
        private static void Boot()
        {
            var entries = new List<(RuntimeInitializeLoadType load, MethodInfo method)>();
            foreach (Type type in typeof(GameApp).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (attribute != null)
                    {
                        entries.Add((attribute.loadType, method));
                    }
                }
            }

            foreach (var entry in entries.OrderBy(e => Order(e.load)))
            {
                Console.WriteLine($"   boot: {entry.method.DeclaringType?.Name}.{entry.method.Name} ({entry.load})");
                entry.method.Invoke(null, null);
            }
        }

        private static int Order(RuntimeInitializeLoadType load) => load switch
        {
            RuntimeInitializeLoadType.SubsystemRegistration => 0,
            RuntimeInitializeLoadType.AfterAssembliesLoaded => 1,
            RuntimeInitializeLoadType.BeforeSplashScreen => 2,
            RuntimeInitializeLoadType.BeforeSceneLoad => 3,
            _ => 4,
        };

        /// <summary>Troca a resolução do canvas e da tela, como uma rotação de aparelho.</summary>
        private static void Resize(int width, int height)
        {
            UnityRuntime.CanvasSize = new Vector2(width, height);
            Screen.width = width;
            Screen.height = height;
        }

        private static void Frame()
        {
            UnityRuntime.Step(FrameStep);
            Input.NewFrame();
        }

        private static void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Frame();
            }
        }

        // ------------------------------------------------------------------ hierarquia

        private static IEnumerable<GameObject> Walk(GameObject go)
        {
            yield return go;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                foreach (GameObject child in Walk(t.GetChild(i).gameObject))
                {
                    yield return child;
                }
            }
        }

        private static IEnumerable<GameObject> Everything()
            => UnityRuntime.Roots().SelectMany(Walk);

        private static GameObject Node(string name)
            => Everything().FirstOrDefault(go => go.name == name);

        private static bool Visible(string name)
        {
            GameObject go = Node(name);
            return go != null && go.activeInHierarchy;
        }

        /// <summary>Os 28 nós "buraco", na ordem de criação = índice global do tabuleiro.</summary>
        private static GameObject[] HoleObjects()
        {
            GameObject boardRoot = Node("tabuleiro");
            if (boardRoot == null)
            {
                return Array.Empty<GameObject>();
            }

            var found = new List<GameObject>(Rules.HoleCount);
            Transform t = boardRoot.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (child.name == "buraco")
                {
                    found.Add(child);
                }
            }

            return found.ToArray();
        }

        /// <summary>Clique de verdade: down → up → click pelo caminho de eventos do uGUI.</summary>
        private static bool Click(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
            {
                return false;
            }

            var button = go.GetComponent<Button>();
            if (button == null || !button.IsActive() || !button.IsInteractable())
            {
                return false;
            }

            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static bool Click(string name) => Click(Node(name));

        private static T Private<T>(object target, string field) where T : class
            => target?.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target) as T;

        // ------------------------------------------------------------------ relatório

        private static void Phase(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"-- {title}");
        }

        private static void Check(bool condition, string message)
        {
            if (condition)
            {
                _ok++;
                Console.WriteLine($"OK: {message}");
                return;
            }

            _fail++;
            Console.WriteLine($"FALHOU: {message}");
        }
    }
}
