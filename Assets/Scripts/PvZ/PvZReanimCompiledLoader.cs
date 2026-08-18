using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimCompiledLoader
    {
        // =========================================================
        // FORMATO ORIGINAL DE PVZ / RESODDED
        // =========================================================

        private const uint COMPILED_LEGACY_DEFINITION_MAGIC =
            0xDEADFED4;

        /*
         * En Resodded:
         *
         * struct CompressedDefinitionHeader
         * {
         *     unsigned int  mCookie;
         *     unsigned long mUncompressedSize;
         * };
         *
         * En el build x86 original son 8 bytes.
         */

        private const int HEADER_SIZE = 8;

        /*
         * Tamaños de las estructuras nativas x86
         * usadas por ReanimatorDefinition.
         *
         * ReanimatorDefinition:
         *
         * ReanimatorTrack* mTracks;      4
         * int              mTrackCount;  4
         * float            mFPS;         4
         * ReanimAtlas*     mReanimAtlas; 4
         *
         * TOTAL = 16 bytes
         */

        private const int NATIVE_DEFINITION_SIZE = 16;

        /*
         * ReanimatorTrack:
         *
         * const char*          mName;            4
         * ReanimatorTransform* mTransforms;      4
         * int                  mTransformCount;  4
         *
         * TOTAL = 12 bytes
         */

        private const int NATIVE_TRACK_SIZE = 12;

        /*
         * ReanimatorTransform:
         *
         * float x       4
         * float y       4
         * float kx      4
         * float ky      4
         * float sx      4
         * float sy      4
         * float f       4
         * float a       4
         * Image*        4
         * Font*         4
         * const char*   4
         *
         * TOTAL = 44 bytes
         */

        private const int NATIVE_TRANSFORM_SIZE = 44;

        // =========================================================
        // PUBLIC
        // =========================================================

        public static PvZReanimDefinition LoadBytes(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Datos vacíos."
                );

                return null;
            }

            try
            {
                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "Analizando compiled | Bytes: " +
                    data.Length
                );

                byte[] uncompressed =
                    DecompressCompiled(
                        data
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
                    ParseOriginalCache(
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

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "Definición reconstruida | " +
                    "Tracks: " +
                    definition.TrackCount +
                    " | Frames: " +
                    definition.GetMaxFrameCount() +
                    " | FPS: " +
                    definition.fps
                );

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
        // DESCOMPRESIÓN DEL COMPILED ORIGINAL
        // =========================================================

        private static byte[] DecompressCompiled(
            byte[] data)
        {
            if (data.Length < HEADER_SIZE)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Compiled demasiado pequeño."
                );

                return null;
            }

            uint cookie =
                BitConverter.ToUInt32(
                    data,
                    0
                );

            uint uncompressedSize =
                BitConverter.ToUInt32(
                    data,
                    4
                );

            Debug.Log(
                "[PvZReanimCompiledLoader] " +
                "Cookie: 0x" +
                cookie.ToString("X8") +
                " | UncompressedSize: " +
                uncompressedSize
            );

            if (cookie !=
                COMPILED_LEGACY_DEFINITION_MAGIC)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Magic inválido: 0x" +
                    cookie.ToString("X8") +
                    " | Esperado: 0x" +
                    COMPILED_LEGACY_DEFINITION_MAGIC.ToString("X8")
                );

                return null;
            }

            if (uncompressedSize == 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Tamaño descomprimido inválido."
                );

                return null;
            }

            int compressedOffset =
                HEADER_SIZE;

            int compressedSize =
                data.Length -
                compressedOffset;

            if (compressedSize <= 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "No existe bloque comprimido."
                );

                return null;
            }

            byte[] compressed =
                new byte[
                    compressedSize
                ];

            Buffer.BlockCopy(
                data,
                compressedOffset,
                compressed,
                0,
                compressedSize
            );

            /*
             * Resodded utiliza:
             *
             * zlib::uncompress()
             *
             * Por lo tanto el bloque contiene
             * zlib completo.
             *
             * .NET DeflateStream necesita solamente
             * el stream DEFLATE.
             */

            if (compressed.Length < 6)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "Bloque zlib demasiado pequeño."
                );

                return null;
            }

            int deflateOffset = 2;

            int deflateSize =
                compressed.Length -
                6;

            if (deflateSize <= 0)
            {
                Debug.LogError(
                    "[PvZReanimCompiledLoader] " +
                    "DEFLATE vacío."
                );

                return null;
            }

            byte[] deflateData =
                new byte[
                    deflateSize
                ];

            Buffer.BlockCopy(
                compressed,
                deflateOffset,
                deflateData,
                0,
                deflateSize
            );

            using (
                MemoryStream input =
                    new MemoryStream(
                        deflateData
                    ))
            using (
                DeflateStream deflate =
                    new DeflateStream(
                        input,
                        CompressionMode.Decompress
                    ))
            using (
                MemoryStream output =
                    new MemoryStream(
                        checked(
                            (int)uncompressedSize
                        )
                    )
            )
            {
                deflate.CopyTo(
                    output
                );

                byte[] result =
                    output.ToArray();

                if (result.Length !=
                    uncompressedSize)
                {
                    Debug.LogWarning(
                        "[PvZReanimCompiledLoader] " +
                        "Tamaño descomprimido diferente | " +
                        "Esperado: " +
                        uncompressedSize +
                        " | Real: " +
                        result.Length
                    );
                }

                return result;
            }
        }

        // =========================================================
        // CACHE ORIGINAL
        // =========================================================

        private static PvZReanimDefinition ParseOriginalCache(
            byte[] data)
        {
            if (data == null ||
                data.Length < 4)
            {
                return null;
            }

            using (
                MemoryStream stream =
                    new MemoryStream(data))
            using (
                BinaryReader reader =
                    new BinaryReader(
                        stream,
                        Encoding.UTF8,
                        true
                    )
            )
            {
                /*
                 * Resodded:
                 *
                 * Primero:
                 *
                 * uint32 schemaHash
                 *
                 * Después:
                 *
                 * ReanimatorDefinition
                 */

                uint schemaHash =
                    reader.ReadUInt32();

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "SchemaHash: 0x" +
                    schemaHash.ToString("X8")
                );

                if (reader.BaseStream.Length -
                    reader.BaseStream.Position <
                    NATIVE_DEFINITION_SIZE)
                {
                    Debug.LogError(
                        "[PvZReanimCompiledLoader] " +
                        "No hay suficientes bytes para " +
                        "ReanimatorDefinition."
                    );

                    return null;
                }

                /*
                 * Leemos la estructura nativa.
                 *
                 * x86:
                 *
                 * offset 0 = mTracks
                 * offset 4 = mTrackCount
                 * offset 8 = mFPS
                 * offset 12 = mReanimAtlas
                 */

                reader.ReadUInt32();

                int trackCount =
                    reader.ReadInt32();

                float fps =
                    reader.ReadSingle();

                reader.ReadUInt32();

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "Native Definition | " +
                    "Tracks: " +
                    trackCount +
                    " | FPS: " +
                    fps
                );

                if (trackCount < 0 ||
                    trackCount > 10000)
                {
                    Debug.LogError(
                        "[PvZReanimCompiledLoader] " +
                        "TrackCount inválido: " +
                        trackCount
                    );

                    return null;
                }

                PvZReanimDefinition definition =
                    ScriptableObject.CreateInstance<
                        PvZReanimDefinition
                    >();

                definition.fps =
                    fps > 0f
                        ? fps
                        : PvZReanimConstants.DefaultFPS;

                definition.tracks.Clear();

                /*
                 * -------------------------------------------------
                 * DefReadFromCacheArray
                 * -------------------------------------------------
                 *
                 * Después de la estructura viene:
                 *
                 * int aDefSize
                 *
                 * Debe ser sizeof(ReanimatorTrack)
                 * = 12 en x86.
                 */

                int trackDefinitionSize =
                    reader.ReadInt32();

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "TrackDefSize: " +
                    trackDefinitionSize
                );

                if (trackDefinitionSize !=
                    NATIVE_TRACK_SIZE)
                {
                    Debug.LogError(
                        "[PvZReanimCompiledLoader] " +
                        "Tamaño de ReanimatorTrack " +
                        "inesperado: " +
                        trackDefinitionSize +
                        " | Esperado: " +
                        NATIVE_TRACK_SIZE
                    );

                    return null;
                }

                /*
                 * -------------------------------------------------
                 * RAW TRACK ARRAY
                 * -------------------------------------------------
                 *
                 * Primero vienen todos los structs.
                 *
                 * Después se reparan sus punteros leyendo
                 * los campos definidos por DefMap.
                 */

                NativeTrack[] nativeTracks =
                    new NativeTrack[
                        trackCount
                    ];

                for (int i = 0;
                     i < trackCount;
                     i++)
                {
                    nativeTracks[i] =
                        ReadNativeTrack(
                            reader
                        );
                }

                /*
                 * -------------------------------------------------
                 * RECONSTRUIR TRACKS
                 * -------------------------------------------------
                 */

                for (int i = 0;
                     i < trackCount;
                     i++)
                {
                    PvZReanimTrack track =
                        new PvZReanimTrack(
                            string.Empty
                        );

                    /*
                     * DefMap:
                     *
                     * name -> DT_STRING
                     * t    -> DT_ARRAY
                     */

                    track.name =
                        ReadString(
                            reader
                        );

                    if (string.IsNullOrEmpty(
                            track.name))
                    {
                        track.name =
                            "track_" + i;
                    }

                    int transformCount =
                        nativeTracks[i]
                            .transformCount;

                    /*
                     * DefReadFromCacheArray:
                     *
                     * int aDefSize
                     */

                    int transformDefinitionSize =
                        reader.ReadInt32();

                    Debug.Log(
                        "[PvZReanimCompiledLoader] " +
                        "Track [" +
                        i +
                        "] " +
                        track.name +
                        " | TransformCount: " +
                        transformCount +
                        " | TransformDefSize: " +
                        transformDefinitionSize
                    );

                    if (transformDefinitionSize !=
                        NATIVE_TRANSFORM_SIZE)
                    {
                        Debug.LogError(
                            "[PvZReanimCompiledLoader] " +
                            "Tamaño de ReanimatorTransform " +
                            "inesperado: " +
                            transformDefinitionSize +
                            " | Esperado: " +
                            NATIVE_TRANSFORM_SIZE
                        );

                        return null;
                    }

                    /*
                     * RAW TRANSFORMS
                     */

                    NativeTransform[] nativeTransforms =
                        new NativeTransform[
                            transformCount
                        ];

                    for (int frame = 0;
                         frame < transformCount;
                         frame++)
                    {
                        nativeTransforms[frame] =
                            ReadNativeTransform(
                                reader
                            );
                    }

                    /*
                     * Campos reparados del transform.
                     *
                     * DefMap:
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

                    for (int frame = 0;
                         frame < transformCount;
                         frame++)
                    {
                        PvZReanimTransform transform =
                            new PvZReanimTransform();

                        transform.x =
                            nativeTransforms[frame].x;

                        transform.y =
                            nativeTransforms[frame].y;

                        transform.skewX =
                            nativeTransforms[frame].kx;

                        transform.skewY =
                            nativeTransforms[frame].ky;

                        transform.scaleX =
                            nativeTransforms[frame].sx;

                        transform.scaleY =
                            nativeTransforms[frame].sy;

                        transform.frame =
                            nativeTransforms[frame].f;

                        transform.alpha =
                            nativeTransforms[frame].a;

                        /*
                         * DT_IMAGE
                         */

                        string imageName =
                            ReadImageString(
                                reader
                            );

                        if (!string.IsNullOrEmpty(
                                imageName))
                        {
                            transform.imageName =
                                NormalizeImageName(
                                    imageName
                                );
                        }

                        /*
                         * DT_FONT
                         *
                         * No necesitamos cargar fonts todavía,
                         * pero debemos consumirlos del stream.
                         */

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

                        if (!string.IsNullOrEmpty(
                                text))
                        {
                            transform.text =
                                text;
                        }

                        track.transforms.Add(
                            transform
                        );
                    }

                    definition.tracks.Add(
                        track
                    );

                    Debug.Log(
                        "[PvZReanimCompiledLoader] " +
                        "Track reconstruido: " +
                        track.name +
                        " | Frames: " +
                        track.TransformCount
                    );
                }

                Debug.Log(
                    "[PvZReanimCompiledLoader] " +
                    "CACHE COMPLETO | " +
                    "Tracks: " +
                    definition.TrackCount +
                    " | Frames: " +
                    definition.GetMaxFrameCount() +
                    " | FPS: " +
                    definition.fps
                );

                return definition;
            }
        }

        // =========================================================
        // NATIVE TRACK
        // =========================================================

        private struct NativeTrack
        {
            public uint namePointer;
            public uint transformsPointer;
            public int transformCount;
        }

        private static NativeTrack ReadNativeTrack(
            BinaryReader reader)
        {
            NativeTrack track =
                new NativeTrack();

            track.namePointer =
                reader.ReadUInt32();

            track.transformsPointer =
                reader.ReadUInt32();

            track.transformCount =
                reader.ReadInt32();

            if (track.transformCount < 0 ||
                track.transformCount > 100000)
            {
                throw new InvalidDataException(
                    "TransformCount inválido: " +
                    track.transformCount
                );
            }

            return track;
        }

        // =========================================================
        // NATIVE TRANSFORM
        // =========================================================

        private struct NativeTransform
        {
            public float x;
            public float y;
            public float kx;
            public float ky;
            public float sx;
            public float sy;
            public float f;
            public float a;

            public uint imagePointer;
            public uint fontPointer;
            public uint textPointer;
        }

        private static NativeTransform ReadNativeTransform(
            BinaryReader reader)
        {
            NativeTransform transform =
                new NativeTransform();

            transform.x =
                reader.ReadSingle();

            transform.y =
                reader.ReadSingle();

            transform.kx =
                reader.ReadSingle();

            transform.ky =
                reader.ReadSingle();

            transform.sx =
                reader.ReadSingle();

            transform.sy =
                reader.ReadSingle();

            transform.f =
                reader.ReadSingle();

            transform.a =
                reader.ReadSingle();

            transform.imagePointer =
                reader.ReadUInt32();

            transform.fontPointer =
                reader.ReadUInt32();

            transform.textPointer =
                reader.ReadUInt32();

            return transform;
        }

        // =========================================================
        // STRING ORIGINAL
        // =========================================================

        private static string ReadString(
            BinaryReader reader)
        {
            int length =
                reader.ReadInt32();

            if (length < 0 ||
                length > 100000)
            {
                throw new InvalidDataException(
                    "Longitud de string inválida: " +
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
                    "los bytes del string."
                );
            }

            return Encoding.UTF8.GetString(
                bytes
            );
        }

        // =========================================================
        // IMAGE / FONT ORIGINAL
        // =========================================================

        private static string ReadImageString(
            BinaryReader reader)
        {
            int length =
                reader.ReadInt32();

            if (length < 0 ||
                length > 100000)
            {
                throw new InvalidDataException(
                    "Longitud de imagen/font inválida: " +
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
                    "los bytes de imagen/font."
                );
            }

            return Encoding.UTF8.GetString(
                bytes
            );
        }

        // =========================================================
        // NORMALIZAR IMAGEN
        // =========================================================

        private static string NormalizeImageName(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            imageName =
                imageName.Trim();

            imageName =
                imageName.Replace(
                    '\\',
                    '/'
                );

            /*
             * Resodded resuelve imágenes usando:
             *
             * IMAGE_REANIM_
             * +
             * reanim/
             *
             * por eso dejamos el nombre limpio.
             */

            if (imageName.StartsWith(
                    "IMAGE_REANIM_",
                    StringComparison.OrdinalIgnoreCase))
            {
                imageName =
                    imageName.Substring(
                        "IMAGE_REANIM_".Length
                    );
            }

            if (imageName.StartsWith(
                    "reanim/",
                    StringComparison.OrdinalIgnoreCase))
            {
                imageName =
                    imageName.Substring(
                        "reanim/".Length
                    );
            }

            return imageName;
        }
    }
}