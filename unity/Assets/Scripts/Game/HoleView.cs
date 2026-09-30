using System;
using TapaBuraco.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Um buraco cavado na areia — o <c>&lt;button class="buraco"&gt;</c> do protótipo.
    ///
    /// NÃO é MonoBehaviour de propósito: são 28 buracos na tela e 28 <c>Update()</c> por quadro
    /// seria desperdício puro. Quem dá o "tique" das animações é o <see cref="BoardView"/>, que
    /// já calcula uma vez por quadro as fases compartilhadas (pulso e cintilação) e as repassa.
    ///
    /// Camadas, de trás para frente (mesma ordem de pintura do CSS):
    /// <list type="number">
    ///   <item><c>_rim</c> — o <c>.buraco::before</c>, areia empurrada para fora (só na skin Praia).</item>
    ///   <item><c>_danger</c> — anel de perigo do último buraco (<c>.buraco.perigo::before</c>).</item>
    ///   <item><c>_core</c> — cava + anel de seleção; é este nó que recebe o keyframe <c>pulsa</c>.</item>
    ///   <item><c>_monte</c> — montinho de areia do buraco tapado, com a hachura da skin Papel.</item>
    /// </list>
    /// O próprio nó raiz carrega a <see cref="Image"/> invisível que recebe o toque.
    /// </summary>
    public sealed class HoleView
    {
        /// <summary>Transição do montinho: <c>transition:opacity .18s, transform .18s</c>.</summary>
        private const float CoverSeconds = 0.18f;

        /// <summary>Keyframe <c>nega</c>: 0.3 s de tremida lateral quando a jogada é recusada.</summary>
        private const float ShakeSeconds = 0.30f;

        /// <summary>Keyframe <c>plop</c>: 0.4 s de salto ao tapar o buraco (classe <c>.recem</c>).</summary>
        private const float PlopSeconds = 0.40f;

        /// <summary>Amplitude da tremida em CSS px (<c>translateX(±5px)</c>).</summary>
        private const float ShakeCssPixels = 5f;

        /// <summary>Espessura do rabisco de caneta da skin Papel, em texels do sprite.</summary>
        private const int SketchThickness = 6;

        /// <summary>Lado da textura dos rabiscos — um por buraco, então vale manter modesto.</summary>
        private const int SketchSize = 96;

        // D3 — o anel de seleção sozinho lia a 2,00 contra a areia. Vira anel DUPLO: 3 CSS px
        // de coral colados no buraco e 2 CSS px de tinta por fora (tinta × areia = 11,89).
        // O halo translúcido que existia por fora saiu.
        private const float SelInnerCss = 3f;
        private const float SelOuterCss = 2f;

        /// <summary>Espessura em texels do anel interno numa textura de 128 (≈3 CSS px).</summary>
        private const int SelInnerThickness = 9;

        /// <summary>Espessura em texels do anel externo numa textura de 128 (≈2 CSS px).</summary>
        private const int SelOuterThickness = 5;

        private RectTransform _root;
        private RectTransform _core;
        private RectTransform _monteRt;
        private RectTransform _selRt;
        private RectTransform _selOutRt;
        private RectTransform _rimRt;
        private RectTransform _dangerRt;
        private RectTransform _hatchRt;

        private Image _hit;
        private Image _rim;
        private Image _danger;
        private Image _cava;
        private Image _sel;
        private Image _selOut;
        private Image _monte;
        private Image _hatch;
        private Button _button;

        private Color _cavaBase = Color.white;
        private Color _monteBase = Color.white;
        private Color _selBase = Color.white;
        private Color _dangerBase = Color.white;

        private int _seed;
        private float _unit;
        private Vector2 _home;

        /// <summary>0 = buraco aberto, 1 = montinho no lugar. Anda sozinho em <see cref="Tick"/>.</summary>
        private float _cover;

        private float _shake = -1f;
        private float _plop = -1f;

        private bool _open = true;
        private bool _selected;
        private bool _dangerOn;
        private bool _playable;
        private bool _reduced;
        private bool _paper;

        /// <summary>Nó raiz — posicionado pelo <see cref="BoardView"/>.</summary>
        public RectTransform Root => _root;

        /// <summary>Botão do buraco; desligado quando tapado ou fora do turno humano.</summary>
        public Button Button => _button;

        // ------------------------------------------------------------------ montagem

        /// <summary>Monta a hierarquia do buraco sob <paramref name="parent"/>.</summary>
        /// <param name="onClick">Recebe (fileira, índice) a cada toque válido.</param>
        public void Build(Transform parent, int row, int index, Action<int, int> onClick)
        {
            // Semente estável por buraco: garante que o rabisco da skin Papel nunca mude de forma,
            // nem quando a skin vai e volta.
            _seed = Board.RowOffset(row) + index;

            _root = UiKit.Node(parent, "buraco");

            // A área de toque é o próprio nó raiz: um quadrado invisível do tamanho do buraco.
            _hit = _root.gameObject.AddComponent<Image>();
            _hit.sprite = SpriteFactory.Solid();
            _hit.color = new Color(1f, 1f, 1f, 0f);
            _hit.raycastTarget = true;

            // 1. areia empurrada para fora — o ::before, que no CSS pinta ATRÁS da cava.
            _rim = UiKit.Picture(_root, "rim", SpriteFactory.SoftDisc(96, 2.2f), new Color(1f, 1f, 1f, 0.35f));
            _rimRt = Center(_rim);

            // 2. anel de perigo do último buraco do tabuleiro.
            _danger = UiKit.Picture(_root, "perigo", SpriteFactory.Ring(128, 6), Color.clear);
            _dangerRt = Center(_danger);
            _danger.enabled = false;

            // 3. miolo: cava + anel de seleção. Escala junto no keyframe `pulsa`.
            _core = UiKit.Node(_root, "miolo");
            _cava = UiKit.Picture(_core, "cava", SpriteFactory.Cava(128), Color.white);
            UiKit.FillParent((RectTransform)_cava.transform);
            _sel = UiKit.Picture(_core, "selecao", SpriteFactory.Ring(128, SelInnerThickness), Color.clear);
            _selRt = Center(_sel);
            _sel.enabled = false;

            // Anel externo de tinta: desenhado por fora do coral, é ele que garante o salto
            // contra a areia. Fica no mesmo `_core` para pulsar junto.
            _selOut = UiKit.Picture(_core, "selecao-tinta", SpriteFactory.Ring(128, SelOuterThickness), Color.clear);
            _selOutRt = Center(_selOut);
            _selOut.enabled = false;

            // 4. montinho de areia fofa por cima do buraco tapado.
            _monte = UiKit.Picture(_root, "monte", SpriteFactory.Monte(128), new Color(1f, 1f, 1f, 0f));
            _monteRt = Center(_monte);
            _monte.enabled = false;
            _hatch = UiKit.Picture(_monteRt, "hachura", SpriteFactory.Hatch(64, 56f, 5, 2), Color.clear);
            _hatch.type = Image.Type.Tiled;
            _hatchRt = Center(_hatch);
            _hatch.enabled = false;

            _button = _root.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = _hit;
            int r = row;
            int i = index;
            _button.onClick.AddListener(() => onClick(r, i));

            ApplySkin();
        }

        // ------------------------------------------------------------------ geometria

        /// <summary>Reaplica todos os tamanhos a partir do <c>--u</c> do CSS.</summary>
        public void SetUnit(float unit)
        {
            if (Mathf.Approximately(_unit, unit))
            {
                return;
            }

            _unit = unit;
            _root.sizeDelta = new Vector2(unit, unit);

            // `.buraco::before{inset:-9%}` → 1.18× o diâmetro.
            float rim = unit * 1.18f;
            _rimRt.sizeDelta = new Vector2(rim, rim);
            _dangerRt.sizeDelta = new Vector2(rim, rim);

            _core.sizeDelta = new Vector2(unit, unit);

            // Anel interno: 3 CSS px colados na borda do buraco (sobra 3 de cada lado).
            float ring = unit + (UiKit.Css(SelInnerCss) * 2f);
            _selRt.sizeDelta = new Vector2(ring, ring);

            // Anel externo: 2 CSS px logo depois do coral, sem folga entre os dois.
            float ringOut = ring + (UiKit.Css(SelOuterCss) * 2f);
            _selOutRt.sizeDelta = new Vector2(ringOut, ringOut);

            // `.buraco .monte{inset:-6%}` → 1.12× (na skin Papel o CSS volta para inset:0).
            float monte = Palette.IsPaper ? unit : unit * 1.12f;
            _monteRt.sizeDelta = new Vector2(monte, monte);

            // Quadrado inscrito no montinho: a hachura precisa caber dentro do rabisco redondo.
            float hatch = unit * 0.72f;
            _hatchRt.sizeDelta = new Vector2(hatch, hatch);
        }

        /// <summary>Guarda a posição de repouso; a tremida soma um deslocamento sobre ela.</summary>
        public void SetHome(Vector2 position)
        {
            _home = position;
            if (_shake < 0f)
            {
                _root.anchoredPosition = position;
            }
        }

        // ------------------------------------------------------------------ estado

        /// <summary>
        /// Aplica o estado do buraco. Sai cedo quando nada mudou: o <c>Render</c> do jogo chama
        /// isto 28 vezes por lance e não pode sujar nenhuma malha à toa.
        /// </summary>
        public void Apply(bool open, bool selected, bool danger, bool playable, bool reducedMotion)
        {
            _reduced = reducedMotion;

            if (_open != open)
            {
                _open = open;
                if (reducedMotion)
                {
                    // Sem animações longas: o montinho simplesmente aparece.
                    _cover = open ? 0f : 1f;
                    _plop = -1f;
                }

                RefreshCover();
            }

            if (_selected != selected)
            {
                _selected = selected;
                _sel.enabled = selected;
                _sel.color = selected ? _selBase : Color.clear;
                _selOut.enabled = selected;
                _selOut.color = selected ? Palette.Ink : Color.clear;
                if (!selected)
                {
                    _core.localScale = Vector3.one;
                }
            }

            if (_dangerOn != danger)
            {
                _dangerOn = danger;
                _danger.enabled = danger;
                if (!danger)
                {
                    _danger.color = Color.clear;
                }
            }

            if (_playable != playable)
            {
                _playable = playable;
                _button.interactable = playable;
            }
        }

        /// <summary>Tremida do keyframe <c>nega</c> — jogada recusada pela variante Vizinhos.</summary>
        public void Shake() => _shake = 0f;

        /// <summary>Salto do keyframe <c>plop</c> — o montinho acabou de cair no buraco.</summary>
        public void Plop()
        {
            _cover = 1f;
            if (_reduced)
            {
                _plop = -1f;
                RefreshCover();
                return;
            }

            _plop = 0f;
            RefreshCover();
        }

        /// <summary>
        /// Avança as animações. <paramref name="pulse"/> e <paramref name="blink"/> chegam
        /// prontos do <see cref="BoardView"/> — são as fases compartilhadas dos keyframes
        /// <c>pulsa</c> (1 s) e <c>cintila</c> (1.2 s).
        /// </summary>
        public void Tick(float deltaTime, float pulse, float blink)
        {
            float target = _open ? 0f : 1f;
            if (!Mathf.Approximately(_cover, target))
            {
                _cover = Mathf.MoveTowards(_cover, target, deltaTime / CoverSeconds);
                RefreshCover();
            }

            if (_plop >= 0f)
            {
                _plop += deltaTime;
                if (_plop >= PlopSeconds)
                {
                    _plop = -1f;
                }

                RefreshCover();
            }

            if (_shake >= 0f)
            {
                _shake += deltaTime;
                if (_shake >= ShakeSeconds)
                {
                    _shake = -1f;
                    _root.anchoredPosition = _home;
                }
                else
                {
                    // 25% → -5px, 75% → +5px, extremos em zero: um seno resolve os três keyframes.
                    float k = Mathf.Sin(_shake / ShakeSeconds * Mathf.PI * 2f);
                    _root.anchoredPosition = new Vector2(_home.x - UiKit.Css(ShakeCssPixels) * k, _home.y);
                }
            }

            if (_selected)
            {
                _core.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (_dangerOn)
            {
                _danger.color = _dangerBase.WithAlpha(blink);
            }
        }

        // ------------------------------------------------------------------ skin

        /// <summary>
        /// Troca os desenhos conforme a skin. Praia usa os sprites já coloridos
        /// (<see cref="SpriteFactory.Cava"/>/<see cref="SpriteFactory.Monte"/>); Papel de Pão
        /// troca tudo por rabisco de caneta com hachura (CSS 480–492).
        /// </summary>
        public void ApplySkin()
        {
            bool paper = Palette.IsPaper;

            _paper = paper;

            if (paper)
            {
                Sprite sketch = SpriteFactory.SketchRing(SketchSize, SketchThickness, _seed);
                _cava.sprite = sketch;
                _monte.sprite = sketch;
                _cavaBase = Palette.Caneta;
                _monteBase = Palette.Caneta;
                _rim.enabled = false;               // `body.skin-papel .buraco::before{display:none}`
            }
            else
            {
                _cava.sprite = SpriteFactory.Cava(128);
                _monte.sprite = SpriteFactory.Monte(128);
                _cavaBase = Color.white;            // sprites já saem coloridos
                _monteBase = Color.white;
                _rim.enabled = true;
            }

            _selBase = Palette.Accent;
            _dangerBase = Palette.Accent;
            _sel.color = _selected ? _selBase : Color.clear;
            _selOut.color = _selected ? Palette.Ink : Color.clear;
            _danger.color = _dangerOn ? _dangerBase.WithAlpha(0.6f) : Color.clear;

            // O inset do montinho muda de -6% para 0 na skin Papel: força o recálculo.
            float unit = _unit;
            _unit = -1f;
            SetUnit(unit);

            RefreshCover();
        }

        // ------------------------------------------------------------------ interno

        /// <summary>Aplica opacidade e escala de cava/montinho a partir de <see cref="_cover"/>.</summary>
        private void RefreshCover()
        {
            float cavaAlpha = 1f - _cover;
            _cava.color = _cavaBase.WithAlpha(cavaAlpha);
            _cava.enabled = cavaAlpha > 0.002f;

            float monteAlpha = _cover;
            _monte.color = _monteBase.WithAlpha(monteAlpha);
            _monte.enabled = monteAlpha > 0.002f;

            // A hachura é filha do montinho, mas o uGUI não propaga alfa entre Graphics:
            // ela precisa desaparecer junto, na mão.
            _hatch.enabled = _paper && _monte.enabled;
            if (_paper)
            {
                _hatch.color = Palette.Caneta.WithAlpha(0.9f * monteAlpha);
            }

            // Escala: o `plop` tem prioridade porque é o destaque do lance recém-jogado.
            float scale = _plop >= 0f
                ? PlopScale(Mathf.Clamp01(_plop / PlopSeconds))
                : Mathf.Lerp(0.6f, 1f, EaseBack(_cover));
            _monteRt.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Aproxima o <c>cubic-bezier(.3,1.7,.5,1)</c>: chega em 1 passando um tico do ponto.</summary>
        private static float EaseBack(float t)
        {
            const float overshoot = 1.35f;
            float u = t - 1f;
            return 1f + (((overshoot + 1f) * u * u * u) + (overshoot * u * u));
        }

        /// <summary>Keyframe <c>plop</c>: 0% escala .4, 60% escala 1.18, 100% escala 1.</summary>
        private static float PlopScale(float p)
        {
            if (p < 0.6f)
            {
                return Mathf.Lerp(0.4f, 1.18f, EaseOut(p / 0.6f));
            }

            return Mathf.Lerp(1.18f, 1f, EaseOut((p - 0.6f) / 0.4f));
        }

        /// <summary>Saída suave equivalente ao <c>ease-out</c> do CSS.</summary>
        private static float EaseOut(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - (u * u);
        }

        /// <summary>
        /// Ancoragem central com tamanho próprio — o <c>position:absolute</c> centrado do CSS.
        /// O <see cref="UiKit.Picture"/> não mexe em âncoras, então cada camada declara a sua.
        /// </summary>
        private static RectTransform Center(Image image)
        {
            var rt = (RectTransform)image.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }
    }
}
