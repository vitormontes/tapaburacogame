using System;
using System.Diagnostics;
using System.Threading;

namespace TapaBuraco.Core
{
    /// <summary>
    /// Solver misère exato com tempo-limite — o <c>vence()</c> do protótipo web.
    /// Busca em profundidade sobre o bitmask: o jogador da vez vence se algum trecho legal
    /// deixa o adversário perdido; tabuleiro vazio = o adversário acabou de tapar o último
    /// buraco, então quem está na vez venceu.
    ///
    /// Tabela de transposição em endereçamento aberto: chave+1 (0 = vaga livre) e valor
    /// 1 = perde / 2 = vence; 4 Mi de vagas (~20 MB), alocada na primeira busca e mantida entre
    /// lances e partidas. Passou de 70% de ocupação, zera tudo. A posição e o espelho pela
    /// diagonal dividem a mesma entrada (chave = menor das duas máscaras).
    ///
    /// Não é thread-safe: uma busca por vez (a <see cref="GameSession"/> enfileira as pensadas).
    /// </summary>
    public sealed class MisereSolver
    {
        /// <summary>Sem prazo.</summary>
        public const int NoTimeLimit = -1;

        private const int Bits = 22;
        private const int Size = 1 << Bits;
        private const int SlotMask = Size - 1;
        private const int ClearThreshold = (int)(Size * 0.7);

        private const byte Lose = 1;
        private const byte Win = 2;

        private int[] _keys;
        private byte[] _values;
        private int _used;

        private long _nodes;
        private long _deadlineTicks;
        private CancellationToken _token;

        /// <summary>Entradas ocupadas na tabela (diagnóstico).</summary>
        public int TableCount => _used;

        /// <summary>Nós visitados na última busca (diagnóstico).</summary>
        public long LastNodes => _nodes;

        /// <summary>
        /// true se o jogador da vez vence a partir de <paramref name="mask"/>; null se o prazo
        /// acabou (ou a busca foi cancelada) antes da prova.
        /// </summary>
        public bool? CurrentPlayerWins(uint mask, int timeLimitMs = NoTimeLimit, CancellationToken token = default)
        {
            Prepare(timeLimitMs, token);
            try
            {
                return Wins(mask & Board.FullMask);
            }
            catch (SearchAborted)
            {
                return null;
            }
        }

        /// <summary>
        /// Procura, na ordem de <paramref name="candidates"/>, um trecho legal que deixe o
        /// adversário perdido. Devolve o índice do trecho, -1 se nenhum vence, ou null se o
        /// prazo acabou antes da prova.
        /// </summary>
        public int? FindWinningMove(uint mask, int[] candidates, int count, int timeLimitMs = NoTimeLimit, CancellationToken token = default)
        {
            mask &= Board.FullMask;
            Prepare(timeLimitMs, token);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int segment = candidates[i];
                    uint s = Segments.MaskOf(segment);
                    if ((s & mask) == s && !Wins(mask ^ s))
                    {
                        return segment;
                    }
                }

                return -1;
            }
            catch (SearchAborted)
            {
                return null;
            }
        }

        /// <summary>Esvazia a tabela de transposição.</summary>
        public void Clear()
        {
            if (_keys != null)
            {
                Array.Clear(_keys, 0, _keys.Length);
            }

            _used = 0;
        }

        private void Prepare(int timeLimitMs, CancellationToken token)
        {
            if (_keys == null)
            {
                _keys = new int[Size];
                _values = new byte[Size];
            }

            _nodes = 0;
            _token = token;
            _deadlineTicks = timeLimitMs < 0
                ? long.MaxValue
                : Stopwatch.GetTimestamp() + (timeLimitMs * Stopwatch.Frequency / 1000L);
        }

        private bool Wins(uint mask)
        {
            if (mask == 0u)
            {
                return true;
            }

            uint mirror = Board.Transpose(mask);
            int key = (int)(mask < mirror ? mask : mirror);
            byte known = Read(key);
            if (known != 0)
            {
                return known == Win;
            }

            if ((++_nodes & 4095) == 0
                && (Stopwatch.GetTimestamp() > _deadlineTicks || _token.IsCancellationRequested))
            {
                throw SearchAborted.Instance;
            }

            bool wins = false;
            int count = Segments.Count;
            for (int k = 0; k < count; k++)
            {
                uint s = Segments.MaskOf(Segments.InSearchOrder(k));
                if ((s & mask) == s && !Wins(mask ^ s))
                {
                    wins = true;
                    break;
                }
            }

            Write(key, wins ? Win : Lose);
            return wins;
        }

        private static int Slot(int key) => (int)(((uint)key * 0x9E3779B1u) >> (32 - Bits));

        private byte Read(int key)
        {
            int h = Slot(key);
            int stored = key + 1;
            while (true)
            {
                int x = _keys[h];
                if (x == 0)
                {
                    return 0;
                }

                if (x == stored)
                {
                    return _values[h];
                }

                h = (h + 1) & SlotMask;
            }
        }

        private void Write(int key, byte value)
        {
            if (_used > ClearThreshold)
            {
                Array.Clear(_keys, 0, _keys.Length);
                _used = 0;
            }

            int h = Slot(key);
            int stored = key + 1;
            while (true)
            {
                int x = _keys[h];
                if (x == 0)
                {
                    _keys[h] = stored;
                    _values[h] = value;
                    _used++;
                    return;
                }

                if (x == stored)
                {
                    _values[h] = value;
                    return;
                }

                h = (h + 1) & SlotMask;
            }
        }

        /// <summary>Prazo estourado ou busca cancelada: desenrola a recursão de uma vez.</summary>
        private sealed class SearchAborted : Exception
        {
            public static readonly SearchAborted Instance = new SearchAborted();
        }
    }
}
