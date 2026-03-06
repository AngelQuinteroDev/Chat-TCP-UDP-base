using System.Collections.Concurrent;

namespace ChatServer.Models
{
    /// <summary>
    /// Object Pool de buffers de bytes.
    ///
    /// CONCEPTO: En vez de crear y destruir arreglos de bytes constantemente
    /// (lo que presiona al Garbage Collector), pre-alocamos N buffers y los
    /// reutilizamos. Cuando un handler necesita un buffer llama Rent(),
    /// lo usa, y al terminar llama Return() para que vuelva al pool.
    ///
    /// En .NET 5+ existe System.Buffers.ArrayPool<byte>.Shared que hace
    /// exactamente esto. Aquí lo implementamos manualmente para mostrar
    /// el concepto explícitamente (valor académico).
    /// </summary>
    public sealed class ByteBufferPool
    {
        public static readonly ByteBufferPool Shared = new ByteBufferPool();

        private readonly ConcurrentBag<byte[]> _pool = new();
        private readonly int _bufferSize;
        private readonly int _maxPooled;

        public ByteBufferPool(int bufferSize = 65536, int maxPooled = 64)
        {
            _bufferSize = bufferSize;
            _maxPooled  = maxPooled;

            // Pre-alocar la mitad del pool al inicio
            for (int i = 0; i < maxPooled / 2; i++)
                _pool.Add(new byte[bufferSize]);
        }

        /// <summary>
        /// Toma un buffer del pool (o crea uno nuevo si el pool está vacío).
        /// El llamador DEBE devolver el buffer con Return() cuando termine.
        /// </summary>
        public byte[] Rent()
        {
            if (_pool.TryTake(out var buf))
                return buf;

            // Pool agotado → crear uno temporal
            return new byte[_bufferSize];
        }

        /// <summary>
        /// Devuelve el buffer al pool para reutilización futura.
        /// NO usar el buffer después de llamar Return().
        /// </summary>
        public void Return(byte[] buffer)
        {
            if (buffer == null || buffer.Length != _bufferSize)
                return; // Buffer de tamaño distinto → desechar

            if (_pool.Count < _maxPooled)
                _pool.Add(buffer);
            // Si el pool está lleno, el buffer simplemente es recolectado por el GC
        }
    }
}
