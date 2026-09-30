using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Camada de efeitos sobre a areia — o <c>#fx</c> do protótipo e a função <c>efeitoTapar</c>.
    ///
    /// Cada buraco tapado dispara três coisas ao mesmo tempo: a pazinha que desce e cava
    /// (keyframe <c>cavar</c>), o bafo de poeira (<c>puff</c>) e doze grãos de areia
    /// espirrados (<c>cai</c>). Como um lance tapa até 7 buracos em rajada (um a cada 115 ms),
    /// tudo sai de um pool: nenhum <c>GameObject</c> nasce ou morre durante a partida.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FxLayer : MonoBehaviour
    {
        /// <summary>Duração do keyframe <c>cavar</c>.</summary>
        private const float ShovelSeconds = 0.5f;

        /// <summary>Duração do keyframe <c>puff</c>.</summary>
        private const float PuffSeconds = 0.55f;

        /// <summary>Grãos por buraco tapado (o laço <c>for(k&lt;12)</c> do protótipo).</summary>
        private const int GrainCount = 12;

        /// <summary>Grãos quando o jogador pediu menos movimento.</summary>
        private const int GrainCountReduced = 3;

        // Keyframes de `cavar`, em CSS px / graus. Y do CSS cresce PARA BAIXO e a rotação
        // do CSS é horária: ambos entram invertidos no uGUI.
        private static readonly float[] ShovelStops = { 0f, 0.25f, 0.45f, 0.65f, 1f };
        private static readonly Vector2[] ShovelMove =
        {
            new Vector2(28f, -46f),
            new Vector2(6f, -8f),
            new Vector2(0f, 2f),
            new Vector2(0f, -2f),
            new Vector2(20f, -40f),
        };

        private static readonly float[] ShovelSpin = { 38f, 12f, -6f, 4f, 30f };
        private static readonly float[] ShovelFade = { 0f, 1f, 1f, 1f, 0f };

        private readonly List<Fx> _shovels = new List<Fx>(4);
        private readonly List<Fx> _puffs = new List<Fx>(4);
        private readonly List<Fx> _grains = new List<Fx>(32);

        private RectTransform _root;
        private int _active;

        // Cursor de varredura por pool: numa rajada de 7 buracos o pool passa de 90 itens e
        // procurar sempre do zero viraria varredura quadrática à toa.
        private int _shovelCursor;
        private int _puffCursor;
        private int _grainCursor;

        /// <summary>Monta a camada de efeitos cobrindo o pai inteiro.</summary>
        public static FxLayer Create(Transform parent)
        {
            RectTransform rt = UiKit.Stretch(parent, "fx");
            var layer = rt.gameObject.AddComponent<FxLayer>();
            layer._root = rt;
            layer.Prewarm();
            return layer;
        }

        /// <summary>
        /// Dispara o efeito de cavar/tapar num ponto da areia.
        /// </summary>
        /// <param name="localPosition">Centro do buraco, em coordenadas locais desta camada.</param>
        /// <param name="unit">O <c>--u</c> em vigor: os tamanhos do CSS são todos múltiplos dele.</param>
        /// <param name="reducedMotion">Pula a pazinha e corta os grãos para três.</param>
        public void Dig(Vector2 localPosition, float unit, bool reducedMotion)
        {
            // `body.skin-papel .pa-fx,.poeira{opacity:.5}` — no papel de pão tudo é mais tímido.
            float skinAlpha = Palette.IsPaper ? 0.5f : 1f;

            if (!reducedMotion)
            {
                SpawnShovel(localPosition, unit, skinAlpha);
            }

            SpawnPuff(localPosition, unit, skinAlpha);

            int grains = reducedMotion ? GrainCountReduced : GrainCount;
            for (int k = 0; k < grains; k++)
            {
                SpawnGrain(localPosition, unit);
            }
        }

        /// <summary>Apaga tudo o que estiver no ar (troca de tela, partida nova).</summary>
        public void Clear()
        {
            Release(_shovels);
            Release(_puffs);
            Release(_grains);
            _active = 0;
        }

        // ------------------------------------------------------------------ ciclo

        private void Update()
        {
            if (_active == 0)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            TickShovels(dt);
            TickPuffs(dt);
            TickGrains(dt);
        }

        private void OnDisable() => Clear();

        // ------------------------------------------------------------------ pazinha

        private void TickShovels(float dt)
        {
            for (int k = 0; k < _shovels.Count; k++)
            {
                Fx fx = _shovels[k];
                if (!fx.Active)
                {
                    continue;
                }

                fx.Elapsed += dt;
                float p = fx.Elapsed / ShovelSeconds;
                if (p >= 1f)
                {
                    Stop(fx);
                    continue;
                }

                int seg = Segment(ShovelStops, p);
                float k01 = EaseOut(Mathf.InverseLerp(ShovelStops[seg], ShovelStops[seg + 1], p));
                Vector2 move = Vector2.Lerp(ShovelMove[seg], ShovelMove[seg + 1], k01);
                float spin = Mathf.Lerp(ShovelSpin[seg], ShovelSpin[seg + 1], k01);
                float fade = Mathf.Lerp(ShovelFade[seg], ShovelFade[seg + 1], k01);

                fx.Rt.anchoredPosition = fx.Home + new Vector2(UiKit.Css(move.x), -UiKit.Css(move.y));
                fx.Rt.localRotation = Quaternion.Euler(0f, 0f, -spin);
                fx.Img.color = new Color(1f, 1f, 1f, fade * fx.Alpha);
            }
        }

        private void SpawnShovel(Vector2 position, float unit, float skinAlpha)
        {
            Fx fx = Take(_shovels, ref _shovelCursor, "pazinha", UiKit.Art("pazinha"), new Vector2(0.5f, 0f));

            // `.pa-fx{width:1.5u;height:2u;margin-left:-.75u;margin-top:-2.15u}` com
            // `transform-origin:50% 100%`: o cabo fica em pé com a ponta logo acima do buraco.
            fx.Rt.sizeDelta = new Vector2(unit * 1.5f, unit * 2f);
            fx.Home = new Vector2(position.x, position.y + (unit * 0.15f));
            fx.Rt.anchoredPosition = fx.Home;
            fx.Rt.localRotation = Quaternion.identity;
            fx.Alpha = skinAlpha;
            fx.Img.color = Color.clear;
            fx.Elapsed = 0f;
        }

        // ------------------------------------------------------------------ poeira

        private void TickPuffs(float dt)
        {
            for (int k = 0; k < _puffs.Count; k++)
            {
                Fx fx = _puffs[k];
                if (!fx.Active)
                {
                    continue;
                }

                fx.Elapsed += dt;
                float p = fx.Elapsed / PuffSeconds;
                if (p >= 1f)
                {
                    Stop(fx);
                    continue;
                }

                // `@keyframes puff{0%{scale(.2);opacity:.9} 100%{scale(1.4);opacity:0}}`
                float e = EaseOut(p);
                float scale = Mathf.Lerp(0.2f, 1.4f, e);
                fx.Rt.localScale = new Vector3(scale, scale, 1f);
                fx.Img.color = Palette.Areia.WithAlpha(Mathf.Lerp(0.9f, 0f, e) * fx.Alpha);
            }
        }

        private void SpawnPuff(Vector2 position, float unit, float skinAlpha)
        {
            Fx fx = Take(_puffs, ref _puffCursor, "poeira", SpriteFactory.SoftDisc(128, 2.4f), new Vector2(0.5f, 0.5f));

            // `.poeira{width:1.7u;height:1.7u;margin:-.85u}` — centrado no buraco.
            float side = unit * 1.7f;
            fx.Rt.sizeDelta = new Vector2(side, side);
            fx.Home = position;
            fx.Rt.anchoredPosition = position;
            fx.Rt.localScale = new Vector3(0.2f, 0.2f, 1f);
            fx.Alpha = skinAlpha;
            fx.Img.color = Palette.Areia.WithAlpha(0.9f * skinAlpha);
            fx.Elapsed = 0f;
        }

        // ------------------------------------------------------------------ grãos

        private void TickGrains(float dt)
        {
            for (int k = 0; k < _grains.Count; k++)
            {
                Fx fx = _grains[k];
                if (!fx.Active)
                {
                    continue;
                }

                fx.Elapsed += dt;
                float p = fx.Elapsed / fx.Duration;
                if (p >= 1f)
                {
                    Stop(fx);
                    continue;
                }

                // `@keyframes cai{0%{translate(0,-.9u) scale(1);opacity:1}
                //                 100%{translate(dx,dy) scale(.3);opacity:0}}`
                float e = EaseOut(p);
                fx.Rt.anchoredPosition = fx.Home + Vector2.Lerp(fx.Start, fx.Target, e);
                float scale = Mathf.Lerp(1f, 0.3f, e);
                fx.Rt.localScale = new Vector3(scale, scale, 1f);
                fx.Img.color = Palette.AreiaEscura.WithAlpha(Mathf.Lerp(0.95f, 0f, e));
            }
        }

        private void SpawnGrain(Vector2 position, float unit)
        {
            Fx fx = Take(_grains, ref _grainCursor, "grao", SpriteFactory.Disc(32), new Vector2(0.5f, 0.5f));

            float side = unit * 0.11f;
            fx.Rt.sizeDelta = new Vector2(side, side);
            fx.Home = position;

            // Sai de 0.9u acima do buraco (o -.9u do keyframe) e cai para um ponto sorteado.
            fx.Start = new Vector2(0f, unit * 0.9f);

            float angle = Random.value * Mathf.PI * 2f;
            float dist = 10f + (Random.value * 26f);
            float dx = Mathf.Cos(angle) * dist;
            float dy = (Mathf.Abs(Mathf.Sin(angle)) * dist * 0.7f) + 8f;
            fx.Target = new Vector2(UiKit.Css(dx), -UiKit.Css(dy));

            fx.Duration = 0.3f + (Random.value * 0.35f);
            fx.Rt.anchoredPosition = fx.Home + fx.Start;
            fx.Rt.localScale = Vector3.one;
            fx.Img.color = Palette.AreiaEscura.WithAlpha(0.95f);
            fx.Elapsed = 0f;
        }

        // ------------------------------------------------------------------ pool

        private void Prewarm()
        {
            // Dois lances seguidos já enchem a tela: 2 pazinhas, 2 bafos e 24 grãos cobrem
            // o caso comum sem nenhuma alocação em partida.
            for (int k = 0; k < 2; k++)
            {
                Park(NewItem(_shovels, "pazinha", UiKit.Art("pazinha"), new Vector2(0.5f, 0f)));
                Park(NewItem(_puffs, "poeira", SpriteFactory.SoftDisc(128, 2.4f), new Vector2(0.5f, 0.5f)));
            }

            for (int k = 0; k < 24; k++)
            {
                Park(NewItem(_grains, "grao", SpriteFactory.Disc(32), new Vector2(0.5f, 0.5f)));
            }
        }

        private Fx Take(List<Fx> pool, ref int cursor, string name, Sprite sprite, Vector2 pivot)
        {
            int count = pool.Count;
            for (int k = 0; k < count; k++)
            {
                int slot = cursor + k;
                if (slot >= count)
                {
                    slot -= count;
                }

                Fx candidate = pool[slot];
                if (candidate.Active)
                {
                    continue;
                }

                cursor = slot + 1 >= count ? 0 : slot + 1;
                candidate.Active = true;
                candidate.Img.enabled = true;
                _active++;
                return candidate;
            }

            Fx fresh = NewItem(pool, name, sprite, pivot);
            fresh.Active = true;
            fresh.Img.enabled = true;
            _active++;
            return fresh;
        }

        private Fx NewItem(List<Fx> pool, string name, Sprite sprite, Vector2 pivot)
        {
            Image img = UiKit.Picture(_root, name, sprite, Color.clear);
            img.preserveAspect = false;
            var rt = (RectTransform)img.transform;

            // Âncora central: o `left/top` do CSS vira anchoredPosition medida do centro da areia.
            // O pivô muda por tipo — a pazinha gira em 50% 100% (transform-origin do keyframe).
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;

            var fx = new Fx { Rt = rt, Img = img, Duration = 1f };
            pool.Add(fx);
            return fx;
        }

        private void Stop(Fx fx)
        {
            Park(fx);
            _active--;
        }

        private static void Park(Fx fx)
        {
            fx.Active = false;
            fx.Elapsed = 0f;
            fx.Img.enabled = false;
            fx.Img.color = Color.clear;
            fx.Rt.localScale = Vector3.one;
            fx.Rt.localRotation = Quaternion.identity;
        }

        private static void Release(List<Fx> pool)
        {
            for (int k = 0; k < pool.Count; k++)
            {
                Park(pool[k]);
            }
        }

        // ------------------------------------------------------------------ curvas

        /// <summary>Índice do trecho de keyframe que contém <paramref name="p"/>.</summary>
        private static int Segment(float[] stops, float p)
        {
            for (int k = stops.Length - 2; k > 0; k--)
            {
                if (p >= stops[k])
                {
                    return k;
                }
            }

            return 0;
        }

        /// <summary>
        /// Saída suave: cobre tanto o <c>ease-out</c> dos grãos/poeira quanto o
        /// <c>cubic-bezier(.35,.9,.4,1)</c> da pazinha, que é praticamente um ease-out.
        /// </summary>
        private static float EaseOut(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - (u * u);
        }

        /// <summary>Um efeito do pool: sprite, tempo e os dois pontos da trajetória.</summary>
        private sealed class Fx
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Home;
            public Vector2 Start;
            public Vector2 Target;
            public float Elapsed;
            public float Duration;
            public float Alpha;
            public bool Active;
        }
    }
}
