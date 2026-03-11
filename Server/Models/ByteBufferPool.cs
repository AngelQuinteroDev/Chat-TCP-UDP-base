using System.Collections.Concurrent;

namespace ChatServer.Models
{
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

            for (int i = 0; i < maxPooled / 2; i++)
                _pool.Add(new byte[bufferSize]);
        }
        public byte[] Rent()
        {
            if (_pool.TryTake(out var buf))
                return buf;

            return new byte[_bufferSize];
        }

        public void Return(byte[] buffer)
        {
            if (buffer == null || buffer.Length != _bufferSize)
                return;

            if (_pool.Count < _maxPooled)
                _pool.Add(buffer);
        }
    }
}
