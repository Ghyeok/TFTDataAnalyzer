using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TFTAnalyzer.Editor.Persistence
{
    /// <summary>A derived hash-only cache; observation records remain the authority after any failure.</summary>
    internal sealed class ProcessedMatchIndex
    {
        private readonly HashSet<string> _hashes = new HashSet<string>(StringComparer.Ordinal);

        internal bool Add(string hash) => _hashes.Add(hash);
        internal bool Contains(string hash) => _hashes.Contains(hash);

        /// <summary>Flushes a complete replacement before publishing it atomically in the same directory.</summary>
        internal void Save(AnalyzerStorePaths paths)
        {
            paths.EnsureSafe();
            var temporary = paths.ProcessedMatchIndexPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var json = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["hashes"] = new JArray(_hashes.OrderBy(value => value, StringComparer.Ordinal))
                }.ToString(Formatting.None) + "\n";
                var bytes = new UTF8Encoding(false, true).GetBytes(json);
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(paths.ProcessedMatchIndexPath)) File.Replace(temporary, paths.ProcessedMatchIndexPath, null);
                else File.Move(temporary, paths.ProcessedMatchIndexPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
