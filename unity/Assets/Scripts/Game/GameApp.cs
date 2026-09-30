using System.Collections;
using System.Diagnostics;
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
        // Tempos do protótipo (index.html §4): um buraco a cada 115 ms, a areia cai 150 ms
        // depois da pazinha, e a vez só passa 430 ms após o último buraco.
        private const float HoleStepSeconds = 0.115f;
        private const float SandFallSeconds = 0.150f;
        private const float TurnTailSeconds = 0.430f;
        private const float MachineThinkSeconds = 0.260f;
        private const float MachineMinTurnSeconds = 0.420f;
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
        private Image _backdrop;
        private Image _grain;
        private Image _halftone;
        private FrameDecor _frame;

        private TitleScreen _title;
        private SetupScreen _setup;
        private GameScreen _game;
        private EndScreen _end;
        private RulesModal _rules;

        private readonly int[] _moveIndices = new int[Rules.RowCount];
        private readonly WaitForSeconds _waitHoleStep = new WaitForSeconds(HoleStepSeconds);
        private readonly WaitForSeconds _waitSandFall = new WaitForSeconds(SandFallSeconds);
        private readonly WaitForSeconds _waitTurnTail = new WaitForSeconds(TurnTailSeconds);
        private readonly WaitForSeconds _waitThink = new WaitForSeconds(MachineThinkSeconds);
        private readonly WaitForSeconds _waitEndCard = new WaitForSeconds(EndCardDelaySeconds);
        private readonly WaitForSeconds _waitFirstMove = new WaitForSeconds(FirstMachineMoveSeconds);

        private Board _displayBoard = Board.Dug;
        private Coroutine _turnRoutine;
        private bool _endScreenVisible;
        private float _pendingHintTimer;

        /// <summary>Telas do app (o <c>.tela.ativa</c> do protótipo).</summary>
        private enum ScreenId
        {
            Title = 0,
            Setup = 1,
            Play = 2,
            End = 3,
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

            _title = new TitleScreen(appArea);
            _setup = new SetupScreen(appArea, _settings);
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

            _title.PlayClicked += OnTitlePlay;
            _title.RulesClicked += OnOpenRules;

            _setup.Changed += OnSetupChanged;
            _setup.StartClicked += OnStartGame;
            _setup.BackClicked += () => ShowScreen(ScreenId.Title);

            _game.Board.HoleClicked += OnHoleClicked;
            _game.ConfirmClicked += OnConfirm;
            _game.UndoClicked += OnUndo;
            _game.Hud.SoundClicked += OnToggleAllSound;
            _game.Hud.SkinClicked += OnToggleSkin;
            _game.Hud.RulesClicked += OnOpenRules;
            _game.Hud.MenuClicked += () => ShowScreen(ScreenId.Setup);

            _end.AgainClicked += OnStartGame;
            _end.MenuClicked += () => ShowScreen(ScreenId.Setup);

            _rules.Closed += () => _rules.SetVisible(false);

            Palette.SkinChanged += OnSkinChanged;
            OnSkinChanged();
        }

        // ------------------------------------------------------------------ telas

        private void ShowScreen(ScreenId screen)
        {
            _screen = screen;
            _title.SetVisible(screen == ScreenId.Title);
            _setup.SetVisible(screen == ScreenId.Setup);
            _game.SetVisible(screen == ScreenId.Play);
            _end.SetVisible(screen == ScreenId.End);
            _endScreenVisible = screen == ScreenId.End;

            if (screen == ScreenId.Setup)
            {
                _setup.Refresh();
            }
        }

        private void OnSkinChanged()
        {
            _settings.skin = Palette.Skin;
            _backdrop.sprite = Palette.IsPaper ? SpriteFactory.Hatch(64, 94f, 18, 2) : UiKit.Art("cena-praia");
            _backdrop.color = Palette.IsPaper ? Palette.PapelClaro : Color.white;
            _backdrop.type = Palette.IsPaper ? Image.Type.Tiled : Image.Type.Simple;
            _grain.color = Palette.Tinta.WithAlpha(Palette.IsPaper ? 0.24f : 0.16f);

            _title.ApplySkin();
            _setup.ApplySkin();
            _game.ApplySkin();
            _end.ApplySkin();
            _rules.ApplySkin();
            _frame.ApplySkin();
        }

        // ------------------------------------------------------------------ menus

        private void OnTitlePlay()
        {
            _sound.Tap(true);
            ShowScreen(ScreenId.Setup);
        }

        private void OnOpenRules()
        {
            _sound.Tap(true);
            _rules.SetVisible(true);
        }

        private void OnSetupChanged()
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
            _setup.Refresh();
            SettingsStore.Save(_settings);
        }

        private void OnToggleSkin()
        {
            Palette.SetSkin(Palette.IsPaper ? Skin.Praia : Skin.Papel);
            _settings.skin = Palette.Skin;
            _setup.Refresh();
            _sound.Tap(true);
            SettingsStore.Save(_settings);
        }

        // ------------------------------------------------------------------ partida

        private void OnStartGame()
        {
            StopTurnRoutine();
            _session.NewGame();
            _session.PrewarmSolver();
            _displayBoard = _session.Board;

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

        private void OnHoleClicked(int row, int index)
        {
            if (_session.IsBusy || _session.IsOver || _session.IsMachineTurn)
            {
                return;
            }

            SelectionResult result = _session.ToggleHole(row, index);
            switch (result)
            {
                case SelectionResult.Added:
                    _sound.Tap(true);
                    break;
                case SelectionResult.Removed:
                case SelectionResult.RowChanged:
                    _sound.Tap(false);
                    break;
                case SelectionResult.RejectedNotAdjacent:
                    _sound.Error();
                    _game.Board.PlayReject(row, index);
                    FlashHint("Nessa variante só valem buracos <b>vizinhos</b>");
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

            _session.ClearSelection();
            _sound.Tap(false);
        }

        private void OnConfirm()
        {
            if (!_session.CanConfirm)
            {
                return;
            }

            Move move = _session.BuildSelectedMove();
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
            _session.ClearSelection();
            RenderBoard();
            UpdateHint();

            // WriteIndices devolve os buracos em ordem crescente; o buffer é reaproveitado
            // porque corrotina não aceita stackalloc.
            int written = move.WriteIndices(_moveIndices);

            for (int k = 0; k < written; k++)
            {
                int hole = _moveIndices[k];
                Vector2 center = _game.Board.HoleCenter(move.Row, hole);
                _game.Board.Fx.Dig(center, _game.Board.Unit, _settings.reducedMotion);
                _sound.Sand(1f - k * 0.06f);
                Haptic();

                StartCoroutine(CoverAfterDelay(move.Row, hole));
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

        private IEnumerator CoverAfterDelay(int row, int index)
        {
            yield return _waitSandFall;
            _displayBoard = _displayBoard.Cover(row, index);
            RenderBoard();
            _game.Board.PlayCovered(row, index);
            UpdateHint();
        }

        /// <summary>Pensada da máquina: pausa curta, escolhe e mantém um tempo mínimo de "suspense".</summary>
        private IEnumerator MachineTurn(bool firstMoveOfGame)
        {
            _session.IsBusy = true;
            RenderBoard();
            UpdateHint();

            if (firstMoveOfGame)
            {
                yield return _waitFirstMove;
            }

            yield return _waitThink;

            var watch = Stopwatch.StartNew();
            Move move = _session.ChooseMachineMove();
            watch.Stop();

            float spent = (float)watch.Elapsed.TotalSeconds;
            float wait = Mathf.Max(0f, MachineMinTurnSeconds - spent);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            _session.IsBusy = false;
            _turnRoutine = StartCoroutine(PlayMove(move));
        }

        private void StopTurnRoutine()
        {
            StopAllCoroutines();
            _turnRoutine = null;
            _session.IsBusy = false;
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
                _session.SelectedRow,
                _session.SelectedHoles,
                humanTurn,
                _settings.reducedMotion);

            _game.Hud.SetActivePlayer(_session.CurrentPlayer, _session.IsOver);
            _game.SetActions(_session.CanConfirm, _session.SelectedHoles != 0u && !_session.IsBusy);
        }

        private void UpdateHint()
        {
            if (_session.IsOver || _pendingHintTimer > 0f)
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

            string who = _settings.mode == GameMode.Cpu ? "Sua vez" : $"Vez do <b>{PlayerName(_session.CurrentPlayer)}</b>";
            int selected = _session.SelectedCount;
            if (selected == 0)
            {
                return _displayBoard.OpenCount == 1
                    ? $"{who} — <b>só sobrou um!</b>"
                    : $"{who}: escolha buracos de <b>uma fileira</b>";
            }

            string plural = selected > 1 ? "s" : string.Empty;
            return $"Fileira <b>{_session.SelectedRow + 1}</b> — <b>{selected}</b> buraco{plural} marcado{plural}";
        }

        private void FlashHint(string message)
        {
            _pendingHintTimer = 1.6f;
            _game.SetHint(message);
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

            if (_pendingHintTimer > 0f)
            {
                _pendingHintTimer -= dt;
                if (_pendingHintTimer <= 0f)
                {
                    UpdateHint();
                }
            }

            if (_screen == ScreenId.Play)
            {
                _game.Tick(Time.unscaledTime, dt);
            }
            else if (_endScreenVisible)
            {
                _end.Tick(dt);
            }

            HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _rules.SetVisible(false);
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
