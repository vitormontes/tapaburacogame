// TAPA BURACO — harness headless.
// Roda o jogo INTEIRO (núcleo + camada de apresentação) contra o shim de UnityEngine, sem
// editor e sem janela: boot → título → setup → partida completa → fim → revanche.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private const float FrameStep = 1f / 60f;

        /// <summary>Teto de quadros de uma partida inteira (≈100 s de tempo virtual).</summary>
        private const int MatchFrameBudget = 6000;

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
            Check(!Visible("tela-setup"), "tela de setup escondida");
            Check(!Visible("tela-jogo"), "tela de jogo escondida");
            Check(!Visible("tela-fim"), "tela de fim escondida");
            Check(!Visible("modal-regras"), "modal de regras fechado");

            Phase("2. título → setup");
            Check(Click("ir-setup"), "clique em \"Jogar\" aceito");
            Frames(2);
            Check(Visible("tela-setup") && !Visible("tela-titulo"), "setup visível e título escondido");

            Check(Click("cpu"), "opção \"Contra o computador\" clicada");
            Check(Click("rato"), "opção \"Rato de Praia\" clicada");
            Check(Click("livre"), "opção \"Livre\" clicada");
            Frames(2);
            Check(_settings.mode == GameMode.Cpu, $"modo = {_settings.mode}");
            Check(_settings.level == AiLevel.Rato, $"nível = {_settings.level}");
            Check(_settings.variant == Variant.Livre, $"variante = {_settings.variant}");

            Phase("3. setup → jogo");
            int[] scoreBefore = { _settings.score[0], _settings.score[1] };
            int finishedBefore = _settings.gamesFinished;

            Check(Click("comecar"), "clique em \"Começar\" aceito");
            Frames(2);
            Check(Visible("tela-jogo") && !Visible("tela-setup"), "tela de jogo visível");

            GameObject[] holes = HoleObjects();
            Check(holes.Length == Rules.HoleCount, $"{holes.Length} buracos montados no tabuleiro");
            Check(_session.Board.OpenCount == Rules.HoleCount, $"{_session.Board.OpenCount} buracos abertos");

            BoardView board = UnityRuntime.FindFirst<BoardView>();
            Check(board != null && board.Unit > 0f, $"tabuleiro dimensionado (--u = {board?.Unit ?? 0f:F1} px de canvas)");
            Check(board != null && board.Root.rect.width > 0f && board.Root.rect.height > 0f,
                $"caixa de areia mede {board?.Root.rect.width ?? 0f:F0}×{board?.Root.rect.height ?? 0f:F0}");

            int clickable = holes.Count(h => h.GetComponent<Button>().IsInteractable());
            Check(clickable == Rules.HoleCount, $"{clickable} buracos clicáveis na vez do humano");

            Phase("4. partida completa (humano vs Rato de Praia)");
            PlayUntilEnd(holes);
            Check(_session.IsOver, $"partida terminou em {_movesPlayed} lances");
            Check(_movesPlayed > 1, "os dois lados jogaram");

            Phase("5. fim de partida");
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
            Check(persisted.level == _settings.level && persisted.variant == _settings.variant,
                "opções persistidas junto com o placar");

            int sounds = UnityRuntime.FindAll<AudioSource>().Sum(s => s.PlayCount);
            Check(sounds > 0, $"{sounds} disparos de áudio procedural durante a partida");

            Phase("6. revanche");
            Check(Click("fim-denovo"), "clique em \"Jogar de novo\" aceito");
            Frames(3);
            Check(Visible("tela-jogo") && !Visible("tela-fim"), "voltou para a tela de jogo");
            Check(_session.Board.OpenCount == Rules.HoleCount,
                $"tabuleiro recavado com {_session.Board.OpenCount} buracos abertos");
            Check(HoleObjects().Length == Rules.HoleCount, "28 buracos na hierarquia da revanche");
            Check(!_session.IsOver, "partida nova em andamento");

            Phase("7. HUD: estilo, regras e mudo");
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

            Phase("8. virada de layout (paisagem ↔ retrato)");
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

            Phase("9. saúde geral");
            Check(Debug.Errors.Count == 0, $"nenhum Debug.LogError ({Debug.Errors.Count})");
            Check(Debug.Warnings.Count == 0, $"nenhum Debug.LogWarning ({Debug.Warnings.Count})");
        }

        // ------------------------------------------------------------------ partida

        private static void PlayUntilEnd(GameObject[] holes)
        {
            var legal = new List<Move>(64);
            var rng = new System.Random(4242);

            for (int frame = 0; frame < MatchFrameBudget; frame++)
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

                bool humanTurn = !_session.IsBusy && !_session.IsMachineTurn;
                if (humanTurn)
                {
                    legal.Clear();
                    MoveGenerator.Generate(_session.Board, _settings.variant, legal, rng);
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
            }

            Check(false, $"a partida não terminou em {MatchFrameBudget} quadros");
        }

        /// <summary>Clica nos buracos do lance e depois no botão TAPAR, como um dedo faria.</summary>
        private static bool PlayHumanMove(GameObject[] holes, Move move)
        {
            int expected = 0;
            for (int i = 0; i < Board.RowLength(move.Row); i++)
            {
                if ((move.RowHoles & (1u << i)) == 0u)
                {
                    continue;
                }

                GameObject hole = holes[Board.RowOffset(move.Row) + i];
                if (!Click(hole))
                {
                    Check(false, $"buraco (fileira {move.Row + 1}, {i + 1}) não estava clicável");
                    return false;
                }

                expected++;
            }

            if (_session.SelectedRow != move.Row || _session.SelectedCount != expected)
            {
                Check(false,
                    $"seleção não bateu: fileira {_session.SelectedRow + 1} com {_session.SelectedCount} de {expected}");
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
