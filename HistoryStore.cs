using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SpiritOfTheSamuraiUltrawide
{
    // Plain managed data only; all file IO and serialization may run on a worker thread.
    internal static class HistoryStore
    {
        [DataContract]
        internal sealed class Line
        {
            [DataMember] public string Speaker;
            [DataMember] public string Text;
            [DataMember] public string VoicePath;
            [DataMember] public float Volume = 1;
            [DataMember] public float Pitch = 1;
        }

        [DataContract]
        internal sealed class Document
        {
            [DataMember] public int Version = 1;
            [DataMember] public List<Line> Lines = new List<Line>();
        }

        internal static Document Read(string path, int limit)
        {
            if (!File.Exists(path)) return new Document();
            try { return ReadOne(path, limit); }
            catch
            {
                if (File.Exists(path + ".bak")) return ReadOne(path + ".bak", limit);
                throw;
            }
        }

        private static Document ReadOne(string path, int limit)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("History file exceeds 16 MB.");
            Document document;
            using (var stream = File.OpenRead(path))
                document = (Document)new DataContractJsonSerializer(typeof(Document)).ReadObject(stream);
            if (document == null || document.Version != 1 || document.Lines == null) throw new InvalidDataException("Unsupported history format.");
            document.Lines.RemoveAll(line => line == null || string.IsNullOrWhiteSpace(line.Text) || line.Text.Length > 16384 ||
                (line.Speaker != null && line.Speaker.Length > 1024) || (line.VoicePath != null && line.VoicePath.Length > 2048));
            int excess = document.Lines.Count - Math.Max(10, Math.Min(2000, limit));
            if (excess > 0) document.Lines.RemoveRange(0, excess);
            foreach (Line line in document.Lines)
            {
                if (float.IsNaN(line.Volume) || float.IsInfinity(line.Volume)) line.Volume = 1;
                if (float.IsNaN(line.Pitch) || float.IsInfinity(line.Pitch)) line.Pitch = 1;
                line.Volume = Math.Max(0, Math.Min(1, line.Volume));
                line.Pitch = Math.Max(-3, Math.Min(3, line.Pitch));
            }
            return document;
        }

        internal static void Write(string path, Document document, bool clearBackup = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(Document)).WriteObject(stream, document);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
            // A user-requested Clear must not leave their old dialogue in the recovery copy.
            if (clearBackup && File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
