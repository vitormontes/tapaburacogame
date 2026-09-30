using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Moldura de pedra portuguesa e vinheta do cartaz — as camadas fixas do protótipo
    /// (<c>.moldura</c> e <c>#vinheta</c>), que emolduram a tela inteira sem receber toque.
    /// Na skin Papel de Pão o calçadão vira um filete de caneta, como no CSS.
    /// </summary>
    public sealed class FrameDecor
    {
        /// <summary>Espessura da tira de calçadão, em CSS px.</summary>
        private const float StripCss = 16f;

        /// <summary>Espessura do filete da skin Papel de Pão, em CSS px.</summary>
        private const float PaperStripCss = 3f;

        /// <summary>Altura do ladrilho gerado, em pixels de textura.</summary>
        private const float TileThickness = 28f;

        private readonly RectTransform _root;
        private readonly RectTransform[] _strips = new RectTransform[4];
        private readonly Image[] _stone = new Image[4];
        private readonly Image[] _sheet = new Image[4];
        private readonly Image _vignette;

        public FrameDecor(Transform parent)
        {
            _root = UiKit.Stretch(parent, "moldura");

            // A vinheta fica por baixo da moldura: escurece os cantos do cartaz, não a pedra.
            _vignette = UiKit.Picture(_root, "vinheta", SpriteFactory.Vignette(), new Color(0.27f, 0.16f, 0.04f, 0.30f));
            UiKit.FillParent(_vignette.rectTransform);

            _strips[0] = Strip(0, "moldura-topo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), false);
            _strips[1] = Strip(1, "moldura-base", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), false);
            _strips[2] = Strip(2, "moldura-esq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), true);
            _strips[3] = Strip(3, "moldura-dir", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), true);

            ApplySkin();
        }

        /// <summary>Raiz da decoração (fica acima das telas e abaixo do modal).</summary>
        public RectTransform Root => _root;

        /// <summary>Reaplica espessura e cor conforme a skin.</summary>
        public void ApplySkin()
        {
            float thickness = UiKit.Css(Palette.IsPaper ? PaperStripCss : StripCss);

            for (int i = 0; i < _strips.Length; i++)
            {
                RectTransform strip = _strips[i];
                bool horizontal = i < 2;
                if (horizontal)
                {
                    strip.offsetMin = new Vector2(0f, i == 0 ? -thickness : 0f);
                    strip.offsetMax = new Vector2(0f, i == 0 ? 0f : thickness);
                }
                else
                {
                    strip.offsetMin = new Vector2(i == 2 ? 0f : -thickness, 0f);
                    strip.offsetMax = new Vector2(i == 2 ? thickness : 0f, 0f);
                }

                Image stone = _stone[i];
                if (Palette.IsPaper)
                {
                    // Filete azul: sem ladrilho, só a cor da caneta com a opacidade do CSS.
                    stone.sprite = SpriteFactory.Solid();
                    stone.type = Image.Type.Simple;
                    stone.color = Palette.Caneta.WithAlpha(0.55f);
                }
                else
                {
                    stone.sprite = SpriteFactory.Calcada(96, 28, !horizontal);
                    stone.type = Image.Type.Tiled;
                    stone.color = Palette.Tinta;

                    // Ladrilho de 28 px de espessura desenhado com a espessura da tira.
                    stone.pixelsPerUnitMultiplier = TileThickness / thickness;
                }

                _sheet[i].color = Palette.IsPaper ? Palette.Caneta.WithAlpha(0f) : Palette.Creme;
            }

            _vignette.enabled = !Palette.IsPaper;
        }

        private RectTransform Strip(int index, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, bool vertical)
        {
            RectTransform strip = UiKit.Node(_root, name);
            strip.anchorMin = anchorMin;
            strip.anchorMax = anchorMax;
            strip.pivot = pivot;
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = Vector2.zero;

            // O creme por baixo da pedra: o desenho do calçadão é a faixa escura sobre ele.
            Image sheet = UiKit.Picture(strip, "fundo", SpriteFactory.Solid(), Palette.Creme);
            UiKit.FillParent(sheet.rectTransform);
            _sheet[index] = sheet;

            Image stone = UiKit.Picture(strip, "pedra", SpriteFactory.Calcada(96, 28, vertical), Palette.Tinta);
            UiKit.FillParent(stone.rectTransform);
            stone.type = Image.Type.Tiled;
            _stone[index] = stone;

            return strip;
        }
    }
}
