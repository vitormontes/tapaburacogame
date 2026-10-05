using System.Collections;
using System.Threading.Tasks;
using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Bootstrap e maestro do jogo: monta o canvas, as telas, o som e conduz os turnos.
    /// Único MonoBehaviour "dono" do estado de apresentação — as telas são classes puras e o
    /// núcleo de regras (<see cref="GameSession"/>) não sabe que existe Unity.
    ///
    /// O app se cria sozinho antes da cena carregar: assim a cena pode ser vazia e não há
    /// prefab nem referência serializada para quebrar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameApp : MonoBehaviour
    {
        // Tempos do protótipo (index.html §4): um buraco a cada 115 ms e a vez só passa 430 ms
        // após o último buraco. D5 — a areia agora cai 210 ms depois da pazinha, e não 150:
        // os 60 ms extras são o hit-stop do impacto, o respiro que faz a pazinha "bater".
        private const float HoleStepSeconds = 0.115f;
        private const float SandFallSeconds = 0.210f;
        private const float TurnTailSeconds = 0.430f;

        /// <summary>D5 — amplitude do tremor de derrota, em CSS px.</summary>
        private const float ShakeAmplitudeCss = 4f;

        /// <summary>D5 — duração do tremor de derrota.</summary>
        private const float ShakeSeconds = 0.120f;

        /// <summary>Tempo mínimo da vez da máquina, contado do início da pensada (680 ms no protótipo).</summary>
        private const float MachineMinTurnSeconds = 0.680f;
        private const float EndCardDelaySeconds = 0.520f;
        private const float FirstMachineMoveSeconds = 0.650f;

        private static readonly string[] LevelShortNames = { "TURISTA", "BANHISTA", "RATO DE PRAIA" };
        private static readonly string[] LevelLongNames = { "Turista", "Banhista de Domingo", "Rato de Praia" };

        private static GameApp _instance;

        private GameSettings _settings;
        private GameSession _session;
        private SoundDirector _sound;

        private Canvas _canvas;
        private RectTransform _uiRoot;
        private RectTransform _shakeRoot;
        private Vector2 _shakeHome;

        /// <summary>Tempo corrido do tremor; negativo = parado.</summary>
        private float _shakeTime = -1f;
        private Image _backdrop;
        private Image _grain;
        private Image _halftone;
        private FrameDecor _frame;

        private TitleScreen _title;
        private SettingsModal _settingsModal;
        private GameScreen _game;
        private EndScreen _end;
        private RulesModal _rules;

        private readonly WaitForSeconds _waitHoleStep = new WaitForSeconds(HoleStepSeconds);
        private readonly WaitForSeconds _waitSandFall = new WaitForSeconds(SandFallSeconds);
        private readonly WaitForSeconds _waitTurnTail = new WaitForSeconds(TurnTailSeconds);
        private readonly WaitForSeconds _waitEndCard = new WaitForSeconds(EndCardDelaySeconds);
        private readonly WaitForSeconds _waitFirstMove = new WaitForSeconds(FirstMachineMoveSeconds);

        private Board _displayBoard = Board.Dug;
        private Coroutine _turnRoutine;
        private bool _endScreenVisible;

        /// <summary>Recado curto na dica (o <c>J.aviso</c>) até a próxima marcação.</summary>
        private string _notice = string.Empty;

        /// <summary>Telas do app (o <c>.tela.ativa</c> do protótipo).</summary>
        private enum ScreenId
        {
            Title = 0,
            Play = 1,
            End = 2,
        }

        private ScreenId _screen = ScreenId.Title;

        /// <summary>Cria o app antes de qualquer cena — dispensa objeto na cena.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("TapaBuraco");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _settings = SettingsStore.Load();
            Palette.SetSkin(_settings.skin);

            _session = new GameSession(_settings);
            _session.SelectionChanged += OnSelectionChanged;
            _session.TurnChanged += OnTurnChanged;
            _session.MoveApplied += OnMoveApplied;
            _session.GameEnded += OnGameEnded;

            BuildUi();

            _sound = SoundDirector.Create(transform);
            _sound.Bind(_settings);

            ShowScreen(ScreenId.Title);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            Palette.SkinChanged -= OnSkinChanged;
            _session?.CancelMachineThinking();

            // Texturas, sprites e clipes são gerados em runtime: soltá-los evita que o editor
            // acumule cópias a cada entrada em Play Mode (os caches são estáticos).
            SpriteFactory.Clear();
            UiKit.ClearArtCache();
            ProceduralAudio.Clear();
        }

        // ------------------------------------------------------------------ montagem

        private void BuildUi()
        {
            // A cena pode estar vazia: o app cria a câmera (fundo sólido) e o AudioListener,
            // sem os quais o Unity avisa "No cameras rendering" e o som não sai.
            if (Camera.main == null)
            {
                var cameraGo = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
                cameraGo.tag = "MainCamera";
                cameraGo.transform.SetParent(transform, false);
                Camera cam = cameraGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                cam.orthographic = true;
                cam.cullingMask = 0;
            }
            else if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemGo.transform.SetParent(transform, false);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiKit.ReferenceWidth, UiKit.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _uiRoot = (RectTransform)canvasGo.transform;

            // Fundo: cartaz da praia (skin Praia) ou papel pardo (skin Papel).
            _backdrop = UiKit.Picture(_uiRoot, "cenario", UiKit.Art("cena-praia"), Color.white);
            UiKit.FillParent(_backdrop.rectTransform);
            _backdrop.preserveAspect = false;

            RectTransform appArea = UiKit.Stretch(_uiRoot, "app", UiKit.Css(16f));

            // D5 — alvo do tremor. O RectTransform do próprio Canvas é reescrito pelo
            // CanvasScaler a cada quadro, então quem treme é a área do app logo abaixo dele:
            // o cartaz de fundo e a moldura ficam parados, e o tabuleiro é que leva o tranco.
            _shakeRoot = appArea;
            _shakeHome = appArea.anchoredPosition;

            _title = new TitleScreen(appArea);
            _game = new GameScreen(appArea);
            _end = new EndScreen(appArea);

            // Grão de impressão e halftone por cima de tudo, como os overlays fixos do CSS.
            _grain = UiKit.Picture(_uiRoot, "grao", SpriteFactory.Grain(), Palette.Tinta.WithAlpha(0.16f));
            UiKit.FillParent(_grain.rectTransform);
            _grain.type = Image.Type.Tiled;

            _halftone = UiKit.Picture(_uiRoot, "halftone", SpriteFactory.Halftone(), Palette.Tinta.WithAlpha(0.10f));
            UiKit.FillParent(_halftone.rectTransform);
            _halftone.type = Image.Type.Tiled;

            // Moldura de pedra portuguesa + vinheta, as bordas fixas do cartaz.
            _frame = new FrameDecor(_uiRoot);

            _rules = new RulesModal(_uiRoot);
            _settingsModal = new SettingsModal(_uiRoot, _settings);

            _title.CpuClicked += OnTitleCpu;
            _title.TwoPlayersClicked += () => StartGame(GameMode.DoisJogadores, _settings.level);
            _title.LevelChosen += level => StartGame(GameMode.Cpu, level);
            _title.BackClicked += OnTitleBack;
            _title.RulesClicked += OnOpenRules;
            _title.SettingsClicked += OnOpenSettings;

            _game.Board.HoleClicked += OnHoleClicked;
            _game.ConfirmClicked += OnConfirm;
            _game.UndoClicked += OnUndo;
            _game.Hud.SoundClicked += OnToggleAllSound;
            _game.Hud.SkinClicked += OnToggleSkin;
            _game.Hud.RulesClicked += OnOpenRules;
            _game.Hud.MenuClicked += GoToMenu;

            _end.AgainClicked += OnStartGame;
            _end.MenuClicked += GoToMenu;

            _rules.Closed += () => _rules.SetVisible(false);
            _settingsModal.Closed += () => _settingsModal.SetVisible(false);
            _settingsModal.Changed += OnSettingsChanged;

            Palette.SkinChanged += OnSkinChanged;
            OnSkinChanged();
        }

        // ------------------------------------------------------------------ telas

        private void ShowScreen(ScreenId screen)
        {
            _screen = screen;
            _title.SetVisible(screen == ScreenId.Title);
            _game.SetVisible(screen == ScreenId.Play);
            _end.SetVisible(screen == ScreenId.End);
            _endScreenVisible = screen == ScreenId.End;
        }

        private void OnSkinChanged()
        {
            _settings.skin = Palette.Skin;
            _backdrop.sprite = Palette.IsPaper ? SpriteFactory.Hatch(64, 94f, 18, 2) : UiKit.Art("cena-praia");
            _backdrop.color = Palette.IsPaper ? Palette.PapelClaro : Color.white;
            _backdrop.type = Palette.IsPaper ? Image.Type.Tiled : Image.Type.Simple;
            _grain.color = Palette.Tinta.WithAlpha(Palette.IsPaper ? 0.24f : 0.16f);

            _title.ApplySkin();
            _settingsModal.ApplySkin();
            _game.ApplySkin();
            _end.ApplySkin();
            _rules.ApplySkin();
            _frame.ApplySkin();
        }

        // ------------------------------------------------------------------ menus

        private void OnTitleCpu()
        {
            _sound.Tap(true);
            _title.ShowLevelChoice(_settings.level);
        }

        private void OnTitleBack()
        {
            _sound.Tap(false);
            _title.ShowModeChoice();
        }

        /// <summary>Menu do HUD e "Menu" do fim: descarta lance de máquina ou animação em curso (o <c>irMenu</c>).</summary>
        private void GoToMenu()
        {
            StopTurnRoutine();
            _title.ShowModeChoice();
            ShowScreen(ScreenId.Title);
        }

        private void OnOpenRules()
        {
            _sound.Tap(true);
            _rules.SetVisible(true);
        }

        private void OnOpenSettings()
        {
            _sound.Tap(true);
            _settingsModal.SetVisible(true);
        }

        private void OnSettingsChanged()
        {
            _sound.Tap(true);
            _sound.ApplyMix();
            _game.Hud.SetSoundOn(_settings.AnySound);
            SettingsStore.Save(_settings);
        }

        private void OnToggleAllSound()
        {
            if (_settings.AnySound)
            {
                _settings.mutedBackup[0] = _settings.sfx;
                _settings.mutedBackup[1] = _settings.ambience;
                _settings.mutedBackup[2] = _settings.music;
                _settings.sfx = false;
                _settings.ambience = false;
                _settings.music = false;
            }
            else
            {
                _settings.sfx = _settings.mutedBackup[0];
                _settings.ambience = _settings.mutedBackup[1];
                _settings.music = _settings.mutedBackup[2];
                if (!_settings.AnySound)
                {
                    _settings.sfx = true;
                    _settings.ambience = true;
                }
            }

            _sound.ApplyMix();
            _game.Hud.SetSoundOn(_settings.AnySound);
            _settingsModal.Refresh();
            SettingsStore.Save(_settings);
        }

        private void OnToggleSkin()
        {
            Palette.SetSkin(Palette.IsPaper ? Skin.Praia : Skin.Papel);
            _settings.skin = Palette.Skin;
            _settingsModal.Refresh();
            _sound.Tap(true);
            SettingsStore.Save(_settings);
        }

        // ------------------------------------------------------------------ partida

        /// <summary>
        /// Escolha no título (o <c>comeca</c> do protótipo): o placar é contra UM adversário,
        /// então trocar de modo ou de nível zera.
        /// </summary>
        private void StartGame(GameMode mode, AiLevel level)
        {
            if (mode != _settings.mode || (mode == GameMode.Cpu && level != _settings.level))
            {
                _settings.score[0] = 0;
                _settings.score[1] = 0;
            }

            _settings.mode = mode;
            if (mode == GameMode.Cpu)
            {
                _settings.level = level;
            }

            SettingsStore.Save(_settings);
            _sound.ApplyMix();
            _sound.Tap(true);
            OnStartGame();
        }

        private void OnStartGame()
        {
            StopTurnRoutine();
            _session.NewGame();
            _displayBoard = _session.Board;
            _notice = string.Empty;

            _game.Hud.SetNames(PlayerName(0), PlayerName(1));
            _game.Hud.SetScore(_settings.score[0], _settings.score[1]);
            _game.Hud.SetSoundOn(_settings.AnySound);
            _game.Board.Fx.Clear();

            ShowScreen(ScreenId.Play);
            RenderBoard();
            UpdateHint();

            if (_session.IsMachineTurn)
            {
                _turnRoutine = StartCoroutine(MachineTurn(true));
            }
        }

        private void OnHoleClicked(int row, int column)
        {
            if (_session.IsBusy || _session.IsOver || _session.IsMachineTurn)
            {
                return;
            }

            // O recado vale até a próxima marcação; a seleção nova já repinta a dica.
            _notice = string.Empty;
            SelectionResult result = _session.ToggleHole(row, column);
            switch (result)
            {
                case SelectionResult.Started:
                case SelectionResult.Extended:
                    _sound.Tap(true);
                    break;
                case SelectionResult.Removed:
                    _sound.Tap(false);
                    break;
                case SelectionResult.RestartedDiagonal:
                    _sound.Tap(false);
                    ShowNotice("<b>Diagonal não vale</b> — só na horizontal ou na vertical");
                    break;
                case SelectionResult.RestartedNotStraight:
                    _sound.Tap(false);
                    ShowNotice("Recomeçou aqui: só vale <b>uma linha reta</b>");
                    break;
                case SelectionResult.Blocked:
                    _sound.Error();
                    _game.Board.PlayReject(row, column);
                    ShowNotice("Tem buraco <b>tapado no caminho</b> — a linha para nele");
                    break;
                case SelectionResult.Rejected:
                default:
                    break;
            }
        }

        private void OnUndo()
        {
            if (_session.IsBusy)
            {
                return;
            }

            _notice = string.Empty;
            _session.ClearSelection();
            UpdateHint();
            _sound.Tap(false);
        }

        private void OnConfirm()
        {
            if (!_session.CanConfirm || !_session.TryBuildSelectedMove(out Move move))
            {
                return;
            }

            StopTurnRoutine();
            _turnRoutine = StartCoroutine(PlayMove(move));
        }

        private void OnSelectionChanged()
        {
            RenderBoard();
            UpdateHint();
        }

        private void OnTurnChanged()
        {
            RenderBoard();
            UpdateHint();
        }

        private void OnMoveApplied(int player, Move move, Board board)
        {
            _displayBoard = board;
        }

        private void OnGameEnded(int winner)
        {
            SettingsStore.Save(_settings);
            _game.Hud.SetScore(_settings.score[0], _settings.score[1]);
            _game.Hud.SetActivePlayer(_session.CurrentPlayer, true);
        }

        /// <summary>Anima o lance buraco a buraco e só então aplica no núcleo (igual ao executaLance).</summary>
        private IEnumerator PlayMove(Move move)
        {
            _session.IsBusy = true;
            _notice = string.Empty;
            _session.ClearSelection();
            RenderBoard();
            UpdateHint();

            int[] cells = move.Cells;
            for (int k = 0; k < cells.Length; k++)
            {
                int row = Board.RowOf(cells[k]);
                int column = Board.ColumnOf(cells[k]);
                Vector2 center = _game.Board.HoleCenter(row, column);
                _game.Board.Fx.Dig(center, _game.Board.Unit, _settings.reducedMotion);
                _sound.Sand(1f - k * 0.06f);
                Haptic();

                StartCoroutine(CoverAfterDelay(row, column));
                yield return _waitHoleStep;
            }

            yield return _waitTurnTail;

            _session.IsBusy = false;
            _session.Apply(move);
            _displayBoard = _session.Board;
            RenderBoard();
            UpdateHint();

            if (_session.IsOver)
            {
                yield return _waitEndCard;
                ShowEndCard(_session.Winner);
                _turnRoutine = null;
                yield break;
            }

            if (_session.IsMachineTurn)
            {
                _turnRoutine = StartCoroutine(MachineTurn(false));
                yield break;
            }

            _turnRoutine = null;
        }

        private IEnumerator CoverAfterDelay(int row, int column)
        {
            yield return _waitSandFall;
            _displayBoard = _displayBoard.Cover(row, column);
            RenderBoard();
            _game.Board.PlayCovered(row, column);
            UpdateHint();

            // D5 — o tranco de tela é exclusivo do último buraco do tabuleiro: é o instante da
            // derrota. Em qualquer outra jogada seria ruído gratuito.
            if (_displayBoard.OpenCount == 0 && !_settings.reducedMotion)
            {
                _shakeTime = 0f;
            }
        }

        /// <summary>
        /// Vez da máquina: a busca roda fora da thread principal (o Web Worker do protótipo) e
        /// a corrotina só espera ela terminar, com a dica "está pensando…" e a areia animada.
        /// Um tempo mínimo de "suspense" vale mesmo quando a resposta sai na hora.
        /// </summary>
        private IEnumerator MachineTurn(bool firstMoveOfGame)
        {
            _session.IsBusy = true;
            RenderBoard();
            UpdateHint();

            if (firstMoveOfGame)
            {
                yield return _waitFirstMove;
            }

            float started = Time.unscaledTime;
            Task<Move> thinking = _session.ThinkMachineMove();
            while (!thinking.IsCompleted)
            {
                yield return null;
            }

            // Falha da busca é bug: Result relança a exceção e a Unity a registra.
            Move move = thinking.Result;

            float wait = MachineMinTurnSeconds - (Time.unscaledTime - started);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            _session.IsBusy = false;
            _turnRoutine = StartCoroutine(PlayMove(move));
        }

        /// <summary>Para animações e a pensada da máquina (o resultado atrasado é descartado).</summary>
        private void StopTurnRoutine()
        {
            StopAllCoroutines();
            _session.CancelMachineThinking();
            _turnRoutine = null;
            _session.IsBusy = false;
            StopShake();
        }

        private void ShowEndCard(int winner)
        {
            int loser = 1 - winner;
            bool humanWon = _settings.mode != GameMode.Cpu || winner == 0;
            string title;
            string subtitle;

            if (_settings.mode == GameMode.Cpu)
            {
                string level = LevelLongNames[(int)_settings.level];
                title = winner == 0 ? "VOCÊ GANHOU!" : "TOMOU UM CALDO!";
                subtitle = winner == 0
                    ? $"O {level} tapou o último buraco e foi pro raso."
                    : $"Você tapou o último buraco. O {level} agradece.";
            }
            else
            {
                title = $"{PlayerName(winner)} GANHOU!";
                subtitle = $"{PlayerName(loser)} tapou o último buraco e levou um caldo.";
            }

            _end.Show(
                humanWon,
                title,
                subtitle,
                winner,
                PlayerName(0),
                PlayerName(1),
                _settings.score[0],
                _settings.score[1],
                _settings.reducedMotion);

            ShowScreen(ScreenId.End);

            if (humanWon)
            {
                _sound.Victory();
            }
            else
            {
                _sound.Defeat();
            }
        }

        // ------------------------------------------------------------------ render

        private void RenderBoard()
        {
            bool humanTurn = !_session.IsBusy && !_session.IsOver && !_session.IsMachineTurn;
            _game.Board.Render(
                _displayBoard,
                _session.SelectedMask,
                humanTurn,
                _settings.reducedMotion);

            _game.Hud.SetActivePlayer(_session.CurrentPlayer, _session.IsOver);
            _game.SetActions(_session.CanConfirm, _session.SelectedCount > 0 && !_session.IsBusy);
        }

        private void UpdateHint()
        {
            if (_session.IsOver)
            {
                return;
            }

            _game.SetHint(BuildHint());
        }

        private string BuildHint()
        {
            if (_session.IsBusy && _session.IsMachineTurn)
            {
                return $"<b>{LevelShortNames[(int)_settings.level]}</b> está pensando…";
            }

            if (_session.IsBusy)
            {
                return "Fshhh…";
            }

            if (_session.IsMachineTurn)
            {
                return $"Vez do <b>{LevelShortNames[(int)_settings.level]}</b>";
            }

            if (_notice.Length > 0)
            {
                return _notice;
            }

            int selected = _session.SelectedCount;
            if (selected == 0)
            {
                string who = _settings.mode == GameMode.Cpu ? "Sua vez" : $"Vez do <b>{PlayerName(_session.CurrentPlayer)}</b>";
                return _displayBoard.OpenCount == 1
                    ? $"{who} — <b>só sobrou um!</b>"
                    : $"{who}: tape buracos <b>em linha reta</b>";
            }

            if (selected == 1)
            {
                return "<b>1</b> buraco marcado — siga na horizontal ou na vertical";
            }

            return $"<b>{selected}</b> buracos na <b>{(_session.SelectionIsHorizontal ? "horizontal" : "vertical")}</b>";
        }

        /// <summary>Recado na dica até a próxima marcação (o <c>J.aviso</c>).</summary>
        private void ShowNotice(string message)
        {
            _notice = message;
            UpdateHint();
        }

        private string PlayerName(int index)
        {
            if (_settings.mode == GameMode.Cpu)
            {
                return index == 0 ? "VOCÊ" : LevelShortNames[(int)_settings.level];
            }

            return index == 0 ? "JOGADOR 1" : "JOGADOR 2";
        }

        private void Haptic()
        {
            if (!_settings.haptics)
            {
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        // ------------------------------------------------------------------ laço

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_screen == ScreenId.Play)
            {
                _game.Tick(Time.unscaledTime, dt);
            }
            else if (_endScreenVisible)
            {
                _end.Tick(dt);
            }

            TickShake(dt);

            HandleKeyboard();
        }

        /// <summary>
        /// D5 — tranco de 4 px em 120 ms com decaimento linear. Três idas e voltas no eixo X
        /// (meia no Y) e o alvo volta à posição EXATA no fim: nada de deriva acumulada quando
        /// a partida seguinte começa.
        /// </summary>
        private void TickShake(float dt)
        {
            if (_shakeTime < 0f)
            {
                return;
            }

            _shakeTime += dt;
            if (_shakeTime >= ShakeSeconds)
            {
                StopShake();
                return;
            }

            float t = _shakeTime / ShakeSeconds;
            float amplitude = UiKit.Css(ShakeAmplitudeCss) * (1f - t);
            float phase = t * Mathf.PI * 6f;
            _shakeRoot.anchoredPosition = new Vector2(
                _shakeHome.x + (Mathf.Sin(phase) * amplitude),
                _shakeHome.y + (Mathf.Cos(phase) * amplitude * 0.5f));
        }

        private void StopShake()
        {
            if (_shakeTime < 0f)
            {
                return;
            }

            _shakeTime = -1f;
            _shakeRoot.anchoredPosition = _shakeHome;
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _rules.SetVisible(false);
                _settingsModal.SetVisible(false);
                return;
            }

            if (_screen != ScreenId.Play)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                OnConfirm();
            }
            else if (Input.GetKeyDown(KeyCode.Backspace))
            {
                OnUndo();
            }
        }

        private void OnApplicationQuit()
        {
            SettingsStore.Save(_settings);
        }
    }
}
