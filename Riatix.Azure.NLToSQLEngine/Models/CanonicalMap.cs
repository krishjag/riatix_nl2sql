using System;
using System.Collections.Generic;
using MessagePack;

namespace Riatix.Azure.NLToSQLEngine.Models
{
    /// <summary>
    /// Represents the prepacked canonical mapping between
    /// Azure product canonical names and their alias variations.
    /// Serialized as a MessagePack binary file (canonicalMap.bin).
    /// </summary>
    [MessagePackObject]
    public class CanonicalMap
    {
        // Optional version field (for traceability)
        [Key(0)]
        public string Version { get; set; } = $"v{DateTime.UtcNow:yyyy.MM.dd.HHmm}";

        // The actual canonical-to-alias mapping
        [Key(1)]
        public Dictionary<string, List<string>> Map { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);
    }
}
