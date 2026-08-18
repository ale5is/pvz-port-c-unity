using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimCompiledLoader
    {
        private const uint COMPILED_DEFINITION_MAGIC = 0x43444631;

        // =========================================================
        // HEADER
        // =========================================================

        private struct CompiledDefinitionHeader
        {
            public uint cookie;
            public uint uncompressedSize;
            public uint dataOffset;
        }

        // =========================================================
        // PUBLIC
        // =========================================================

        public static PvZReanimDefinition LoadBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] Datos vacíos."
                );

                return null;
            }

            try
            {
                CompiledDefinitionHeader header;

                if (!ReadHeader(
                        data,
                        out header))
                {
                    return null;
                }

                Debug.Log(
                    "[PvZReanimCompiledLoader] Header OK | " +
                    "Uncompressed: " +
                    header.uncompressedSize +
                    " | Offset: " +
                    header.dataOffset
                );

                byte[] compressed =
                    ExtractCompressedData(
                        data,
                        header
                    );

                if (compressed == null)
                {
                    return null;
                }

                byte[] uncompressed =
                    DecompressZlib(
                        compressed,
                        (int)header.uncompressedSize
                    );

                if (uncompressed == null)
                {
                    return null;
                }

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "Descompresión OK | Bytes: " +
                    uncompressed.Length
                );

                PvZReanimDefinition definition =
                    ParseDefinition(
                        uncompressed
                    );

                if (definition == null)
                {
                    Debug.LogError(
                        "[PvZReanimCompiledLoader] " +
                        "No se pudo reconstruir la definición."
                    );

                    return null;
                }

                return definition;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Error:\n" +
                    exception
                );

                return null;
            }
        }

        // =========================================================
        // HEADER
        // =========================================================

        private static bool ReadHeader(
            byte[] data,
            out CompiledDefinitionHeader header)
        {
            header =
                new CompiledDefinitionHeader();

            if (data.Length < 12)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Archivo demasiado pequeño."
                );

                return false;
            }

            header.cookie =
                BitConverter.ToUInt32(
                    data,
                    0
                );

            header.uncompressedSize =
                BitConverter.ToUInt32(
                    data,
                    4
                );

            header.dataOffset =
                BitConverter.ToUInt32(
                    data,
                    8
                );

            if (header.cookie !=
                COMPILED_DEFINITION_MAGIC)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Magic inválido: 0x" +
                    header.cookie.ToString("X8")
                );

                return false;
            }

            if (header.dataOffset >= data.Length)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "DataOffset inválido: " +
                    header.dataOffset +
                    " / " +
                    data.Length
                );

                return false;
            }

            return true;
        }

        // =========================================================
        // COMPRESSED DATA
        // =========================================================

        private static byte[] ExtractCompressedData(
            byte[] data,
            CompiledDefinitionHeader header)
        {
            int offset =
                (int)header.dataOffset;

            int size =
                data.Length - offset;

            if (size <= 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "No hay datos comprimidos."
                );

                return null;
            }

            byte[] result =
                new byte[size];

            Buffer.BlockCopy(
                data,
                offset,
                result,
                0,
                size
            );

            return result;
        }

        // =========================================================
        // ZLIB
        // =========================================================

        private static byte[] DecompressZlib(
            byte[] compressed,
            int expectedSize)
        {
            if (compressed == null ||
                compressed.Length < 6)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Datos comprimidos inválidos."
                );

                return null;
            }

            /*
             * ResoddedFramework usa zlib::uncompress().
             *
             * El formato zlib contiene:
             *
             * 2 bytes  -> header zlib
             * N bytes  -> DEFLATE
             * 4 bytes  -> Adler32
             *
             * DeflateStream trabaja con el bloque
             * DEFLATE, por lo que quitamos header y trailer.
             */

            int deflateOffset = 2;

            int deflateSize =
                compressed.Length - 6;

            if (deflateSize <= 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Bloque DEFLATE vacío."
                );

                return null;
            }

            byte[] deflateData =
                new byte[deflateSize];

            Buffer.BlockCopy(
                compressed,
                deflateOffset,
                deflateData,
                0,
                deflateSize
            );

            try
            {
                using (MemoryStream input =
                    new MemoryStream(deflateData))
                using (DeflateStream deflate =
                    new DeflateStream(
                        input,
                        CompressionMode.Decompress
                    ))
                using (MemoryStream output =
                    new MemoryStream(
                        expectedSize > 0
                            ? expectedSize
                            : 4096
                    ))
                {
                    deflate.CopyTo(output);

                    byte[] result =
                        output.ToArray();

                    if (expectedSize > 0 &&
                        result.Length != expectedSize)
                    {
                        Debug.LogWarning(
                            "[PvZReanimCompiledLoader] " +
                            "Tamaño descomprimido diferente. " +
                            "Esperado: " +
                            expectedSize +
                            " | Real: " +
                            result.Length
                        );
                    }

                    return result;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Error descomprimiendo ZLIB:\n" +
                    exception
                );

                return null;
            }
        }

        // =========================================================
        // DEFINITION
        // =========================================================

        private static PvZReanimDefinition ParseDefinition(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                return null;
            }

            using (MemoryStream stream =
                new MemoryStream(data))
            using (BinaryReader reader =
                new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    true
                ))
            {
                PvZReanimDefinition definition =
                    ScriptableObject.CreateInstance<
                        PvZReanimDefinition
                    >();

                /*
                 * ReanimatorDefinition:
                 *
                 * track -> ARRAY
                 * fps   -> FLOAT
                 */

                uint trackCount =
                    reader.ReadUInt32();

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "Tracks: " +
                    trackCount
                );

                if (trackCount > 10000)
                {
                    Debug.LogError(
                        "[PvZReanimCompiledLoader] " +
                        "TrackCount inválido: " +
                        trackCount
                    );

                    return null;
                }

                definition.tracks.Clear();

                for (uint i = 0;
                     i < trackCount;
                     i++)
                {
                    PvZReanimTrack track =
                        ReadTrack(
                            reader
                        );

                    if (track == null)
                    {
                        Debug.LogError(
                            "[PvZReanimCompiledLoader] " +
                            "No se pudo leer track " +
                            i
                        );

                        return null;
                    }

                    definition.tracks.Add(
                        track
                    );
                }

                definition.fps =
                    reader.ReadSingle();

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "FPS: " +
                    definition.fps
                );

                return definition;
            }
        }

        // =========================================================
        // TRACK
        // =========================================================

        private static PvZReanimTrack ReadTrack(
            BinaryReader reader)
        {
            string name =
                ReadString(
                    reader
                );

            if (name == null)
            {
                return null;
            }

            PvZReanimTrack track =
                new PvZReanimTrack(
                    name
                );

            uint transformCount =
                reader.ReadUInt32();

            Debug.Log(
                "[PvZReanimCompiledLoader] " +
                "Track: " +
                name +
                " | Transforms: " +
                transformCount
            );

            if (transformCount > 100000)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "TransformCount inválido: " +
                    transformCount
                );

                return null;
            }

            track.transforms.Clear();

            for (uint i = 0;
                 i < transformCount;
                 i++)
            {
                PvZReanimTransform transform =
                    ReadTransform(
                        reader
                    );

                if (transform == null)
                {
                    return null;
                }

                track.transforms.Add(
                    transform
                );
            }

            return track;
        }

        // =========================================================
        // TRANSFORM
        // =========================================================

        private static PvZReanimTransform ReadTransform(
            BinaryReader reader)
        {
            PvZReanimTransform transform =
                new PvZReanimTransform();

            /*
             * ResoddedFramework:
             *
             * x
             * y
             * kx
             * ky
             * sx
             * sy
             * f
             * a
             * i
             * font
             * text
             */

            transform.x =
                reader.ReadSingle();

            transform.y =
                reader.ReadSingle();

            transform.skewX =
                reader.ReadSingle();

            transform.skewY =
                reader.ReadSingle();

            transform.scaleX =
                reader.ReadSingle();

            transform.scaleY =
                reader.ReadSingle();

            transform.frame =
                reader.ReadSingle();

            transform.alpha =
                reader.ReadSingle();

            /*
             * DT_IMAGE usa:
             *
             * int length
             * bytes
             */

            string image =
                ReadImageString(
                    reader
                );

            if (!string.IsNullOrEmpty(image))
            {
                transform.imageName =
                    image;
            }

            /*
             * DT_FONT
             *
             * El loader actual de Unity no necesita
             * el font para las plantas, pero debemos
             * consumirlo del stream.
             */

            string font =
                ReadImageString(
                    reader
                );

            /*
             * DT_STRING
             */

            string text =
                ReadString(
                    reader
                );

            if (!string.IsNullOrEmpty(text))
            {
                transform.text =
                    text;
            }

            return transform;
        }

        // =========================================================
        // STRING
        // =========================================================

        private static string ReadString(
            BinaryReader reader)
        {
            uint length =
                reader.ReadUInt32();

            if (length == 0)
            {
                return string.Empty;
            }

            if (length > 1000000)
            {
                throw new InvalidDataException(
                    "String demasiado grande: " +
                    length
                );
            }

            byte[] bytes =
                reader.ReadBytes(
                    checked((int)length)
                );

            if (bytes.Length != length)
            {
                throw new EndOfStreamException(
                    "No se pudieron leer todos " +
                    "los bytes del string."
                );
            }

            return Encoding.UTF8.GetString(
                bytes
            );
        }

        // =========================================================
        // IMAGE / FONT
        // =========================================================

        private static string ReadImageString(
            BinaryReader reader)
        {
            int length =
                reader.ReadInt32();

            if (length < 0 ||
                length > 1000000)
            {
                throw new InvalidDataException(
                    "Longitud de imagen inválida: " +
                    length
                );
            }

            if (length == 0)
            {
                return string.Empty;
            }

            byte[] bytes =
                reader.ReadBytes(
                    length
                );

            if (bytes.Length != length)
            {
                throw new EndOfStreamException(
                    "No se pudieron leer todos " +
                    "los bytes de imagen."
                );
            }

            return Encoding.UTF8.GetString(
                bytes
            );
        }
    }
}