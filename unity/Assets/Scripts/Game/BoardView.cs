using System;
using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// A caixa de areia com o tabuleiro em escada invertida (7 buracos no topo, 1 embaixo,
    /// alinhada à esquerda) — o <c>.areia-box</c> + <c>#tabuleiro</c> do protótipo.
    ///
    /// Por que tudo posicionado na mão, sem LayoutGroup: o protótipo calcula uma única medida
    /// (<c>--u</c>, o diâmetro do buraco) e deriva TODO o resto dela. Reproduzir isso com flex
    /// do uGUI custaria três níveis de layout e um rebuild por quadro; aqui é uma conta de
    /// meia dúzia de multiplicações, feita só quando a caixa muda de tamanho.
    ///
    /// Hierarquia:
    /// <code>
    /// areia-box            (este MonoBehaviour)
    ///  ├─ sombra           box-shadow 0 5px 0
    ///  ├─ recorte          Mask arredondada de 12px (overflow:hidden)
    ///  │   ├─ areia        SandBed (branco translúcido na skin Papel)
    ///  │   ├─ espuma       faixa da beira d'água no topo
    ///  │   ├─ tabuleiro    7 fileiras de estaca + buracos
    ///  │   └─ fx           FxLayer (pazinha, poeira, grãos)
    ///  └─ borda            traço de tinta de 3px
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        /// <summary>Raio dos cantos da caixa de areia, em CSS px.</summary>
        private const float BoxRadiusCss = 12f;

        /// <summary>Espessura do traço de tinta da caixa, em CSS px.</summary>
        private const float BoxBorderCss = 3f;

        /// <summary>Queda da sombra dura da caixa, em CSS px.</summary>
        private const float BoxShadowCss = 5f;

        /// <summary>Largura da estaca em múltiplos de <c>--u</c> (<c>--estaca</c> do CSS).</summary>
        private const float StakeWidth = 1.15f;

        /// <summary>Altura da estaca em múltiplos de <c>--u</c>.</summary>
        private const float StakeHeight = 1.08f;

        /// <summary>Folga entre buracos e entre fileiras (<c>--gap</c> do CSS).</summary>
        private const float GapRatio = 0.30f;

        /// <summary>Divisor de largura do <c>ajustaEscala</c>: estaca + 7 buracos + 7 folgas.</summary>
        private const float WidthDivisor = StakeWidth + 7f + (7f * GapRatio);

        /// <summary>Divisor de altura do <c>ajustaEscala</c>: 7 buracos + 6 folgas.</summary>
        private const float HeightDivisor = 7f + (6f * GapRatio);

        /// <summary>Piso do <c>--u</c>, em CSS px.</summary>
        private const float UnitMinCss = 20f;

        /// <summary>Teto do <c>--u</c>, em CSS px.</summary>
        private const float UnitMaxCss = 76f;

        /// <summary>Opacidade mais baixa do keyframe <c>cintila</c> aplicada ao anel de perigo.</summary>
        private const float BlinkLow = 0.25f;

        /// <summary>Opacidade mais alta do keyframe <c>cintila</c> (é também o estado final).</summary>
        private const float BlinkHigh = 0.85f;

        /// <summary>Números das estacas pré-alocados (o tamanho da fileira): nada de <c>ToString()</c> em laço.</summary>
        private static readonly string[] RowSizes = { "1", "2", "3", "4", "5", "6", "7" };

        private readonly HoleView[] _holes = new HoleView[Rules.HoleCount];
        private readonly Vector2[] _holeCenters = new Vector2[Rules.HoleCount];
        private readonly RectTransform[] _stakes = new RectTransform[Rules.RowCount];
        private readonly Image[] _stakeArt = new Image[Rules.RowCount];
        private readonly Text[] _stakeNumber = new Text[Rules.RowCount];
        private readonly Text[] _stakeParen = new Text[Rules.RowCount];

        private RectTransform _root;
        private RectTransform _clip;
        private RectTransform _boardRoot;
        private Image _shadow;
        private Image _clipImage;
        private Image _sand;
        private Image _espuma;
        private Image _border;
        private FxLayer _fx;

        private Action<int, int> _onHoleClicked;
        private float _unit;
        private float _lastWidth = -1f;
        private float _lastHeight = -1f;
        private float _phase;
        private bool _reducedMotion;
        private bool _hasSelected;
        private bool _hasDanger;

        /// <summary>Nó raiz da caixa de areia.</summary>
        public RectTransform Root => _root;

        /// <summary>Camada de efeitos sobre a areia.</summary>
        public FxLayer Fx => _fx;

        /// <summary>O <c>--u</c> do CSS já convertido para unidades do canvas.</summary>
        public float Unit => _unit;

        /// <summary>Toque num buraco: (fileira, coluna).</summary>
        public event Action<int, int> HoleClicked;

        // ------------------------------------------------------------------ fábrica

        /// <summary>Monta a caixa de areia inteira sob <paramref name="parent"/>.</summary>
        public static BoardView Create(Transform parent)
        {
            RectTransform root = UiKit.Stretch(parent, "areia-box");
            var view = root.gameObject.AddComponent<BoardView>();
            view.Build(root);
            return view;
        }

        private void Build(RectTransform root)
        {
            _root = root;

            // `.areia-box{flex:1;min-height:0}` — se o pai tiver layout, a caixa estica.
            UiKit.Flexible(this, 1f);

            int radius = Mathf.RoundToInt(UiKit.Css(BoxRadiusCss));
            Sprite box = SpriteFactory.RoundedRect(Mathf.Max(48, radius * 3), radius);

            // box-shadow: 0 5px 0 rgba(26,26,26,.28)
            _shadow = UiKit.Picture(_root, "sombra", box, Palette.Ink.WithAlpha(0.28f));
            UiKit.FillParent((RectTransform)_shadow.transform);
            ((RectTransform)_shadow.transform).anchoredPosition = new Vector2(0f, -UiKit.Css(BoxShadowCss));

            // `overflow:hidden` com cantos arredondados: a máscara não é pintada, só recorta.
            _clip = UiKit.Stretch(_root, "recorte");
            _clipImage = _clip.gameObject.AddComponent<Image>();
            _clipImage.sprite = box;
            _clipImage.type = Image.Type.Sliced;
            _clipImage.color = Color.white;
            _clipImage.raycastTarget = false;
            var mask = _clip.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _sand = UiKit.Picture(_clip, "areia", SpriteFactory.SandBed(256, 256), Color.white);
            UiKit.FillParent((RectTransform)_sand.transform);

            // `.espuma{top:-6px;left:-5%;width:110%;height:26px;opacity:.75}`
            _espuma = UiKit.Picture(_clip, "espuma", UiKit.Art("espuma"), new Color(1f, 1f, 1f, 0.75f));
            var espumaRt = (RectTransform)_espuma.transform;
            espumaRt.anchorMin = new Vector2(-0.05f, 1f);
            espumaRt.anchorMax = new Vector2(1.05f, 1f);
            espumaRt.pivot = new Vector2(0.5f, 1f);
            espumaRt.sizeDelta = new Vector2(0f, UiKit.Css(26f));
            espumaRt.anchoredPosition = new Vector2(0f, UiKit.Css(6f));

            _boardRoot = UiKit.Node(_clip, "tabuleiro");

            _onHoleClicked = RaiseHoleClicked;
            for (int row = 0; row < Rules.RowCount; row++)
            {
                BuildStake(row);
                for (int i = 0; i < Board.RowLength(row); i++)
                {
                    var hole = new HoleView();
                    hole.Build(_boardRoot, row, i, _onHoleClicked);
                    _holes[Board.RowOffset(row) + i] = hole;
                }
            }

            _fx = FxLayer.Create(_clip);

            // Traço de tinta por cima de tudo (fica FORA da máscara para não ser comido por ela).
            _border = UiKit.Picture(
                _root,
                "borda",
                SpriteFactory.RoundedOutline(Mathf.Max(48, radius * 3), radius, Mathf.RoundToInt(UiKit.Css(BoxBorderCss))),
                Palette.Ink);
            UiKit.FillParent((RectTransform)_border.transform);

            ApplySkin();
        }

        /// <summary>Estaca à esquerda da fileira com o tamanho dela (<c>svgEstaca()</c> + <c>&lt;span&gt;</c>).</summary>
        private void BuildStake(int row)
        {
            RectTransform stake = UiKit.Node(_boardRoot, "estaca");
            _stakes[row] = stake;

            // O SVG do protótipo usa preserveAspectRatio="none": a madeira é esticada mesmo.
            _stakeArt[row] = UiKit.Picture(stake, "madeira", UiKit.Art("estaca"), Color.white);
            UiKit.FillParent((RectTransform)_stakeArt[row].transform);

            Text number = UiKit.Label(stake, "numero", RowSizes[Board.RowLength(row) - 1], UiKit.Sign, 24, Palette.Creme);
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.FillParent((RectTransform)number.transform);
            _stakeNumber[row] = number;

            // `body.skin-papel .estaca::after{content:")";right:8%;opacity:.7}`
            Text paren = UiKit.Label(stake, "parentese", ")", UiKit.BodyItalic, 24, Palette.Caneta.WithAlpha(0.7f), TextAnchor.MiddleRight);
            paren.horizontalOverflow = HorizontalWrapMode.Overflow;
            var parenRt = (RectTransform)paren.transform;
            UiKit.FillParent(parenRt);
            paren.enabled = false;
            _stakeParen[row] = paren;
        }

        private void RaiseHoleClicked(int row, int index) => HoleClicked?.Invoke(row, index);

        // ------------------------------------------------------------------ ciclo

        private void OnEnable() => Palette.SkinChanged += ApplySkin;

        private void OnDisable() => Palette.SkinChanged -= ApplySkin;

        private void Update()
        {
            // `window.addEventListener("resize", ajustaEscala)`: aqui basta comparar o retângulo.
            Rect rect = _root.rect;
            if (rect.width != _lastWidth || rect.height != _lastHeight)
            {
                _lastWidth = rect.width;
                _lastHeight = rect.height;
                Relayout();
            }

            float dt = Time.unscaledDeltaTime;

            // Fases compartilhadas dos keyframes `pulsa` (1 s) e `cintila` (1.2 s): uma conta
            // por quadro para os 28 buracos, em vez de 28 contas.
            float pulse = 1f;
            float blink = BlinkHigh;
            if (!_reducedMotion && (_hasSelected || _hasDanger))
            {
                _phase += dt;
                if (_phase >= 6f)
                {
                    _phase -= 6f;       // 6 s é múltiplo comum de 1 s e 1.2 s: emenda sem salto
                }

                float wave = 0.5f - (0.5f * Mathf.Cos(_phase * (Mathf.PI * 2f)));
                pulse = 1f + (0.07f * wave);                                // 0%,100% → 1; 50% → 1.07
                float blinkWave = 0.5f - (0.5f * Mathf.Cos(_phase * (Mathf.PI * 2f / 1.2f)));
                blink = Mathf.Lerp(BlinkLow, BlinkHigh, blinkWave);
            }

            for (int k = 0; k < _holes.Length; k++)
            {
                _holes[k].Tick(dt, pulse, blink);
            }
        }

        // ------------------------------------------------------------------ escala

        /// <summary>
        /// Porte do <c>ajustaEscala()</c>: uma única medida manda em tudo.
        /// <c>u = clamp(20, 76, min((L-16)/(1.15+7+7*.30), (A-14)/(7+6*.30)))</c>, em CSS px.
        /// </summary>
        private void Relayout()
        {
            if (_lastWidth <= 0f || _lastHeight <= 0f)
            {
                return;
            }

            float usableWidth = _lastWidth - UiKit.Css(16f);
            float usableHeight = _lastHeight - UiKit.Css(14f);
            float byWidth = usableWidth / WidthDivisor;
            float byHeight = usableHeight / HeightDivisor;
            float unit = Mathf.Clamp(Mathf.Min(byWidth, byHeight), UiKit.Css(UnitMinCss), UiKit.Css(UnitMaxCss));

            if (Mathf.Approximately(unit, _unit))
            {
                return;
            }

            _unit = unit;
            PlaceHoles();
        }

        /// <summary>Recoloca estacas e buracos a partir do <c>--u</c> recém-calculado.</summary>
        private void PlaceHoles()
        {
            float unit = _unit;
            float gap = unit * GapRatio;
            float stakeW = unit * StakeWidth;
            float stakeH = unit * StakeHeight;
            float pitch = unit + gap;
            float totalHeight = (7f * unit) + (6f * gap);
            float totalWidth = stakeW + (7f * unit) + (7f * gap);
            float left = -totalWidth * 0.5f;
            _boardRoot.sizeDelta = new Vector2(totalWidth, totalHeight);

            int numberSize = Mathf.Max(1, Mathf.RoundToInt(unit * 0.52f));

            for (int row = 0; row < Rules.RowCount; row++)
            {
                int count = Board.RowLength(row);

                // `#tabuleiro{align-items:flex-start}` + `.fileira{justify-content:flex-start}`: escada
                // alinhada à esquerda, colunas retas — é o que dá sentido à jogada na vertical.
                float y = (totalHeight * 0.5f) - (unit * 0.5f) - (row * pitch);

                RectTransform stake = _stakes[row];
                stake.sizeDelta = new Vector2(stakeW, stakeH);
                stake.anchoredPosition = new Vector2(left + (stakeW * 0.5f), y);

                Text number = _stakeNumber[row];
                number.fontSize = numberSize;
                // `.estaca span{transform:translateY(-4%)}` — o número sobe um tico na madeira.
                ((RectTransform)number.transform).anchoredPosition = new Vector2(0f, stakeH * 0.04f);

                Text paren = _stakeParen[row];
                paren.fontSize = numberSize;
                ((RectTransform)paren.transform).anchoredPosition = new Vector2(-stakeW * 0.08f, 0f);

                float x = left + stakeW + gap + (unit * 0.5f);
                for (int i = 0; i < count; i++)
                {
                    int global = Board.RowOffset(row) + i;
                    var center = new Vector2(x, y);
                    _holeCenters[global] = center;

                    HoleView hole = _holes[global];
                    hole.SetUnit(unit);
                    hole.SetHome(center);

                    x += pitch;
                }
            }
        }

        // ------------------------------------------------------------------ render

        /// <summary>
        /// Espelha o estado da partida no tabuleiro. Idempotente e sem alocação: cada
        /// <see cref="HoleView"/> compara antes de tocar em qualquer malha.
        /// </summary>
        /// <param name="selectedMask">Buracos marcados (máscara global).</param>
        /// <param name="interactive">true quando é a vez de um humano e nada está em curso.</param>
        /// <param name="reducedMotion">Corta pulsos e cintilações.</param>
        public void Render(in Board board, uint selectedMask, bool interactive, bool reducedMotion)
        {
            _reducedMotion = reducedMotion;

            // `.perigo`: quando sobra um único buraco no tabuleiro inteiro.
            bool lastOne = board.OpenCount == 1;
            bool anySelected = false;
            bool anyDanger = false;

            for (int row = 0; row < Rules.RowCount; row++)
            {
                int count = Board.RowLength(row);

                for (int i = 0; i < count; i++)
                {
                    int global = Board.Index(row, i);
                    bool open = board.IsOpen(global);
                    bool selected = open && (selectedMask & (1u << global)) != 0u;
                    bool danger = open && lastOne;
                    bool playable = open && interactive;

                    _holes[global].Apply(open, selected, danger, playable, reducedMotion);

                    anySelected |= selected;
                    anyDanger |= danger;
                }
            }

            // Em reducedMotion o Update já usa pulso 1 e anel de perigo no pico (o estado
            // final dos keyframes), então não há nada a "assentar" aqui.

            _hasSelected = anySelected;
            _hasDanger = anyDanger;
        }

        /// <summary>Tremida do keyframe <c>nega</c>: buraco tapado no caminho da linha.</summary>
        public void PlayReject(int row, int index)
        {
            HoleView hole = Find(row, index);
            if (hole != null)
            {
                hole.Shake();
            }
        }

        /// <summary>Salto do keyframe <c>plop</c>: o montinho acabou de cair no buraco.</summary>
        public void PlayCovered(int row, int index)
        {
            HoleView hole = Find(row, index);
            if (hole != null)
            {
                hole.Plop();
            }
        }

        /// <summary>Centro do buraco em coordenadas locais da camada de efeitos.</summary>
        public Vector2 HoleCenter(int row, int index)
        {
            if (!Board.Exists(row, index))
            {
                return Vector2.zero;
            }

            // `_boardRoot` está centrado no recorte, e a camada de FX cobre o recorte inteiro
            // com pivô central: a posição do buraco já está no mesmo sistema de coordenadas.
            return _holeCenters[Board.RowOffset(row) + index];
        }

        // ------------------------------------------------------------------ skin

        /// <summary>Repinta a caixa e os 28 buracos na skin ativa.</summary>
        public void ApplySkin()
        {
            bool paper = Palette.IsPaper;

            if (paper)
            {
                // `body.skin-papel .areia-box{background:rgba(255,255,255,.22)}`
                _sand.sprite = SpriteFactory.Solid();
                _sand.color = new Color(1f, 1f, 1f, 0.22f);
                _espuma.enabled = false;             // `body.skin-papel .espuma{display:none}`
            }
            else
            {
                _sand.sprite = SpriteFactory.SandBed(256, 256);
                _sand.color = Color.white;
                _espuma.enabled = true;
            }

            _shadow.color = Palette.Ink.WithAlpha(paper ? 0.35f : 0.28f);
            _border.color = Palette.Ink;

            for (int row = 0; row < Rules.RowCount; row++)
            {
                // Na skin Papel a estaca some e sobra só o número itálico com ")" (CSS 493–495).
                _stakeArt[row].enabled = !paper;
                _stakeParen[row].enabled = paper;

                Text number = _stakeNumber[row];
                number.font = paper ? UiKit.BodyItalic : UiKit.Sign;
                number.color = paper ? Palette.Caneta : Palette.Creme;
                _stakeParen[row].color = Palette.Caneta.WithAlpha(0.7f);
            }

            for (int k = 0; k < _holes.Length; k++)
            {
                _holes[k].ApplySkin();
            }
        }

        // ------------------------------------------------------------------ interno

        private HoleView Find(int row, int index)
        {
            if (!Board.Exists(row, index))
            {
                return null;
            }

            return _holes[Board.RowOffset(row) + index];
        }
    }
}
