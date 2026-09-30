using System;
using TapaBuraco.Core;
using UnityEngine;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Paleta do cartaz serigráfico, copiada do protótipo web (variáveis CSS de index.html).
    /// As cores "vivas" (<see cref="Ink"/>, <see cref="Sheet"/>…) trocam junto com a skin.
    /// </summary>
    public static class Palette
    {
        // --- Areia de Copacabana ---
        public static readonly Color Areia = Hex(0xE9D3A8);
        public static readonly Color AreiaMolhada = Hex(0xD3B786);
        public static readonly Color AreiaEscura = Hex(0xB9985F);
        public static readonly Color AreiaSombra = Hex(0x8E7038);
        public static readonly Color AreiaFunda = Hex(0x6E5426);
        public static readonly Color AreiaClara = Hex(0xFBEFD5);
        public static readonly Color AreiaMeia = Hex(0xEBD6AC);
        public static readonly Color AreiaBase = Hex(0xCBAA71);
        public static readonly Color AreiaFundo = Hex(0xC2A374);

        public static readonly Color Mar = Hex(0x2BA6A4);
        public static readonly Color MarFundo = Hex(0x1C7E80);
        public static readonly Color MarClaro = Hex(0x57C2B4);
        public static readonly Color Ceu = Hex(0x7EC8D8);
        public static readonly Color Coral = Hex(0xE8765C);
        public static readonly Color CoralEscuro = Hex(0xC4503A);
        public static readonly Color Creme = Hex(0xF4EBD9);
        public static readonly Color Tinta = Hex(0x1A1A1A);
        public static readonly Color Madeira = Hex(0xA9713F);
        public static readonly Color MadeiraEscura = Hex(0x7A4C26);
        public static readonly Color Amarelo = Hex(0xE8B23C);
        public static readonly Color Pele = Hex(0xE8B98D);

        // --- Papel de Pão ---
        public static readonly Color Papel = Hex(0xD2AE7C);
        public static readonly Color PapelClaro = Hex(0xE7CCA0);
        public static readonly Color PapelEscuro = Hex(0xBE9563);
        public static readonly Color PapelCreme = Hex(0xEFE2C6);
        public static readonly Color Caneta = Hex(0x2E4CA8);
        public static readonly Color CanetaEscura = Hex(0x22336E);

        /// <summary>Skin em uso. Troque por <see cref="SetSkin"/>.</summary>
        public static Skin Skin { get; private set; } = Skin.Praia;

        /// <summary>Disparado depois de trocar a skin: quem desenha se repinta.</summary>
        public static event Action SkinChanged;

        public static bool IsPaper => Skin == Skin.Papel;

        /// <summary>Cor do traço/contorno: tinta preta na praia, caneta azul no papel.</summary>
        public static Color Ink => IsPaper ? CanetaEscura : Tinta;

        /// <summary>Cor de preenchimento dos painéis.</summary>
        public static Color Sheet => IsPaper ? PapelCreme : Creme;

        /// <summary>Cor de destaque (botões principais, seleção).</summary>
        public static Color Accent => IsPaper ? Caneta : Coral;

        /// <summary>Cor de destaque escura (títulos, rótulos).</summary>
        public static Color AccentDark => IsPaper ? CanetaEscura : CoralEscuro;

        /// <summary>Cor do texto corrido.</summary>
        public static Color Text => IsPaper ? CanetaEscura : Tinta;

        /// <summary>Fundo da tela inteira.</summary>
        public static Color Background => IsPaper ? Papel : Hex(0x0D4C55);

        /// <summary>Cor do jogador 1 / jogador 2 no HUD.</summary>
        public static Color Player(int index) => index == 0 ? Coral : Mar;

        public static void SetSkin(Skin skin)
        {
            if (Skin == skin)
            {
                return;
            }

            Skin = skin;
            SkinChanged?.Invoke();
        }

        /// <summary>Notifica os assinantes sem trocar de skin (usado ao montar a tela).</summary>
        public static void Refresh() => SkinChanged?.Invoke();

        /// <summary>0xRRGGBB → Color opaco (sRGB).</summary>
        public static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }

    /// <summary>Atalhos de cor usados pela camada de UI.</summary>
    public static class ColorExtensions
    {
        /// <summary>Mesma cor com outro alfa.</summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Mistura linear entre duas cores.</summary>
        public static Color Mix(this Color a, Color b, float t) => Color.Lerp(a, b, t);
    }
}
