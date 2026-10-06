using System;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Tela de jogo: HUD em cima, caixa de areia no meio e a lateral com a dica e a pazinha.
    /// Reproduz o <c>#tela-jogo</c> do protótipo, inclusive a virada de layout das media queries
    /// (paisagem larga = lateral à direita; retrato = faixa embaixo).
    /// </summary>
    public sealed class GameScreen
    {
        /// <summary>Altura da barra do HUD — a mesma que o <see cref="HudBar"/> se dá.</summary>
        private const float HudHeightCss = 40f;
        private const float SideWidthCss = 190f;
        private const float SideHeightCss = 92f;
        private const float GapCss = 8f;

        private readonly RectTransform _boardHolder;
        private readonly RectTransform _side;
        private readonly RectTransform _actions;
        private readonly HorizontalLayoutGroup _actionsRow;
        private readonly VerticalLayoutGroup _actionsColumn;
        private readonly PanelView _hintPill;
        private readonly Text _hintText;
        private readonly ButtonView _undo;
        private readonly ButtonView _confirm;

        private int _confirmCount = -1;
        private bool _wideLayout;
        private bool _layoutApplied;

        public GameScreen(Transform parent)
        {
            Root = UiKit.Stretch(parent, "tela-jogo", UiKit.Css(4f));

            // O HudBar já se ancora no topo do pai; aqui só reservamos a altura dele.
            Hud = new HudBar(Root);

            _boardHolder = UiKit.Node(Root, "areia-holder");
            Board = BoardView.Create(_boardHolder);
            UiKit.FillParent(Board.Root);

            _side = UiKit.Node(Root, "lateral");
            var sideColumn = _side.gameObject.AddComponent<VerticalLayoutGroup>();
            sideColumn.spacing = UiKit.Css(GapCss);
            sideColumn.childAlignment = TextAnchor.MiddleCenter;
            sideColumn.childForceExpandWidth = true;
            sideColumn.childForceExpandHeight = false;
            sideColumn.childControlWidth = true;
            sideColumn.childControlHeight = true;

            // Dica: pastilha creme de cantos redondos, como o .dica do CSS.
            _hintPill = UiKit.Panel(_side, "dica", 20f, 2.5f, 3f);
            _hintText = UiKit.Label(
                _hintPill.Content,
                "texto",
                "Escolha uma fileira",
                UiKit.BodyItalic,
                UiKit.FontSize(10f, 3f, 14f),
                Palette.Text);
            UiKit.FillParent((RectTransform)_hintText.transform, UiKit.Css(8f));
            UiKit.Size(_hintPill.Root, 0f, 40f);

            _actions = UiKit.Node(_side, "acoes");
            _actionsRow = _actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            _actionsRow.spacing = UiKit.Css(GapCss);
            _actionsRow.childAlignment = TextAnchor.MiddleCenter;
            _actionsRow.childForceExpandWidth = false;
            _actionsRow.childForceExpandHeight = false;
            _actionsRow.childControlWidth = true;
            _actionsRow.childControlHeight = true;

            _actionsColumn = _actions.gameObject.AddComponent<VerticalLayoutGroup>();
            _actionsColumn.spacing = UiKit.Css(GapCss);
            _actionsColumn.childAlignment = TextAnchor.MiddleCenter;
            _actionsColumn.childForceExpandWidth = true;
            _actionsColumn.childForceExpandHeight = false;
            _actionsColumn.childControlWidth = true;
            _actionsColumn.childControlHeight = true;
            _actionsColumn.enabled = false;

            _undo = UiKit.Button(_actions, "btn-desfazer", "Limpar seleção", ButtonStyle.Small);
            UiKit.Size(_undo.Root, 140f, 34f);
            _undo.Button.onClick.AddListener(() => UndoClicked?.Invoke());

            _confirm = UiKit.Button(_actions, "btn-tapar", "TAPAR", ButtonStyle.Coral, UiKit.Art("pazinha"));
            UiKit.Size(_confirm.Root, 200f, 52f);
            _confirm.Button.onClick.AddListener(() => ConfirmClicked?.Invoke());

            _undo.Interactable = false;
            _confirm.Interactable = false;
        }

        /// <summary>Raiz da tela.</summary>
        public RectTransform Root { get; }

        /// <summary>Barra superior (placar, som, estilo, regras, menu).</summary>
        public HudBar Hud { get; }

        /// <summary>Tabuleiro na areia.</summary>
        public BoardView Board { get; }

        /// <summary>Botão TAPAR (o rótulo mostra quantos buracos estão marcados).</summary>
        public event Action ConfirmClicked;

        /// <summary>Botão "Limpar seleção": desmarca tudo.</summary>
        public event Action UndoClicked;

        /// <summary>Liga/desliga a tela inteira.</summary>
        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            if (visible)
            {
                _layoutApplied = false;
            }
        }

        /// <summary>Texto da pastilha de dica (aceita rich text).</summary>
        public void SetHint(string richText)
        {
            if (_hintText.text != richText)
            {
                _hintText.text = richText;
            }
        }

        /// <summary>Estado dos dois botões de ação.</summary>
        public void SetActions(bool canConfirm, bool canUndo)
        {
            _confirm.Interactable = canConfirm;
            _undo.Interactable = canUndo;
        }

        /// <summary>Rótulo da pazinha: "TAPAR", "TAPAR 1 BURACO" ou "TAPAR n BURACOS".</summary>
        public void SetConfirmCount(int selected)
        {
            if (selected == _confirmCount)
            {
                return;
            }

            _confirmCount = selected;
            _confirm.Label.text = selected <= 0
                ? "TAPAR"
                : selected == 1 ? "TAPAR 1 BURACO" : $"TAPAR {selected} BURACOS";
        }

        /// <summary>Acompanha a rotação da tela e anima o HUD.</summary>
        public void Tick(float time, float deltaTime)
        {
            Rect area = Root.rect;
            bool wide = area.width >= area.height * 1.15f;
            if (!_layoutApplied || wide != _wideLayout)
            {
                _wideLayout = wide;
                _layoutApplied = true;
                ApplyLayout();
            }

            Hud.Tick(time);
        }

        /// <summary>Repinta a tela com as cores da skin atual.</summary>
        public void ApplySkin()
        {
            Hud.ApplySkin();
            Board.ApplySkin();
            UiKit.RepaintPanel(_hintPill);
            _hintText.color = Palette.Text;
            UiKit.RepaintButton(_undo);
            UiKit.RepaintButton(_confirm);
        }

        private void ApplyLayout()
        {
            float top = -UiKit.Css(HudHeightCss + GapCss);

            if (_wideLayout)
            {
                float sideWidth = UiKit.Css(SideWidthCss);

                _boardHolder.anchorMin = Vector2.zero;
                _boardHolder.anchorMax = Vector2.one;
                _boardHolder.offsetMin = new Vector2(0f, 0f);
                _boardHolder.offsetMax = new Vector2(-(sideWidth + UiKit.Css(GapCss)), top);

                _side.anchorMin = new Vector2(1f, 0f);
                _side.anchorMax = new Vector2(1f, 1f);
                _side.pivot = new Vector2(1f, 0.5f);
                _side.offsetMin = new Vector2(-sideWidth, 0f);
                _side.offsetMax = new Vector2(0f, top);

                _actionsRow.enabled = false;
                _actionsColumn.enabled = true;
                UiKit.Size(_undo.Root, SideWidthCss, 34f);
                UiKit.Size(_confirm.Root, SideWidthCss, 56f);
            }
            else
            {
                float sideHeight = UiKit.Css(SideHeightCss);

                _boardHolder.anchorMin = Vector2.zero;
                _boardHolder.anchorMax = Vector2.one;
                _boardHolder.offsetMin = new Vector2(0f, sideHeight + UiKit.Css(GapCss));
                _boardHolder.offsetMax = new Vector2(0f, top);

                _side.anchorMin = Vector2.zero;
                _side.anchorMax = new Vector2(1f, 0f);
                _side.pivot = new Vector2(0.5f, 0f);
                _side.offsetMin = Vector2.zero;
                _side.offsetMax = new Vector2(0f, sideHeight);

                _actionsColumn.enabled = false;
                _actionsRow.enabled = true;
                UiKit.Size(_undo.Root, 140f, 34f);
                UiKit.Size(_confirm.Root, 200f, 46f);
            }

            LayoutRebuilder.MarkLayoutForRebuild(_side);
        }
    }
}
