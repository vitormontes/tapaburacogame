// Shim de UnityEngine — tipos matemáticos.
// Reimplementação honesta o suficiente para compilar e RODAR a camada de apresentação fora do
// editor. Fórmulas copiadas do comportamento documentado da Unity (não do código dela).
using System;
using System.Globalization;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 down => new Vector2(0f, -1f);
        public static Vector2 left => new Vector2(-1f, 0f);
        public static Vector2 right => new Vector2(1f, 0f);

        public float magnitude => Mathf.Sqrt(x * x + y * y);

        public float sqrMagnitude => x * x + y * y;

        public Vector2 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-5f ? new Vector2(x / m, y / m) : zero;
            }
        }

        public float this[int index]
        {
            get => index == 0 ? x : y;
            set
            {
                if (index == 0) x = value;
                else y = value;
            }
        }

        public void Set(float newX, float newY)
        {
            x = newX;
            y = newY;
        }

        public void Normalize()
        {
            this = normalized;
        }

        public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);

        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
            => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));

        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
        {
            Vector2 delta = target - current;
            float dist = delta.magnitude;
            if (dist <= maxDelta || dist < 1e-6f) return target;
            return current + delta / dist * maxDelta;
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);

        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);

        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);

        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);

        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);

        public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);

        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);

        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-10f;

        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);

        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);

        public bool Equals(Vector2 other) => x == other.x && y == other.y;

        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", x, y);
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y)
        {
            this.x = x;
            this.y = y;
            z = 0f;
        }

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);

        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public float this[int index]
        {
            get => index == 0 ? x : index == 1 ? y : z;
            set
            {
                if (index == 0) x = value;
                else if (index == 1) y = value;
                else z = value;
            }
        }

        public void Set(float newX, float newY, float newZ)
        {
            x = newX;
            y = newY;
            z = newZ;
        }

        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
            => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);

        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);

        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;

        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

        public bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object obj) => obj is Vector3 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", x, y, z);
    }

    public struct Vector4 : IEquatable<Vector4>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vector4(float x, float y, float z) : this(x, y, z, 0f)
        {
        }

        public static Vector4 zero => new Vector4(0f, 0f, 0f, 0f);
        public static Vector4 one => new Vector4(1f, 1f, 1f, 1f);

        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z + w * w);

        public float sqrMagnitude => x * x + y * y + z * z + w * w;

        public Vector4 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-5f ? new Vector4(x / m, y / m, z / m, w / m) : zero;
            }
        }

        public float this[int index]
        {
            get => index == 0 ? x : index == 1 ? y : index == 2 ? z : w;
            set
            {
                if (index == 0) x = value;
                else if (index == 1) y = value;
                else if (index == 2) z = value;
                else w = value;
            }
        }

        public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);

        public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);

        public static Vector4 operator *(Vector4 a, float d) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);

        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);

        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);

        public bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;

        public override bool Equals(object obj) => obj is Vector4 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2}, {3:F2})", x, y, z, w);
    }

    /// <summary>
    /// Quaternion reduzido: o jogo só usa rotação no eixo Z (cartaz 2D), então a conversão
    /// Euler↔quaternion é feita de forma completa mas sem otimização.
    /// </summary>
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public Vector3 eulerAngles
        {
            get
            {
                // Suficiente para o uso 2D: extrai o ângulo em Z.
                float sinp = 2f * (w * z + x * y);
                float cosp = 1f - 2f * (y * y + z * z);
                float angle = Mathf.Atan2(sinp, cosp) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                return new Vector3(0f, 0f, angle);
            }
            set => this = Euler(value);
        }

        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            float hx = xDeg * Mathf.Deg2Rad * 0.5f;
            float hy = yDeg * Mathf.Deg2Rad * 0.5f;
            float hz = zDeg * Mathf.Deg2Rad * 0.5f;
            float cx = Mathf.Cos(hx), sx = Mathf.Sin(hx);
            float cy = Mathf.Cos(hy), sy = Mathf.Sin(hy);
            float cz = Mathf.Cos(hz), sz = Mathf.Sin(hz);
            return new Quaternion(
                sx * cy * cz - cx * sy * sz,
                cx * sy * cz + sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }

        public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            float half = angleDeg * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, Mathf.Cos(half));
        }

        public static Quaternion operator *(Quaternion a, Quaternion b)
            => new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float num = q.x * 2f, num2 = q.y * 2f, num3 = q.z * 2f;
            float num4 = q.x * num, num5 = q.y * num2, num6 = q.z * num3;
            float num7 = q.x * num2, num8 = q.x * num3, num9 = q.y * num3;
            float num10 = q.w * num, num11 = q.w * num2, num12 = q.w * num3;
            return new Vector3(
                (1f - (num5 + num6)) * v.x + (num7 - num12) * v.y + (num8 + num11) * v.z,
                (num7 + num12) * v.x + (1f - (num4 + num6)) * v.y + (num9 - num10) * v.z,
                (num8 - num11) * v.x + (num9 + num10) * v.y + (1f - (num4 + num5)) * v.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b)
            => Mathf.Abs(a.x - b.x) < 1e-5f && Mathf.Abs(a.y - b.y) < 1e-5f
               && Mathf.Abs(a.z - b.z) < 1e-5f && Mathf.Abs(a.w - b.w) < 1e-5f;

        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => Lerp(a, b, Mathf.Clamp01(t));

        public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Quaternion(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t,
                a.w + (b.w - a.w) * t);
        }

        public bool Equals(Quaternion other) => x == other.x && y == other.y && z == other.z && w == other.w;

        public override bool Equals(object obj) => obj is Quaternion other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();

        public override string ToString() => eulerAngles.ToString();
    }

    public struct Rect : IEquatable<Rect>
    {
        private float _x;
        private float _y;
        private float _w;
        private float _h;

        public Rect(float x, float y, float width, float height)
        {
            _x = x;
            _y = y;
            _w = width;
            _h = height;
        }

        public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y)
        {
        }

        public static Rect zero => new Rect(0f, 0f, 0f, 0f);

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
            => new Rect(xmin, ymin, xmax - xmin, ymax - ymin);

        public float x
        {
            get => _x;
            set => _x = value;
        }

        public float y
        {
            get => _y;
            set => _y = value;
        }

        public float width
        {
            get => _w;
            set => _w = value;
        }

        public float height
        {
            get => _h;
            set => _h = value;
        }

        public Vector2 position
        {
            get => new Vector2(_x, _y);
            set
            {
                _x = value.x;
                _y = value.y;
            }
        }

        public Vector2 size
        {
            get => new Vector2(_w, _h);
            set
            {
                _w = value.x;
                _h = value.y;
            }
        }

        public Vector2 center
        {
            get => new Vector2(_x + _w * 0.5f, _y + _h * 0.5f);
            set
            {
                _x = value.x - _w * 0.5f;
                _y = value.y - _h * 0.5f;
            }
        }

        public Vector2 min
        {
            get => new Vector2(xMin, yMin);
            set
            {
                xMin = value.x;
                yMin = value.y;
            }
        }

        public Vector2 max
        {
            get => new Vector2(xMax, yMax);
            set
            {
                xMax = value.x;
                yMax = value.y;
            }
        }

        public float xMin
        {
            get => _x;
            set
            {
                float old = xMax;
                _x = value;
                _w = old - _x;
            }
        }

        public float yMin
        {
            get => _y;
            set
            {
                float old = yMax;
                _y = value;
                _h = old - _y;
            }
        }

        public float xMax
        {
            get => _x + _w;
            set => _w = value - _x;
        }

        public float yMax
        {
            get => _y + _h;
            set => _h = value - _y;
        }

        public bool Contains(Vector2 point)
            => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;

        public bool Overlaps(Rect other)
            => other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;

        public static bool operator ==(Rect a, Rect b)
            => a._x == b._x && a._y == b._y && a._w == b._w && a._h == b._h;

        public static bool operator !=(Rect a, Rect b) => !(a == b);

        public bool Equals(Rect other) => this == other;

        public override bool Equals(object obj) => obj is Rect other && Equals(other);

        public override int GetHashCode()
            => _x.GetHashCode() ^ (_w.GetHashCode() << 2) ^ (_y.GetHashCode() >> 2) ^ (_h.GetHashCode() >> 1);

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "(x:{0:F2}, y:{1:F2}, w:{2:F2}, h:{3:F2})", _x, _y, _w, _h);
    }

    public struct Color : IEquatable<Color>
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(float r, float g, float b) : this(r, g, b, 1f)
        {
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color yellow => new Color(1f, 0.92f, 0.016f, 1f);
        public static Color cyan => new Color(0f, 1f, 1f, 1f);
        public static Color magenta => new Color(1f, 0f, 1f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color grey => gray;

        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;

        public Color linear => this;

        public Color gamma => this;

        public float this[int index]
        {
            get => index == 0 ? r : index == 1 ? g : index == 2 ? b : a;
            set
            {
                if (index == 0) r = value;
                else if (index == 1) g = value;
                else if (index == 2) b = value;
                else a = value;
            }
        }

        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }

        public static Color LerpUnclamped(Color x, Color y, float t)
            => new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);

        public static Color operator +(Color x, Color y) => new Color(x.r + y.r, x.g + y.g, x.b + y.b, x.a + y.a);

        public static Color operator -(Color x, Color y) => new Color(x.r - y.r, x.g - y.g, x.b - y.b, x.a - y.a);

        public static Color operator *(Color x, Color y) => new Color(x.r * y.r, x.g * y.g, x.b * y.b, x.a * y.a);

        public static Color operator *(Color x, float d) => new Color(x.r * d, x.g * d, x.b * d, x.a * d);

        public static Color operator *(float d, Color x) => new Color(x.r * d, x.g * d, x.b * d, x.a * d);

        public static Color operator /(Color x, float d) => new Color(x.r / d, x.g / d, x.b / d, x.a / d);

        public static bool operator ==(Color x, Color y)
            => Mathf.Abs(x.r - y.r) < 1e-4f && Mathf.Abs(x.g - y.g) < 1e-4f
               && Mathf.Abs(x.b - y.b) < 1e-4f && Mathf.Abs(x.a - y.a) < 1e-4f;

        public static bool operator !=(Color x, Color y) => !(x == y);

        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);

        public static implicit operator Color(Vector4 v) => new Color(v.x, v.y, v.z, v.w);

        public bool Equals(Color other) => r == other.r && g == other.g && b == other.b && a == other.a;

        public override bool Equals(object obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => ((Vector4)this).GetHashCode();

        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "RGBA({0:F3}, {1:F3}, {2:F3}, {3:F3})", r, g, b, a);
    }

    public struct Color32 : IEquatable<Color32>
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static implicit operator Color32(Color c)
            => new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));

        public static implicit operator Color(Color32 c)
            => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public static Color32 Lerp(Color32 a, Color32 b, float t) => (Color32)Color.Lerp(a, b, t);

        public bool Equals(Color32 other) => r == other.r && g == other.g && b == other.b && a == other.a;

        public override bool Equals(object obj) => obj is Color32 other && Equals(other);

        public override int GetHashCode() => (a << 24) | (r << 16) | (g << 8) | b;

        public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI * 2f / 360f;
        public const float Rad2Deg = 360f / (PI * 2f);
        public const float Epsilon = 1.401298E-45f;

        public static float Abs(float v) => Math.Abs(v);

        public static int Abs(int v) => Math.Abs(v);

        public static float Min(float a, float b) => a < b ? a : b;

        public static int Min(int a, int b) => a < b ? a : b;

        public static float Min(params float[] values)
        {
            float m = float.PositiveInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] < m) m = values[i];
            return m;
        }

        public static float Max(float a, float b) => a > b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static float Max(params float[] values)
        {
            float m = float.NegativeInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] > m) m = values[i];
            return m;
        }

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

        public static float InverseLerp(float a, float b, float value)
            => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((value - a) / (b - a));

        public static float MoveTowards(float current, float target, float maxDelta)
            => Abs(target - current) <= maxDelta ? target : current + Sign(target - current) * maxDelta;

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }

        public static float Sin(float f) => (float)Math.Sin(f);

        public static float Cos(float f) => (float)Math.Cos(f);

        public static float Tan(float f) => (float)Math.Tan(f);

        public static float Asin(float f) => (float)Math.Asin(f);

        public static float Acos(float f) => (float)Math.Acos(f);

        public static float Atan(float f) => (float)Math.Atan(f);

        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);

        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        public static float Pow(float f, float p) => (float)Math.Pow(f, p);

        public static float Exp(float f) => (float)Math.Exp(f);

        public static float Log(float f) => (float)Math.Log(f);

        public static float Log(float f, float b) => (float)Math.Log(f, b);

        public static float Log10(float f) => (float)Math.Log10(f);

        public static float Floor(float f) => (float)Math.Floor(f);

        public static float Ceil(float f) => (float)Math.Ceiling(f);

        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.ToEven);

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);

        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);

        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Abs(t - length);
        }

        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }

        public static bool Approximately(float a, float b)
            => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime,
            float maxSpeed = Infinity, float deltaTime = 0.02f)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = Clamp(current - target, -maxSpeed * smoothTime, maxSpeed * smoothTime);
            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            return target + (change + temp) * exp;
        }

        /// <summary>
        /// APROXIMAÇÃO: ruído de valor suave com interpolação cúbica. Não é o Perlin da Unity,
        /// mas é contínuo em [0,1] — basta para texturas procedurais do shim.
        /// </summary>
        public static float PerlinNoise(float x, float y)
        {
            int xi = FloorToInt(x);
            int yi = FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi);
            float b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1);
            float d = Hash(xi + 1, yi + 1);
            return Clamp01(a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }

    /// <summary>Gerador aleatório global da Unity, reimplementado sobre System.Random.</summary>
    public static class Random
    {
        private static System.Random _rng = new System.Random(20240501);

        public static void InitState(int seed) => _rng = new System.Random(seed);

        public static int seed
        {
            get => 0;
            set => InitState(value);
        }

        public static float value => (float)_rng.NextDouble();

        public static float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public static int Range(int min, int max) => max <= min ? min : _rng.Next(min, max);

        public static Vector2 insideUnitCircle
        {
            get
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(value);
                return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
        }

        public static Vector3 insideUnitSphere => new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized * value;

        public static Color ColorHSV() => new Color(value, value, value, 1f);

        public static float rotation => Range(0f, 360f);
    }

    /// <summary>Margens em pixels — igual ao RectOffset da Unity.</summary>
    public sealed class RectOffset
    {
        public RectOffset()
        {
        }

        public RectOffset(int left, int right, int top, int bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }

        public int left { get; set; }

        public int right { get; set; }

        public int top { get; set; }

        public int bottom { get; set; }

        public int horizontal => left + right;

        public int vertical => top + bottom;

        public Rect Add(Rect rect)
            => new Rect(rect.x - left, rect.y - top, rect.width + horizontal, rect.height + vertical);

        public Rect Remove(Rect rect)
            => new Rect(rect.x + left, rect.y + top, rect.width - horizontal, rect.height - vertical);

        public override string ToString() => $"RectOffset (l:{left} r:{right} t:{top} b:{bottom})";
    }

    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8,
    }

    public enum Space
    {
        World = 0,
        Self = 1,
    }
}
