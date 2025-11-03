using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using Riatix.Azure.NLToSQLEngine.Models;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    /// <summary>
    /// Loads a MessagePack-compressed canonical map once and caches it in memory.
    /// </summary>
    public class CanonicalMapLoader : ICanonicalMapLoader
    {
        private readonly string _path;
        private CanonicalMap? _cache;
        private readonly object _sync = new();

        public CanonicalMapLoader(string path = "Assets/canonicalMap.bin")
        {
            _path = path;
        }

        public Dictionary<string, List<string>> Load()
        {
            if (_cache != null)
                return _cache.Map;

            lock (_sync)
            {
                if (_cache != null)
                    return _cache.Map;

                if (!File.Exists(_path))
                    throw new FileNotFoundException($"Canonical map not found: {_path}");

                var bytes = File.ReadAllBytes(_path);

                _cache = MessagePackSerializer.Deserialize<CanonicalMap>(
                    bytes,
                    MessagePackSerializerOptions.Standard
                        .WithCompression(MessagePackCompression.Lz4BlockArray));

                Console.WriteLine($"[CanonicalMapLoader] Loaded {_cache.Map.Count} entries (v{_cache.Version})");

                return _cache.Map;
            }
        }
    }
}
