// Shim de UnityEngine — recursos: texturas, sprites, fontes, câmera e áudio.
// Texturas guardam pixels de verdade (o SpriteFactory escreve neles); nada é rasterizado
// na tela, então o shim prova a GERAÇÃO dos sprites, não a aparência final.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum TextureFormat
    {
        Alpha8 = 1,
        RGB24 = 3,
        RGBA32 = 4,
        ARGB32 = 5,
        RGBAFloat = 17,
    }

    public enum FilterMode
    {
        Point = 0,
        Bilinear = 1,
        Trilinear = 2,
    }

    public enum TextureWrapMode
    {
        Repeat = 0,
        Clamp = 1,
        Mirror = 2,
        MirrorOnce = 3,
    }

    public enum SpriteMeshType
    {
        FullRect = 0,
        Tight = 1,
    }

    public enum SpritePackingMode
    {
        Tight = 0,
        Rectangle = 1,
    }

    public abstract class Texture : Object
    {
        public virtual int width { get; set; }

        public virtual int height { get; set; }

        public FilterMode filterMode { get; set; } = FilterMode.Bilinear;

        public TextureWrapMode wrapMode { get; set; } = TextureWrapMode.Repeat;

        public int anisoLevel { get; set; } = 1;

        public float mipMapBias { get; set; }
    }

    public class Texture2D : Texture
    {
        private Color32[] _pixels;

        public Texture2D(int textureWidth, int textureHeight)
            : this(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
        }

        public Texture2D(int textureWidth, int textureHeight, TextureFormat textureFormat, bool mipChain)
        {
            width = textureWidth;
            height = textureHeight;
            format = textureFormat;
            _pixels = new Color32[Math.Max(1, textureWidth * textureHeight)];
            name = "Texture2D";
        }

        public Texture2D(int textureWidth, int textureHeight, TextureFormat textureFormat, bool mipChain, bool linear)
            : this(textureWidth, textureHeight, textureFormat, mipChain)
        {
        }

        public TextureFormat format { get; }

        public static Texture2D whiteTexture { get; } = MakeSolid(new Color32(255, 255, 255, 255));

        public static Texture2D blackTexture { get; } = MakeSolid(new Color32(0, 0, 0, 255));

        /// <summary>Quantas vezes Apply() foi chamado — o harness usa para provar que houve pintura.</summary>
        internal int ApplyCount { get; private set; }

        public void SetPixels32(Color32[] colors)
        {
            if (colors == null)
            {
                return;
            }

            Array.Copy(colors, _pixels, Math.Min(colors.Length, _pixels.Length));
        }

        public void SetPixels32(Color32[] colors, int miplevel) => SetPixels32(colors);

        public void SetPixels(Color[] colors)
        {
            if (colors == null)
            {
                return;
            }

            int n = Math.Min(colors.Length, _pixels.Length);
            for (int i = 0; i < n; i++) _pixels[i] = colors[i];
        }

        public void SetPixels(Color[] colors, int miplevel) => SetPixels(colors);

        public Color32[] GetPixels32() => (Color32[])_pixels.Clone();

        public Color[] GetPixels()
        {
            var result = new Color[_pixels.Length];
            for (int i = 0; i < _pixels.Length; i++) result[i] = _pixels[i];
            return result;
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            _pixels[y * width + x] = color;
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return Color.clear;
            }

            return _pixels[y * width + x];
        }

        public Color GetPixelBilinear(float u, float v)
            => GetPixel(Mathf.Clamp((int)(u * width), 0, width - 1), Mathf.Clamp((int)(v * height), 0, height - 1));

        public void Apply() => ApplyCount++;

        public void Apply(bool updateMipmaps) => Apply();

        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) => Apply();

        private static Texture2D MakeSolid(Color32 color)
        {
            var t = new Texture2D(1, 1);
            t.SetPixels32(new[] { color });
            t.Apply();
            return t;
        }
    }

    public sealed class Sprite : Object
    {
        private Sprite()
        {
        }

        public Texture2D texture { get; private set; }

        public Rect rect { get; private set; }

        public Rect textureRect => rect;

        public Vector4 border { get; private set; }

        public Vector2 pivot { get; private set; }

        public float pixelsPerUnit { get; private set; } = 100f;

        public Bounds bounds => new Bounds(Vector3.zero, new Vector3(rect.width / pixelsPerUnit, rect.height / pixelsPerUnit, 0f));

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot)
            => Create(texture, rect, pivot, 100f, 0, SpriteMeshType.Tight, Vector4.zero);

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
            => Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.Tight, Vector4.zero);

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude)
            => Create(texture, rect, pivot, pixelsPerUnit, extrude, SpriteMeshType.Tight, Vector4.zero);

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude,
            SpriteMeshType meshType)
            => Create(texture, rect, pivot, pixelsPerUnit, extrude, meshType, Vector4.zero);

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude,
            SpriteMeshType meshType, Vector4 border)
        {
            return new Sprite
            {
                texture = texture,
                rect = rect,
                pivot = pivot,
                pixelsPerUnit = pixelsPerUnit <= 0f ? 100f : pixelsPerUnit,
                border = border,
                name = texture != null ? texture.name : "Sprite",
            };
        }

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude,
            SpriteMeshType meshType, Vector4 border, bool generateFallbackPhysicsShape)
            => Create(texture, rect, pivot, pixelsPerUnit, extrude, meshType, border);
    }

    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }

        public Vector3 center { get; set; }

        public Vector3 size { get; set; }

        public Vector3 extents => size * 0.5f;
    }

    public enum FontStyle
    {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        BoldAndItalic = 3,
    }

    public sealed class Font : Object
    {
        public Font()
        {
            name = "Font";
        }

        public Font(string fontName)
        {
            name = fontName;
        }

        public int fontSize { get; set; } = 14;

        public bool dynamic => true;

        public static Font CreateDynamicFontFromOSFont(string fontname, int size) => new Font(fontname);

        public static string[] GetOSInstalledFontNames() => new[] { "Arial", "Verdana" };
    }

    public class Material : Object
    {
        public Material()
        {
            name = "Material";
        }

        public Material(Shader shader)
        {
            this.shader = shader;
            name = "Material";
        }

        public Shader shader { get; set; }

        public Color color { get; set; } = Color.white;

        public void SetFloat(string propertyName, float value)
        {
        }

        public void SetColor(string propertyName, Color value)
        {
        }
    }

    public sealed class Shader : Object
    {
        public static Shader Find(string shaderName) => new Shader { name = shaderName };
    }

    /// <summary>
    /// Resources do shim: NÃO há pasta Resources fora do editor, então Load devolve null
    /// (o jogo já trata isso) e a fonte embutida vira um objeto Font vazio.
    /// </summary>
    public static class Resources
    {
        private static readonly Dictionary<string, Object> _builtin = new Dictionary<string, Object>();

        /// <summary>
        /// Texture2D vem do PNG em unity/Assets/Resources. Sprite devolve null de propósito:
        /// fora do editor não existe importador que fabrique Sprites, e o jogo já trata esse caso
        /// montando o Sprite na mão a partir da textura.
        /// </summary>
        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Texture2D) || typeof(T) == typeof(Texture))
            {
                return ResourcesLoader.LoadTexture(path) as T;
            }

            return null;
        }

        public static Object Load(string path) => ResourcesLoader.LoadTexture(path);

        public static T[] LoadAll<T>(string path) where T : Object => Array.Empty<T>();

        public static T GetBuiltinResource<T>(string path) where T : Object
        {
            if (_builtin.TryGetValue(path, out Object cached))
            {
                return cached as T;
            }

            Object created = null;
            if (typeof(T) == typeof(Font))
            {
                created = new Font(path);
            }

            if (created != null)
            {
                _builtin[path] = created;
            }

            return created as T;
        }

        public static void UnloadUnusedAssets()
        {
        }
    }

    public enum CameraClearFlags
    {
        Skybox = 1,
        Color = 2,
        SolidColor = 2,
        Depth = 3,
        Nothing = 4,
    }

    public sealed class Camera : Behaviour
    {
        public CameraClearFlags clearFlags { get; set; } = CameraClearFlags.Skybox;

        public Color backgroundColor { get; set; } = new Color(0.19f, 0.30f, 0.47f, 0f);

        public bool orthographic { get; set; }

        public float orthographicSize { get; set; } = 5f;

        public int cullingMask { get; set; } = -1;

        public float depth { get; set; }

        public float fieldOfView { get; set; } = 60f;

        public float nearClipPlane { get; set; } = 0.3f;

        public float farClipPlane { get; set; } = 1000f;

        public Rect pixelRect => new Rect(0f, 0f, Screen.width, Screen.height);

        public static Camera main => UnityRuntime.FindFirstCamera();

        public Vector3 WorldToScreenPoint(Vector3 position) => position;

        public Vector3 ScreenToWorldPoint(Vector3 position) => position;
    }

    public sealed class AudioClip : Object
    {
        private float[] _data = Array.Empty<float>();

        private AudioClip()
        {
        }

        public int samples { get; private set; }

        public int channels { get; private set; } = 1;

        public int frequency { get; private set; } = 44100;

        public float length => frequency > 0 ? samples / (float)frequency : 0f;

        public bool loadInBackground => false;

        public static AudioClip Create(string clipName, int lengthSamples, int clipChannels, int clipFrequency, bool stream)
        {
            return new AudioClip
            {
                name = clipName,
                samples = lengthSamples,
                channels = clipChannels,
                frequency = clipFrequency,
                _data = new float[Math.Max(1, lengthSamples * clipChannels)],
            };
        }

        public static AudioClip Create(string clipName, int lengthSamples, int clipChannels, int clipFrequency, bool stream,
            object pcmReaderCallback)
            => Create(clipName, lengthSamples, clipChannels, clipFrequency, stream);

        public bool SetData(float[] data, int offsetSamples)
        {
            if (data == null)
            {
                return false;
            }

            int offset = offsetSamples * channels;
            int n = Math.Min(data.Length, Math.Max(0, _data.Length - offset));
            Array.Copy(data, 0, _data, offset, n);
            return true;
        }

        public bool GetData(float[] data, int offsetSamples)
        {
            if (data == null)
            {
                return false;
            }

            int offset = offsetSamples * channels;
            int n = Math.Min(data.Length, Math.Max(0, _data.Length - offset));
            Array.Copy(_data, offset, data, 0, n);
            return true;
        }

        /// <summary>Harness: maior amplitude gravada — prova que o áudio procedural gerou som.</summary>
        internal float PeakAmplitude()
        {
            float peak = 0f;
            for (int i = 0; i < _data.Length; i++)
            {
                float v = Math.Abs(_data[i]);
                if (v > peak) peak = v;
            }

            return peak;
        }
    }

    public enum AudioSpeakerMode
    {
        Mono = 1,
        Stereo = 2,
    }

    public static class AudioSettings
    {
        public static int outputSampleRate { get; set; } = 48000;

        public static AudioSpeakerMode speakerMode { get; set; } = AudioSpeakerMode.Stereo;
    }

    public sealed class AudioSource : Behaviour
    {
        /// <summary>Harness: cada Play/PlayOneShot é contado, então dá para provar que houve som.</summary>
        internal int PlayCount { get; private set; }

        internal AudioClip LastPlayed { get; private set; }

        public AudioClip clip { get; set; }

        public float volume { get; set; } = 1f;

        public float pitch { get; set; } = 1f;

        public bool loop { get; set; }

        public bool playOnAwake { get; set; } = true;

        public bool mute { get; set; }

        public float spatialBlend { get; set; }

        public float panStereo { get; set; }

        public float time { get; set; }

        public int timeSamples { get; set; }

        public bool isPlaying { get; private set; }

        public bool ignoreListenerPause { get; set; }

        public bool bypassEffects { get; set; }

        public bool bypassReverbZones { get; set; }

        public bool bypassListenerEffects { get; set; }

        public float dopplerLevel { get; set; } = 1f;

        public float reverbZoneMix { get; set; } = 1f;

        public float minDistance { get; set; } = 1f;

        public float maxDistance { get; set; } = 500f;

        public int priority { get; set; } = 128;

        public object outputAudioMixerGroup { get; set; }

        public void Play()
        {
            isPlaying = true;
            PlayCount++;
            LastPlayed = clip;
        }

        public void Play(ulong delay) => Play();

        public void PlayDelayed(float delay) => Play();

        public void PlayScheduled(double time) => Play();

        public void PlayOneShot(AudioClip oneShot)
        {
            PlayCount++;
            LastPlayed = oneShot;
        }

        public void PlayOneShot(AudioClip oneShot, float volumeScale) => PlayOneShot(oneShot);

        public void Stop()
        {
            isPlaying = false;
        }

        public void Pause()
        {
            isPlaying = false;
        }

        public void UnPause()
        {
            isPlaying = true;
        }
    }

    public sealed class AudioListener : Behaviour
    {
        public static float volume { get; set; } = 1f;

        public static bool pause { get; set; }
    }

    /// <summary>Opacidade/interatividade de um ramo da UI.</summary>
    public sealed class CanvasGroup : Behaviour
    {
        public float alpha { get; set; } = 1f;

        public bool interactable { get; set; } = true;

        public bool blocksRaycasts { get; set; } = true;

        public bool ignoreParentGroups { get; set; }
    }
}
