// Shim de UnityEngine — serviços globais: tempo, entrada, tela, PlayerPrefs, JsonUtility, Debug.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    /// <summary>Relógio virtual: o harness manda o tempo andar, nada roda em tempo real.</summary>
    public static class Time
    {
        public static float timeScale { get; set; } = 1f;

        public static float deltaTime { get; private set; }

        public static float unscaledDeltaTime { get; private set; }

        public static float smoothDeltaTime => deltaTime;

        public static float fixedDeltaTime { get; set; } = 0.02f;

        public static float maximumDeltaTime { get; set; } = 0.333f;

        public static float time { get; private set; }

        public static float unscaledTime { get; private set; }

        public static float timeSinceLevelLoad => time;

        public static float realtimeSinceStartup => unscaledTime;

        public static int frameCount { get; private set; }

        internal static void Advance(float step)
        {
            unscaledDeltaTime = step;
            deltaTime = step * timeScale;
            unscaledTime += unscaledDeltaTime;
            time += deltaTime;
            frameCount++;
        }

        internal static void Reset()
        {
            deltaTime = 0f;
            unscaledDeltaTime = 0f;
            time = 0f;
            unscaledTime = 0f;
            frameCount = 0;
            timeScale = 1f;
        }
    }

    public enum KeyCode
    {
        None = 0,
        Backspace = 8,
        Tab = 9,
        Return = 13,
        Escape = 27,
        Space = 32,
        Delete = 127,
        KeypadEnter = 271,
        UpArrow = 273,
        DownArrow = 274,
        RightArrow = 275,
        LeftArrow = 276,
        A = 97,
        D = 100,
        M = 109,
        P = 112,
        R = 114,
        S = 115,
        W = 119,
        Alpha0 = 48,
        Alpha1 = 49,
        Alpha2 = 50,
        Mouse0 = 323,
        Mouse1 = 324,
    }

    /// <summary>
    /// Entrada programável: o harness enfileira teclas com <see cref="PressKey"/> e elas valem
    /// por UM quadro, como o GetKeyDown de verdade.
    /// </summary>
    public static class Input
    {
        private static readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> _up = new HashSet<KeyCode>();

        public static Vector3 mousePosition { get; set; } = Vector3.zero;

        public static int touchCount => 0;

        public static bool anyKey => _held.Count > 0;

        public static bool anyKeyDown => _down.Count > 0;

        public static bool GetKey(KeyCode key) => _held.Contains(key);

        public static bool GetKeyDown(KeyCode key) => _down.Contains(key);

        public static bool GetKeyUp(KeyCode key) => _up.Contains(key);

        public static bool GetMouseButton(int button) => false;

        public static bool GetMouseButtonDown(int button) => false;

        public static bool GetMouseButtonUp(int button) => false;

        public static float GetAxis(string axisName) => 0f;

        public static float GetAxisRaw(string axisName) => 0f;

        /// <summary>Harness: aperta e solta uma tecla no próximo quadro.</summary>
        internal static void PressKey(KeyCode key)
        {
            _down.Add(key);
            _held.Add(key);
        }

        /// <summary>Harness: encerra o quadro de entrada (chamado antes de cada Step).</summary>
        internal static void NewFrame()
        {
            _up.Clear();
            foreach (KeyCode key in _down)
            {
                _up.Add(key);
                _held.Remove(key);
            }

            _down.Clear();
        }
    }

    /// <summary>Na Unity real é uma classe de constantes int, não um enum.</summary>
    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
        public const int SystemSetting = -2;
    }

    public enum ScreenOrientation
    {
        Portrait = 1,
        PortraitUpsideDown = 2,
        LandscapeLeft = 3,
        LandscapeRight = 4,
        AutoRotation = 5,
    }

    public static class Screen
    {
        public static int width { get; internal set; } = 1080;

        public static int height { get; internal set; } = 1920;

        public static float dpi => 320f;

        public static int sleepTimeout { get; set; } = (int)SleepTimeout.SystemSetting;

        public static bool fullScreen { get; set; } = true;

        public static ScreenOrientation orientation { get; set; } = ScreenOrientation.Portrait;

        public static Rect safeArea => new Rect(0f, 0f, width, height);

        public static void SetResolution(int w, int h, bool fullscreen)
        {
            width = w;
            height = h;
        }
    }

    public enum RuntimePlatform
    {
        WindowsPlayer = 2,
        WindowsEditor = 7,
        Android = 11,
        IPhonePlayer = 8,
    }

    public enum SystemLanguage
    {
        Portuguese = 27,
        English = 10,
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; } = -1;

        public static bool isPlaying => true;

        public static bool isEditor => false;

        public static bool isMobilePlatform => false;

        public static RuntimePlatform platform => RuntimePlatform.WindowsPlayer;

        public static SystemLanguage systemLanguage => SystemLanguage.Portuguese;

        public static string persistentDataPath => System.IO.Path.GetTempPath();

        public static string productName => "TAPA BURACO";

        public static string version => "1.0";

        public static bool runInBackground { get; set; }

        public static event Action quitting;

        public static void Quit()
        {
            quitting?.Invoke();
        }

        public static void OpenURL(string url)
        {
        }
    }

    public static class SystemInfo
    {
        public static string deviceModel => "Harness";

        public static string deviceName => "Harness";

        public static string operatingSystem => Environment.OSVersion.ToString();

        public static int processorCount => Environment.ProcessorCount;

        public static int systemMemorySize => 8192;

        public static bool supportsVibration => false;
    }

    /// <summary>PlayerPrefs em memória — o harness verifica a persistência lendo de volta.</summary>
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> _store = new Dictionary<string, object>();

        public static void SetString(string key, string value) => _store[key] = value;

        public static string GetString(string key) => GetString(key, string.Empty);

        public static string GetString(string key, string defaultValue)
            => _store.TryGetValue(key, out object v) && v is string s ? s : defaultValue;

        public static void SetInt(string key, int value) => _store[key] = value;

        public static int GetInt(string key) => GetInt(key, 0);

        public static int GetInt(string key, int defaultValue)
            => _store.TryGetValue(key, out object v) && v is int i ? i : defaultValue;

        public static void SetFloat(string key, float value) => _store[key] = value;

        public static float GetFloat(string key) => GetFloat(key, 0f);

        public static float GetFloat(string key, float defaultValue)
            => _store.TryGetValue(key, out object v) && v is float f ? f : defaultValue;

        public static bool HasKey(string key) => _store.ContainsKey(key);

        public static void DeleteKey(string key) => _store.Remove(key);

        public static void DeleteAll() => _store.Clear();

        public static void Save()
        {
        }
    }

    /// <summary>
    /// JsonUtility mínimo: serializa CAMPOS PÚBLICOS (e privados com [SerializeField]) de classes
    /// marcadas com [Serializable]. Cobre int/float/bool/string/enum, vetores desses tipos e
    /// objetos aninhados — exatamente o que GameSettings precisa.
    /// </summary>
    public static class JsonUtility
    {
        public static string ToJson(object obj) => ToJson(obj, false);

        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null)
            {
                return "{}";
            }

            var sb = new StringBuilder();
            WriteObject(sb, obj);
            return sb.ToString();
        }

        public static T FromJson<T>(string json) where T : new()
        {
            object parsed = MiniJson.Parse(json);
            if (!(parsed is Dictionary<string, object> map))
            {
                return default;
            }

            var target = new T();
            Populate(target, map);
            return target;
        }

        public static void FromJsonOverwrite(string json, object target)
        {
            if (MiniJson.Parse(json) is Dictionary<string, object> map)
            {
                Populate(target, map);
            }
        }

        private static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            FieldInfo[] all = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo f in all)
            {
                if (f.IsPublic || f.IsDefined(typeof(SerializeFieldAttribute), true))
                {
                    yield return f;
                }
            }
        }

        private static void WriteObject(StringBuilder sb, object obj)
        {
            sb.Append('{');
            bool first = true;
            foreach (FieldInfo field in SerializedFields(obj.GetType()))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(field.Name).Append("\":");
                WriteValue(sb, field.GetValue(obj));
            }

            sb.Append('}');
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case Enum e:
                    sb.Append(System.Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture));
                    return;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    return;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    return;
                case Array array:
                    sb.Append('[');
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteValue(sb, array.GetValue(k));
                    }

                    sb.Append(']');
                    return;
                default:
                    WriteObject(sb, value);
                    return;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        private static void Populate(object target, Dictionary<string, object> map)
        {
            foreach (FieldInfo field in SerializedFields(target.GetType()))
            {
                if (!map.TryGetValue(field.Name, out object raw))
                {
                    continue;
                }

                object converted = Convert(raw, field.FieldType);
                if (converted != null || !field.FieldType.IsValueType)
                {
                    field.SetValue(target, converted);
                }
            }
        }

        private static object Convert(object raw, Type type)
        {
            if (raw == null)
            {
                return null;
            }

            if (type.IsEnum)
            {
                return Enum.ToObject(type, (int)System.Convert.ToInt64(raw, CultureInfo.InvariantCulture));
            }

            if (type == typeof(string)) return raw as string ?? raw.ToString();
            if (type == typeof(bool)) return raw is bool b ? b : System.Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return System.Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return System.Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return System.Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return System.Convert.ToDouble(raw, CultureInfo.InvariantCulture);

            if (type.IsArray && raw is List<object> list)
            {
                Type element = type.GetElementType();
                Array array = Array.CreateInstance(element, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    array.SetValue(Convert(list[i], element), i);
                }

                return array;
            }

            if (raw is Dictionary<string, object> nested)
            {
                object instance = Activator.CreateInstance(type);
                Populate(instance, nested);
                return instance;
            }

            return null;
        }
    }

    /// <summary>Parser JSON enxuto para o <see cref="JsonUtility"/> do shim.</summary>
    internal static class MiniJson
    {
        internal static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            int index = 0;
            object value = ParseValue(json, ref index);
            return value;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON truncado");

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                    Expect(s, ref i, "true");
                    return true;
                case 'f':
                    Expect(s, ref i, "false");
                    return false;
                case 'n':
                    Expect(s, ref i, "null");
                    return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
            {
                throw new FormatException($"JSON inválido perto de {i}");
            }

            i += literal.Length;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return map;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("esperava ':'");
                i++;
                map[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON truncado");
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (s[i] == '}')
                {
                    i++;
                    return map;
                }

                throw new FormatException("esperava ',' ou '}'");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }

            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON truncado");
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (s[i] == ']')
                {
                    i++;
                    return list;
                }

                throw new FormatException("esperava ',' ou ']'");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("esperava string");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }

                i++;
            }

            i++; // "
            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }

            string text = s.Substring(start, i - start);
            if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0 && text.IndexOf('E') < 0
                && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                return l <= int.MaxValue && l >= int.MinValue ? (object)(int)l : l;
            }

            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

    public static class Debug
    {
        /// <summary>O harness liga isto para ver o que o jogo reclama.</summary>
        internal static bool Verbose = false;

        internal static readonly List<string> Warnings = new List<string>();

        internal static readonly List<string> Errors = new List<string>();

        public static void Log(object message)
        {
            if (Verbose) Console.WriteLine($"[log] {message}");
        }

        public static void Log(object message, Object context) => Log(message);

        public static void LogFormat(string format, params object[] args) => Log(string.Format(format, args));

        public static void LogWarning(object message)
        {
            Warnings.Add(message?.ToString() ?? string.Empty);
            if (Verbose) Console.WriteLine($"[aviso] {message}");
        }

        public static void LogWarning(object message, Object context) => LogWarning(message);

        public static void LogError(object message)
        {
            Errors.Add(message?.ToString() ?? string.Empty);
            Console.WriteLine($"[erro] {message}");
        }

        public static void LogError(object message, Object context) => LogError(message);

        public static void LogException(Exception exception)
        {
            Errors.Add(exception?.ToString() ?? string.Empty);
            Console.WriteLine($"[exceção] {exception}");
        }

        public static void Assert(bool condition, object message = null)
        {
            if (!condition) LogError($"Assert falhou: {message}");
        }

        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
        }
    }

    public static class Handheld
    {
        public static void Vibrate()
        {
        }
    }
}
