using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>Estilos de botão do cartaz (mesmos do CSS: .btn, .btn.creme, .btn.pequeno, .icobtn).</summary>
    public enum ButtonStyle
    {
        /// <summary>Coral com texto creme — ação principal.</summary>
        Coral = 0,

        /// <summary>Creme com texto de tinta — ação secundária.</summary>
        Creme = 1,

        /// <summary>Creme miúdo — "Voltar", "Desfazer".</summary>
        Small = 2,

        /// <summary>Quadradinho de ícone do HUD.</summary>
        Icon = 3,
    }

    /// <summary>Painel montado: borda de tinta, miolo creme e área de conteúdo.</summary>
    public sealed class PanelView
    {
        public RectTransform Root;
        public Image Shadow;
        public Image Border;
        public Image Fill;
        public RectTransform Content;
    }

    /// <summary>Botão montado, com sombra dura e recuo ao toque.</summary>
    public sealed class ButtonView
    {
        public RectTransform Root;
        public Button Button;
        public Image Shadow;
        public Image Border;
        public Image Fill;
        public RectTransform Body;
        public RectTransform Content;
        public Text Label;
        public Image Icon;
        public PressPop Pop;

        private ButtonStyle _style;
        private CanvasGroup _group;

        internal void Init(ButtonStyle style, CanvasGroup group)
        {
            _style = style;
            _group = group;
        }

        /// <summary>Estilo atual — usado ao repintar depois de trocar de skin.</summary>
        public ButtonStyle Style => _style;

        /// <summary>Liga/desliga o botão com o mesmo apagamento do CSS (opacidade .65 e cor lavada).</summary>
        public bool Interactable
        {
            get => Button.interactable;
            set
            {
                if (Button.interactable == value)
                {
                    return;
                }

                Button.interactable = value;
                _group.alpha = value ? 1f : 0.55f;
                Pop.Release();
            }
        }

        /// <summary>Texto do botão (aceita &lt;b&gt; do rich text).</summary>
        public string Text
        {
            get => Label != null ? Label.text : string.Empty;
            set
            {
                if (Label != null)
                {
                    Label.text = value;
                }
            }
        }
    }

    /// <summary>Botão de opção do setup (o <c>.opt</c> com aria-pressed).</summary>
    public sealed class OptionView
    {
        public RectTransform Root;
        public Button Button;
        public Image Border;
        public Image Fill;
        public Text Label;
        public Text Hint;

        private bool _pressed;

        /// <summary>Selecionado?</summary>
        public bool Pressed
        {
            get => _pressed;
            set
            {
                _pressed = value;
                Repaint();
            }
        }

        /// <summary>Reaplica as cores da skin ao estado atual.</summary>
        public void Repaint()
        {
            Fill.color = _pressed ? Palette.Accent : Palette.Sheet.WithAlpha(0.55f);
            Border.color = Palette.Ink;
            Label.color = _pressed ? Palette.Sheet : Palette.Text;
            if (Hint != null)
            {
                Hint.color = (_pressed ? Palette.Sheet : Palette.Text).WithAlpha(_pressed ? 0.9f : 0.7f);
            }
        }
    }

    /// <summary>
    /// Fábrica da interface: tudo é montado em código com sprites do <see cref="SpriteFactory"/>.
    /// Motivo: o jogo nasceu em CSS; reproduzir painel/botão/sombra como componentes evita
    /// prefabs binários e mantém a troca de skin instantânea.
    ///
    /// Escala: o canvas usa resolução de referência 1080×1920, e o protótipo foi desenhado num
    /// viewport de 400 CSS px. Daí <see cref="Css"/> = ×2.7 — as medidas do CSS entram diretas.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Viewport de referência do protótipo, em CSS px.</summary>
        public const float CssViewport = 400f;

        /// <summary>Largura de referência do canvas, em unidades de UI.</summary>
        public const float ReferenceWidth = 1080f;

        /// <summary>Altura de referência do canvas, em unidades de UI.</summary>
        public const float ReferenceHeight = 1920f;

        /// <summary>Fator CSS px → unidade de UI.</summary>
        public const float CssScale = ReferenceWidth / CssViewport;

        private static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>(16);
        private static Font _sign;
        private static Font _body;
        private static Font _bodyBold;
        private static Font _bodyItalic;

        /// <summary>Fonte de cartaz (Anton) — logotipo, botões, números.</summary>
        public static Font Sign => _sign != null ? _sign : (_sign = LoadFont("Fonts/Anton-Regular"));

        /// <summary>Fonte de texto (Crimson Text).</summary>
        public static Font Body => _body != null ? _body : (_body = LoadFont("Fonts/CrimsonText-Regular"));

        /// <summary>Crimson Text negrito.</summary>
        public static Font BodyBold => _bodyBold != null ? _bodyBold : (_bodyBold = LoadFont("Fonts/CrimsonText-Bold"));

        /// <summary>Crimson Text itálico — dicas e legendas.</summary>
        public static Font BodyItalic => _bodyItalic != null ? _bodyItalic : (_bodyItalic = LoadFont("Fonts/CrimsonText-Italic"));

        /// <summary>Converte uma medida do CSS para unidades do canvas.</summary>
        public static float Css(float px) => px * CssScale;

        /// <summary>Equivalente ao <c>clamp(min, Nvw, max)</c> do CSS, já em unidades do canvas.</summary>
        public static int FontSize(float minCss, float vw, float maxCss)
        {
            float atViewport = vw * CssViewport * 0.01f;
            float css = Mathf.Clamp(atViewport, minCss, maxCss);
            return Mathf.Max(1, Mathf.RoundToInt(css * CssScale));
        }

        /// <summary>Sprite de <c>Resources/Art</c>, criado na mão para não depender do importador.</summary>
        public static Sprite Art(string name)
        {
            if (ArtCache.TryGetValue(name, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite imported = Resources.Load<Sprite>("Art/" + name);
            if (imported == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("Art/" + name);
                if (tex == null)
                {
                    Debug.LogWarning($"[UiKit] arte ausente: Art/{name}");
                    return null;
                }

                imported = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                imported.name = "art:" + name;
                imported.hideFlags = HideFlags.DontSave;
            }

            ArtCache[name] = imported;
            return imported;
        }

        /// <summary>Descarta sprites criados em runtime (troca de cena / saída).</summary>
        public static void ClearArtCache() => ArtCache.Clear();

        // ------------------------------------------------------------------ nós

        /// <summary>Nó vazio com âncora central.</summary>
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Nó que cobre o pai inteiro, com margem opcional em unidades do canvas.</summary>
        public static RectTransform Stretch(Transform parent, string name, float margin = 0f)
        {
            RectTransform rt = Node(parent, name);
            FillParent(rt, margin);
            return rt;
        }

        /// <summary>Estica um RectTransform sobre o pai.</summary>
        public static RectTransform FillParent(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        /// <summary>Imagem simples (sem interação por padrão).</summary>
        public static Image Picture(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        /// <summary>Texto do cartaz ou do corpo, já com overflow liberado.</summary>
        public static Text Label(Transform parent, string name, string content, Font font, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = content;
            text.alignment = anchor;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            return text;
        }

        /// <summary>Contorno grosso de tinta atrás de um texto (imita o -webkit-text-stroke).</summary>
        public static Outline InkOutline(Text text, float thickness, Color color)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, thickness);
            outline.useGraphicAlpha = false;
            return outline;
        }

        // ------------------------------------------------------------------ painéis

        /// <summary>Painel do cartaz: sombra dura, borda de tinta e miolo creme.</summary>
        public static PanelView Panel(Transform parent, string name, float radiusCss = 10f, float borderCss = 3f, float shadowCss = 6f)
        {
            var view = new PanelView { Root = Node(parent, name) };

            int radius = Mathf.RoundToInt(Css(radiusCss));
            Sprite box = SpriteFactory.RoundedRect(Mathf.Max(48, radius * 3), radius);

            view.Shadow = Picture(view.Root, "sombra", box, Palette.Ink.WithAlpha(0.28f));
            FillParent((RectTransform)view.Shadow.transform);
            ((RectTransform)view.Shadow.transform).anchoredPosition = new Vector2(0f, -Css(shadowCss));

            view.Border = Picture(view.Root, "borda", box, Palette.Ink, true);
            FillParent((RectTransform)view.Border.transform);

            view.Fill = Picture(view.Root, "miolo", box, Palette.Sheet);
            FillParent((RectTransform)view.Fill.transform, Css(borderCss));

            view.Content = Stretch(view.Root, "conteudo");
            return view;
        }

        /// <summary>Repinta um painel na skin atual.</summary>
        public static void RepaintPanel(PanelView panel)
        {
            panel.Shadow.color = Palette.Ink.WithAlpha(Palette.IsPaper ? 0.35f : 0.28f);
            panel.Border.color = Palette.Ink;
            panel.Fill.color = Palette.Sheet;
        }

        // ------------------------------------------------------------------ botões

        /// <summary>Botão do cartaz. O rótulo aceita rich text; <paramref name="icon"/> é opcional.</summary>
        public static ButtonView Button(Transform parent, string name, string label, ButtonStyle style, Sprite icon = null)
        {
            bool small = style == ButtonStyle.Small;
            bool isIcon = style == ButtonStyle.Icon;

            float radiusCss = isIcon ? 8f : small ? 7f : 8f;
            float borderCss = small || isIcon ? 2.5f : 3f;
            float shadowCss = small || isIcon ? 3f : 5f;

            var view = new ButtonView { Root = Node(parent, name) };
            int radius = Mathf.RoundToInt(Css(radiusCss));
            Sprite box = SpriteFactory.RoundedRect(Mathf.Max(48, radius * 3), radius);

            view.Shadow = Picture(view.Root, "sombra", box, Palette.Ink.WithAlpha(isIcon || small ? 0.35f : 1f));
            FillParent((RectTransform)view.Shadow.transform);
            ((RectTransform)view.Shadow.transform).anchoredPosition = new Vector2(0f, -Css(shadowCss));

            view.Body = Stretch(view.Root, "corpo");

            view.Border = Picture(view.Body, "borda", box, Palette.Ink, true);
            FillParent((RectTransform)view.Border.transform);

            view.Fill = Picture(view.Body, "miolo", box, FillColor(style));
            FillParent((RectTransform)view.Fill.transform, Css(borderCss));

            view.Content = Stretch(view.Body, "conteudo");
            var row = view.Content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = Css(6f);
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.padding = new RectOffset(
                Mathf.RoundToInt(Css(small ? 6f : 10f)),
                Mathf.RoundToInt(Css(small ? 6f : 10f)),
                0,
                0);

            if (icon != null)
            {
                view.Icon = Picture(view.Content, "icone", icon, Color.white);
                var iconSize = view.Icon.gameObject.AddComponent<LayoutElement>();
                float side = Css(isIcon ? 20f : small ? 16f : 26f);
                iconSize.preferredWidth = side;
                iconSize.preferredHeight = side;
                view.Icon.preserveAspect = true;
            }

            if (!string.IsNullOrEmpty(label))
            {
                int size = isIcon
                    ? FontSize(13f, 3.6f, 17f)
                    : small ? FontSize(11f, 3f, 14f) : FontSize(15f, 4.2f, 23f);
                Font font = style == ButtonStyle.Icon ? Body : Sign;
                view.Label = Label(view.Content, "rotulo", label, font, size, TextColor(style));
                view.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var fit = view.Label.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            var button = view.Root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = view.Border;
            view.Button = button;

            var group = view.Root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            view.Init(style, group);

            view.Pop = PressPop.Attach(view.Root.gameObject, view.Body, (RectTransform)view.Shadow.transform, Css(small || isIcon ? 2f : 4f), Css(shadowCss));
            return view;
        }

        /// <summary>Reaplica as cores de skin a um botão.</summary>
        public static void RepaintButton(ButtonView view)
        {
            bool decorated = view.Style == ButtonStyle.Small || view.Style == ButtonStyle.Icon;
            view.Shadow.color = Palette.Ink.WithAlpha(decorated ? 0.35f : 1f);
            view.Border.color = Palette.Ink;
            view.Fill.color = FillColor(view.Style);
            if (view.Label != null)
            {
                view.Label.color = TextColor(view.Style);
            }
        }

        /// <summary>Botão de opção do setup, com subtítulo itálico opcional.</summary>
        public static OptionView Option(Transform parent, string name, string label, string hint = null)
        {
            var view = new OptionView { Root = Node(parent, name) };
            int radius = Mathf.RoundToInt(Css(7f));
            Sprite box = SpriteFactory.RoundedRect(Mathf.Max(48, radius * 3), radius);

            view.Border = Picture(view.Root, "borda", box, Palette.Ink, true);
            FillParent((RectTransform)view.Border.transform);

            view.Fill = Picture(view.Root, "miolo", box, Palette.Sheet.WithAlpha(0.55f));
            FillParent((RectTransform)view.Fill.transform, Css(2.5f));

            RectTransform content = Stretch(view.Root, "conteudo");
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleCenter;
            column.spacing = Css(2f);
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.padding = new RectOffset(
                Mathf.RoundToInt(Css(5f)),
                Mathf.RoundToInt(Css(5f)),
                Mathf.RoundToInt(Css(6f)),
                Mathf.RoundToInt(Css(6f)));

            view.Label = Label(content, "rotulo", label, BodyBold, FontSize(11f, 3.1f, 14f), Palette.Text);
            var labelFit = view.Label.gameObject.AddComponent<ContentSizeFitter>();
            labelFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!string.IsNullOrEmpty(hint))
            {
                view.Hint = Label(content, "dica", hint, BodyItalic, FontSize(9f, 2.5f, 12f), Palette.Text.WithAlpha(0.7f));
                var hintFit = view.Hint.gameObject.AddComponent<ContentSizeFitter>();
                hintFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            var button = view.Root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = view.Border;
            view.Button = button;
            view.Repaint();
            return view;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Linha horizontal com espaçamento em CSS px.</summary>
        public static HorizontalLayoutGroup Row(RectTransform rt, float spacingCss, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = Css(spacingCss);
            row.childAlignment = alignment;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childControlWidth = true;
            row.childControlHeight = true;
            return row;
        }

        /// <summary>Coluna vertical com espaçamento em CSS px.</summary>
        public static VerticalLayoutGroup Column(RectTransform rt, float spacingCss, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var column = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = Css(spacingCss);
            column.childAlignment = alignment;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            column.childControlWidth = true;
            column.childControlHeight = true;
            return column;
        }

        /// <summary>Tamanho fixo para um filho de layout.</summary>
        public static LayoutElement Size(Component target, float widthCss, float heightCss)
        {
            var element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }

            if (widthCss > 0f)
            {
                element.preferredWidth = Css(widthCss);
                element.minWidth = Css(widthCss);
            }

            if (heightCss > 0f)
            {
                element.preferredHeight = Css(heightCss);
                element.minHeight = Css(heightCss);
            }

            return element;
        }

        /// <summary>Filho elástico (o <c>flex:1</c> do CSS).</summary>
        public static LayoutElement Flexible(Component target, float weight = 1f)
        {
            var element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.gameObject.AddComponent<LayoutElement>();
            }

            element.flexibleWidth = weight;
            element.flexibleHeight = weight;
            return element;
        }

        // ------------------------------------------------------------------ interno

        private static Color FillColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Coral: return Palette.Accent;
                case ButtonStyle.Creme:
                case ButtonStyle.Small:
                case ButtonStyle.Icon:
                default: return Palette.Sheet;
            }
        }

        private static Color TextColor(ButtonStyle style)
        {
            return style == ButtonStyle.Coral ? Palette.Sheet : Palette.Text;
        }

        private static Font LoadFont(string path)
        {
            Font font = Resources.Load<Font>(path);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return font;
        }
    }
}
