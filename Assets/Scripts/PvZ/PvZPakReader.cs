using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PvZReanim
{
    public class PvZPakReader
    {
        private const uint Magic = 0xBAC04AC0;
        private const uint Version = 0;
        private const byte XorKey = 0xF7;
        private const byte EndFlag = 0x80;

        private class PakEntry
        {
            public string Path;
            public long Offset;
            public int Size;
        }

        private readonly Dictionary<string, PakEntry> entries =
            new Dictionary<string, PakEntry>(
                StringComparer.OrdinalIgnoreCase
            );

        private string pakPath;
        private bool loaded;

        public bool IsLoaded =>
            loaded;

        public int FileCount =>
            entries.Count;

        public string PakPath =>
            pakPath;

        public bool Load(string path)
        {
            entries.Clear();
            loaded = false;
            pakPath = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError(
                    "[PvZPak] Ruta vacía."
                );

                return false;
            }

            if (!File.Exists(path))
            {
                Debug.LogError(
                    "[PvZPak] No existe el archivo:\n" +
                    path
                );

                return false;
            }

            try
            {
                byte[] data =
                    File.ReadAllBytes(path);

                if (data.Length < 8)
                {
                    Debug.LogError(
                        "[PvZPak] El archivo es demasiado pequeño."
                    );

                    return false;
                }

                DecodeXor(data);

                using MemoryStream stream = new MemoryStream(data);

                using BinaryReader reader = new BinaryReader(stream);

                uint magic = reader.ReadUInt32();

                uint version = reader.ReadUInt32();

                if (magic != Magic)
                {
                    Debug.LogError(
                        "[PvZPak] Magic inválido: 0x" +
                        magic.ToString("X8") +
                        " | Esperado: 0x" +
                        Magic.ToString("X8")
                    );

                    return false;
                }

                if (version != Version)
                {
                    Debug.LogError(
                        "[PvZPak] Versión inválida: " +
                        version +
                        " | Esperada: " +
                        Version
                    );

                    return false;
                }

                List<PakEntry> orderedEntries =
                    ReadDirectory(
                        reader
                    );

                long dataStart =
                    stream.Position;

                long currentOffset =
                    dataStart;

                for (int i = 0;
                     i < orderedEntries.Count;
                     i++)
                {
                    PakEntry entry =
                        orderedEntries[i];

                    entry.Offset =
                        currentOffset;

                    currentOffset +=
                        entry.Size;

                    string normalized =
                        NormalizePath(
                            entry.Path
                        );

                    if (!entries.ContainsKey(
                            normalized))
                    {
                        entries.Add(
                            normalized,
                            entry
                        );
                    }
                }

                pakPath = path;
                loaded = true;

                Debug.Log(
                    "[PvZPak] Cargado correctamente: " +
                    Path.GetFileName(path) +
                    " | Archivos: " +
                    entries.Count
                );

                return true;
            }
            catch (Exception ex)
            {
                entries.Clear();
                loaded = false;

                Debug.LogError(
                    "[PvZPak] Error leyendo PAK:\n" +
                    ex
                );

                return false;
            }
        }
        private List<PakEntry> ReadDirectory(
            BinaryReader reader)
        {
            List<PakEntry> result =
                new List<PakEntry>();

            while (reader.BaseStream.Position <
                   reader.BaseStream.Length)
            {
                byte flag =
                    reader.ReadByte();

                if (flag == EndFlag)
                    break;

                byte nameLength =
                    reader.ReadByte();

                if (nameLength == 0)
                {
                    throw new InvalidDataException(
                        "Entrada PAK con nombre vacío."
                    );
                }

                byte[] nameBytes =
                    reader.ReadBytes(
                        nameLength
                    );

                if (nameBytes.Length != nameLength)
                {
                    throw new EndOfStreamException(
                        "Nombre PAK incompleto."
                    );
                }

                string name =
                    Encoding.UTF8.GetString(
                        nameBytes
                    );

                uint size =
                    reader.ReadUInt32();

                reader.ReadInt64();

                if (size > int.MaxValue)
                {
                    throw new InvalidDataException(
                        "Archivo demasiado grande: " +
                        name
                    );
                }

                PakEntry entry =
                    new PakEntry();

                entry.Path =
                    name;

                entry.Size =
                    (int)size;

                result.Add(
                    entry
                );
            }

            return result;
        }

        private static void DecodeXor(
            byte[] data)
        {
            for (int i = 0;
                 i < data.Length;
                 i++)
            {
                data[i] ^=
                    XorKey;
            }
        }

        public bool Contains(
            string path)
        {
            if (!loaded)
                return false;

            return entries.ContainsKey(
                NormalizePath(path)
            );
        }

        public bool TryGetFile(
            string path,
            out byte[] data)
        {
            data = null;

            if (!loaded)
                return false;

            if (!entries.TryGetValue(
                    NormalizePath(path),
                    out PakEntry entry))
            {
                return false;
            }

            try
            {
                using FileStream file =
                    new FileStream(
                        pakPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    );

                file.Position =
                    entry.Offset;

                data =
                    new byte[
                        entry.Size
                    ];

                int totalRead = 0;

                while (totalRead <
                       entry.Size)
                {
                    int read =
                        file.Read(
                            data,
                            totalRead,
                            entry.Size -
                            totalRead
                        );

                    if (read <= 0)
                    {
                        data = null;
                        return false;
                    }

                    totalRead +=
                        read;
                }

                for (int i = 0;
                     i < data.Length;
                     i++)
                {
                    data[i] ^=
                        XorKey;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PvZPak] Error leyendo '" +
                    path +
                    "':\n" +
                    ex
                );

                data = null;
                return false;
            }
        }

        public bool TryGetFileText(
            string path,
            out string text)
        {
            text = null;

            if (!TryGetFile(
                    path,
                    out byte[] data))
            {
                return false;
            }

            text =
                Encoding.UTF8.GetString(
                    data
                );

            return true;
        }

        private static string NormalizePath(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized =
                path.Replace(
                    '\\',
                    '/'
                );

            while (normalized.StartsWith(
                       "./",
                       StringComparison.Ordinal))
            {
                normalized =
                    normalized.Substring(2);
            }

            return normalized.TrimStart(
                '/'
            );
        }

        public List<string> Find(
            string query)
        {
            List<string> result =
                new List<string>();

            if (!loaded)
                return result;

            string normalizedQuery =
                NormalizePath(query);

            foreach (string key in entries.Keys)
            {
                if (key.IndexOf(
                        normalizedQuery,
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0)
                {
                    result.Add(key);
                }
            }

            return result;
        }

        public List<string> GetFiles()
        {
            return new List<string>(
                entries.Keys
            );
        }
    }
}