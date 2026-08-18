using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZPakImageProvider : MonoBehaviour
    {
        public static PvZPakImageProvider Instance { get; private set; }

        [Header("PAK")]
        [SerializeField]
        private string pakFileName = "main.pak";

        [Header("Debug")]
        [SerializeField]
        private bool logLoads = true;

        [SerializeField]
        private bool logSearches = true;

        [SerializeField]
        private bool debugPeashooterFiles = true;

        private PvZPakReader pakReader;

        private readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly Dictionary<string, Texture2D> textureCache =
            new Dictionary<string, Texture2D>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly Dictionary<string, string> resolvedPaths =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            LoadPak();
        }

        // =========================================================
        // LOAD PAK
        // =========================================================

        private void LoadPak()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath,
                "PvZ",
                pakFileName
            );

            pakReader = new PvZPakReader();

            if (!pakReader.Load(path))
            {
                Debug.LogError(
                    "[PvZPakImageProvider] " +
                    "No se pudo cargar el PAK:\n" +
                    path,
                    this
                );

                pakReader = null;
                return;
            }

            Debug.Log(
                "[PvZPakImageProvider] " +
                "PAK listo | Archivos: " +
                pakReader.FileCount,
                this
            );

            // -----------------------------------------------------
            // DEBUG PEASHOOTER
            // -----------------------------------------------------

            if (debugPeashooterFiles)
            {
                Debug.Log(
                    "[PvZPakImageProvider] " +
                    "Buscando archivos PEASHOOTER dentro del PAK...",
                    this
                );

                List<string> peashooterFiles =
                    pakReader.Find("PEASHOOTER");

                if (peashooterFiles == null ||
                    peashooterFiles.Count == 0)
                {
                    Debug.LogWarning(
                        "[PvZPakImageProvider] " +
                        "NO se encontraron archivos que contengan " +
                        "'PEASHOOTER' dentro del PAK.",
                        this
                    );
                }
                else
                {
                    Debug.Log(
                        "[PvZPakImageProvider] " +
                        "Archivos PEASHOOTER encontrados: " +
                        peashooterFiles.Count,
                        this
                    );

                    for (int i = 0;
                         i < peashooterFiles.Count;
                         i++)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "PEASHOOTER PAK: " +
                            peashooterFiles[i],
                            this
                        );
                    }
                }
            }
        }

        // =========================================================
        // READY
        // =========================================================

        public bool IsReady
        {
            get
            {
                return pakReader != null &&
                       pakReader.IsLoaded;
            }
        }

        public PvZPakReader PakReader
        {
            get
            {
                return pakReader;
            }
        }

        // =========================================================
        // LOAD TEXTURE
        // =========================================================

        public Texture2D LoadTexture(string imageName)
        {
            if (!IsReady)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "El PAK todavía no está disponible.",
                    this
                );

                return null;
            }

            string normalizedName =
                NormalizeImageName(imageName);

            if (string.IsNullOrEmpty(normalizedName))
                return null;

            // -----------------------------------------------------
            // CACHE
            // -----------------------------------------------------

            Texture2D cachedTexture;

            if (textureCache.TryGetValue(
                    normalizedName,
                    out cachedTexture))
            {
                if (cachedTexture != null)
                    return cachedTexture;

                textureCache.Remove(normalizedName);
            }

            // -----------------------------------------------------
            // RESOLVE PATH
            // -----------------------------------------------------

            string path;

            if (!resolvedPaths.TryGetValue(
                    normalizedName,
                    out path))
            {
                path = ResolveImagePath(imageName);

                if (!string.IsNullOrEmpty(path))
                {
                    resolvedPaths[
                        normalizedName
                    ] = path;
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                if (logSearches)
                {
                    Debug.LogWarning(
                        "[PvZPakImageProvider] " +
                        "No se encontró imagen en PAK: " +
                        imageName,
                        this
                    );
                }

                return null;
            }

            // -----------------------------------------------------
            // READ FILE
            // -----------------------------------------------------

            byte[] data;

            if (!pakReader.TryGetFile(
                    path,
                    out data))
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "No se pudo leer del PAK:\n" +
                    path,
                    this
                );

                return null;
            }

            if (data == null ||
                data.Length == 0)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "La imagen está vacía:\n" +
                    path,
                    this
                );

                return null;
            }

            // -----------------------------------------------------
            // DECODIFICAR
            // -----------------------------------------------------

            Texture2D texture =
                DecodeImage(
                    path,
                    data
                );

            if (texture == null)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "No se pudo decodificar la imagen:\n" +
                    path +
                    " | Bytes: " +
                    data.Length,
                    this
                );

                return null;
            }

            texture.name =
                Path.GetFileNameWithoutExtension(
                    path
                );

            if (string.IsNullOrEmpty(texture.name))
            {
                texture.name =
                    normalizedName;
            }

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            textureCache[
                normalizedName
            ] = texture;

            if (logLoads)
            {
                Debug.Log(
                    "[PvZPakImageProvider] " +
                    "Imagen cargada desde PAK: " +
                    path +
                    " | " +
                    texture.width +
                    "x" +
                    texture.height,
                    this
                );
            }

            return texture;
        }

        // =========================================================
        // LOAD SPRITE
        // =========================================================

        public Sprite LoadSprite(string imageName)
        {
            if (!IsReady)
                return null;

            string normalizedName =
                NormalizeImageName(imageName);

            if (string.IsNullOrEmpty(normalizedName))
                return null;

            // -----------------------------------------------------
            // CACHE SPRITE
            // -----------------------------------------------------

            Sprite cachedSprite;

            if (spriteCache.TryGetValue(
                    normalizedName,
                    out cachedSprite))
            {
                if (cachedSprite != null)
                    return cachedSprite;

                spriteCache.Remove(normalizedName);
            }

            // -----------------------------------------------------
            // TEXTURE
            // -----------------------------------------------------

            Texture2D texture =
                LoadTexture(
                    normalizedName
                );

            if (texture == null)
                return null;

            // -----------------------------------------------------
            // SPRITE
            // -----------------------------------------------------

            Sprite sprite =
                Sprite.Create(
                    texture,
                    new Rect(
                        0f,
                        0f,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    100f
                );

            sprite.name =
                normalizedName;

            spriteCache[
                normalizedName
            ] = sprite;

            return sprite;
        }

        // =========================================================
        // RESOLVE IMAGE PATH
        // =========================================================

        private string ResolveImagePath(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return null;

            string normalized =
                NormalizeImageName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return null;

            // =====================================================
            // 1. RUTA DIRECTA ORIGINAL
            // =====================================================

            string directPath =
                NormalizePakPath(
                    imageName
                );

            if (!string.IsNullOrEmpty(directPath))
            {
                if (pakReader.Contains(
                        directPath))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada por ruta directa: " +
                            directPath,
                            this
                        );
                    }

                    return directPath;
                }
            }

            // =====================================================
            // 2. CANDIDATOS CON EXTENSION
            // =====================================================

            string[] extensions =
            {
                ".tga",
                ".png",
                ".jpg",
                ".jpeg",
                ".gif",
                ".webp"
            };

            string[] folders =
            {
                "reanim/",
                "images/",
                ""
            };

            for (int f = 0;
                 f < folders.Length;
                 f++)
            {
                for (int e = 0;
                     e < extensions.Length;
                     e++)
                {
                    string candidate =
                        folders[f] +
                        normalized +
                        extensions[e];

                    if (pakReader.Contains(
                            candidate))
                    {
                        if (logSearches)
                        {
                            Debug.Log(
                                "[PvZPakImageProvider] " +
                                "Imagen encontrada directamente: " +
                                candidate,
                                this
                            );
                        }

                        return candidate;
                    }
                }
            }

            // =====================================================
            // 3. BUSQUEDA REAL
            //
            // IMPORTANTE:
            // El PAK de PvZ puede tener archivos de imagen SIN
            // extension.
            // =====================================================

            List<string> matches =
                pakReader.Find(
                    normalized
                );

            if (matches == null ||
                matches.Count == 0)
            {
                if (logSearches)
                {
                    Debug.LogWarning(
                        "[PvZPakImageProvider] " +
                        "Find() no encontró coincidencias para: " +
                        normalized,
                        this
                    );
                }

                return null;
            }

            // =====================================================
            // 4. NOMBRE EXACTO
            //
            // Acepta:
            // IMAGE_REANIM_PEASHOOTER_BLINK1
            // IMAGE_REANIM_PEASHOOTER_BLINK1.tga
            // IMAGE_REANIM_PEASHOOTER_BLINK1.png
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (string.IsNullOrEmpty(match))
                    continue;

                string fileName =
                    Path.GetFileName(
                        match
                    );

                string fileNameWithoutExtension =
                    Path.GetFileNameWithoutExtension(
                        match
                    );

                bool exactName =
                    string.Equals(
                        fileName,
                        normalized,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool exactWithoutExtension =
                    string.Equals(
                        fileNameWithoutExtension,
                        normalized,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!exactName &&
                    !exactWithoutExtension)
                {
                    continue;
                }

                // -------------------------------------------------
                // COMPROBAR QUE REALMENTE SEA UNA IMAGEN
                // -------------------------------------------------

                byte[] data;

                if (pakReader.TryGetFile(
                        match,
                        out data))
                {
                    if (IsImageData(data))
                    {
                        if (logSearches)
                        {
                            Debug.Log(
                                "[PvZPakImageProvider] " +
                                "Imagen encontrada por nombre exacto: " +
                                match +
                                " | Bytes: " +
                                data.Length,
                                this
                            );
                        }

                        return match;
                    }

                    // -------------------------------------------------
                    // Si tiene nombre exacto pero no pudimos reconocer
                    // el formato, igualmente lo devolvemos.
                    // DecodeImage intentará leerlo.
                    // -------------------------------------------------

                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Archivo con nombre exacto encontrado " +
                            "pero formato no identificado: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // 5. TGA CON EXTENSION
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (match.EndsWith(
                        ".tga",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen TGA encontrada: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // 6. ARCHIVO DE IMAGEN SIN EXTENSION
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (string.IsNullOrEmpty(match))
                    continue;

                // -------------------------------------------------
                // Si tiene extension conocida ya fue revisado.
                // -------------------------------------------------

                if (Path.HasExtension(match))
                    continue;

                byte[] data;

                if (!pakReader.TryGetFile(
                        match,
                        out data))
                {
                    continue;
                }

                if (IsImageData(data))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen SIN EXTENSION encontrada: " +
                            match +
                            " | Bytes: " +
                            data.Length,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // 7. CUALQUIER ARCHIVO DE IMAGEN
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (IsImageFile(match))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada como fallback: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // 8. FALLBACK: REVISAR CUALQUIER MATCH
            // POR CONTENIDO
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (string.IsNullOrEmpty(match))
                    continue;

                byte[] data;

                if (!pakReader.TryGetFile(
                        match,
                        out data))
                {
                    continue;
                }

                if (IsImageData(data))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada por contenido: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            return null;
        }

        // =========================================================
        // IMAGE DATA DETECTION
        // =========================================================

        private static bool IsImageData(
            byte[] data)
        {
            if (data == null ||
                data.Length < 4)
            {
                return false;
            }

            // -----------------------------------------------------
            // PNG
            // 89 50 4E 47 0D 0A 1A 0A
            // -----------------------------------------------------

            if (data.Length >= 8 &&
                data[0] == 0x89 &&
                data[1] == 0x50 &&
                data[2] == 0x4E &&
                data[3] == 0x47 &&
                data[4] == 0x0D &&
                data[5] == 0x0A &&
                data[6] == 0x1A &&
                data[7] == 0x0A)
            {
                return true;
            }

            // -----------------------------------------------------
            // JPG
            // FF D8 FF
            // -----------------------------------------------------

            if (data.Length >= 3 &&
                data[0] == 0xFF &&
                data[1] == 0xD8 &&
                data[2] == 0xFF)
            {
                return true;
            }

            // -----------------------------------------------------
            // TGA
            //
            // TGA no tiene una firma fija como PNG/JPG.
            // Revisamos el header:
            //
            // byte 1 = color map
            // byte 2 = image type
            // bytes 12-15 = width/height
            // byte 16 = bpp
            // -----------------------------------------------------

            if (data.Length >= 18)
            {
                int colorMapType =
                    data[1];

                int imageType =
                    data[2];

                int width =
                    data[12] |
                    (data[13] << 8);

                int height =
                    data[14] |
                    (data[15] << 8);

                int bitsPerPixel =
                    data[16];

                bool validColorMap =
                    colorMapType == 0;

                bool validImageType =
                    imageType == 2 ||
                    imageType == 10;

                bool validBpp =
                    bitsPerPixel == 24 ||
                    bitsPerPixel == 32;

                bool validSize =
                    width > 0 &&
                    height > 0;

                if (validColorMap &&
                    validImageType &&
                    validBpp &&
                    validSize)
                {
                    return true;
                }
            }

            return false;
        }

        // =========================================================
        // DECODE IMAGE
        // =========================================================

        private Texture2D DecodeImage(
            string path,
            byte[] data)
        {
            string extension =
                Path.GetExtension(
                    path
                ).ToLowerInvariant();

            // -----------------------------------------------------
            // TGA POR EXTENSION
            // -----------------------------------------------------

            if (extension == ".tga")
            {
                return DecodeTGA(data);
            }

            // -----------------------------------------------------
            // TGA SIN EXTENSION
            // -----------------------------------------------------

            if (IsTGAData(data))
            {
                return DecodeTGA(data);
            }

            // -----------------------------------------------------
            // PNG / JPG / OTROS FORMATOS UNITY
            // -----------------------------------------------------

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );

            bool decoded =
                ImageConversion.LoadImage(
                    texture,
                    data,
                    false
                );

            if (!decoded)
            {
                Destroy(texture);
                return null;
            }

            return texture;
        }

        // =========================================================
        // IS TGA DATA
        // =========================================================

        private static bool IsTGAData(
            byte[] data)
        {
            if (data == null ||
                data.Length < 18)
            {
                return false;
            }

            int colorMapType =
                data[1];

            int imageType =
                data[2];

            int width =
                data[12] |
                (data[13] << 8);

            int height =
                data[14] |
                (data[15] << 8);

            int bitsPerPixel =
                data[16];

            if (colorMapType != 0)
                return false;

            if (imageType != 2 &&
                imageType != 10)
            {
                return false;
            }

            if (bitsPerPixel != 24 &&
                bitsPerPixel != 32)
            {
                return false;
            }

            if (width <= 0 ||
                height <= 0)
            {
                return false;
            }

            return true;
        }

        // =========================================================
        // TGA DECODER
        //
        // Soporta:
        // - TGA sin compresión
        // - TGA RLE
        // - 24 bits
        // - 32 bits
        // =========================================================

        private Texture2D DecodeTGA(
            byte[] data)
        {
            if (data == null ||
                data.Length < 18)
            {
                return null;
            }

            int idLength =
                data[0];

            int colorMapType =
                data[1];

            int imageType =
                data[2];

            int width =
                data[12] |
                (data[13] << 8);

            int height =
                data[14] |
                (data[15] << 8);

            int bitsPerPixel =
                data[16];

            byte descriptor =
                data[17];

            // -----------------------------------------------------
            // COLOR MAP
            // -----------------------------------------------------

            if (colorMapType != 0)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "TGA con color map no soportado.",
                    this
                );

                return null;
            }

            // -----------------------------------------------------
            // TIPOS
            // -----------------------------------------------------

            if (imageType != 2 &&
                imageType != 10)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "Tipo TGA no soportado: " +
                    imageType,
                    this
                );

                return null;
            }

            // -----------------------------------------------------
            // BITS
            // -----------------------------------------------------

            if (bitsPerPixel != 24 &&
                bitsPerPixel != 32)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "Profundidad TGA no soportada: " +
                    bitsPerPixel,
                    this
                );

                return null;
            }

            if (width <= 0 ||
                height <= 0)
            {
                return null;
            }

            int bytesPerPixel =
                bitsPerPixel / 8;

            int pixelCount =
                width * height;

            Color32[] pixels =
                new Color32[pixelCount];

            int offset =
                18 + idLength;

            if (offset > data.Length)
                return null;

            // -----------------------------------------------------
            // ORIENTACION
            //
            // bit 5 = vertical
            // bit 4 = horizontal
            // -----------------------------------------------------

            bool topOrigin =
                (descriptor & 0x20) != 0;

            bool rightOrigin =
                (descriptor & 0x10) != 0;

            int currentPixel = 0;

            // -----------------------------------------------------
            // READ PIXEL
            // -----------------------------------------------------

            Func<Color32> ReadPixel =
                delegate
                {
                    if (offset + bytesPerPixel >
                        data.Length)
                    {
                        return new Color32(
                            255,
                            0,
                            255,
                            255
                        );
                    }

                    byte b =
                        data[offset++];

                    byte g =
                        data[offset++];

                    byte r =
                        data[offset++];

                    byte a = 255;

                    if (bytesPerPixel == 4)
                    {
                        a =
                            data[offset++];
                    }

                    return new Color32(
                        r,
                        g,
                        b,
                        a
                    );
                };

            // =====================================================
            // SIN COMPRESION
            // =====================================================

            if (imageType == 2)
            {
                for (int sourceIndex = 0;
                     sourceIndex < pixelCount;
                     sourceIndex++)
                {
                    Color32 color =
                        ReadPixel();

                    int sourceX =
                        sourceIndex % width;

                    int sourceY =
                        sourceIndex / width;

                    int x =
                        rightOrigin
                            ? width - 1 - sourceX
                            : sourceX;

                    int y =
                        topOrigin
                            ? height - 1 - sourceY
                            : sourceY;

                    int destinationIndex =
                        y * width + x;

                    if (destinationIndex >= 0 &&
                        destinationIndex < pixels.Length)
                    {
                        pixels[
                            destinationIndex
                        ] = color;
                    }
                }
            }

            // =====================================================
            // RLE
            // =====================================================

            else
            {
                while (currentPixel < pixelCount &&
                       offset < data.Length)
                {
                    byte packet =
                        data[offset++];

                    bool runLength =
                        (packet & 0x80) != 0;

                    int count =
                        (packet & 0x7F) + 1;

                    if (runLength)
                    {
                        Color32 color =
                            ReadPixel();

                        for (int i = 0;
                             i < count &&
                             currentPixel < pixelCount;
                             i++)
                        {
                            WriteTGAPixel(
                                pixels,
                                currentPixel,
                                width,
                                height,
                                color,
                                topOrigin,
                                rightOrigin
                            );

                            currentPixel++;
                        }
                    }
                    else
                    {
                        for (int i = 0;
                             i < count &&
                             currentPixel < pixelCount;
                             i++)
                        {
                            Color32 color =
                                ReadPixel();

                            WriteTGAPixel(
                                pixels,
                                currentPixel,
                                width,
                                height,
                                color,
                                topOrigin,
                                rightOrigin
                            );

                            currentPixel++;
                        }
                    }
                }
            }

            Texture2D texture =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false
                );

            texture.SetPixels32(
                pixels
            );

            texture.Apply(
                false,
                false
            );

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            return texture;
        }

        // =========================================================
        // WRITE TGA PIXEL
        // =========================================================

        private static void WriteTGAPixel(
            Color32[] pixels,
            int sourceIndex,
            int width,
            int height,
            Color32 color,
            bool topOrigin,
            bool rightOrigin)
        {
            int sourceX =
                sourceIndex % width;

            int sourceY =
                sourceIndex / width;

            int x =
                rightOrigin
                    ? width - 1 - sourceX
                    : sourceX;

            int y =
                topOrigin
                    ? height - 1 - sourceY
                    : sourceY;

            int destinationIndex =
                y * width + x;

            if (destinationIndex >= 0 &&
                destinationIndex < pixels.Length)
            {
                pixels[
                    destinationIndex
                ] = color;
            }
        }

        // =========================================================
        // NORMALIZE IMAGE NAME
        // =========================================================

        private static string NormalizeImageName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result =
                value.Trim();

            // -----------------------------------------------------
            // QUITAR COMILLAS
            // -----------------------------------------------------

            if (result.Length >= 2 &&
                result[0] == '"' &&
                result[result.Length - 1] == '"')
            {
                result =
                    result.Substring(
                        1,
                        result.Length - 2
                    );
            }

            // -----------------------------------------------------
            // NORMALIZAR SLASH
            // -----------------------------------------------------

            result =
                result.Replace(
                    '\\',
                    '/'
                );

            // -----------------------------------------------------
            // QUITAR "./"
            // -----------------------------------------------------

            while (result.StartsWith(
                       "./",
                       StringComparison.Ordinal))
            {
                result =
                    result.Substring(2);
            }

            result =
                result.TrimStart('/');

            // -----------------------------------------------------
            // SI ES RUTA, QUEDARSE CON EL NOMBRE
            // -----------------------------------------------------

            int slash =
                result.LastIndexOf('/');

            if (slash >= 0 &&
                slash + 1 < result.Length)
            {
                result =
                    result.Substring(
                        slash + 1
                    );
            }

            // -----------------------------------------------------
            // QUITAR EXTENSION CONOCIDA
            // -----------------------------------------------------

            result =
                RemoveImageExtension(
                    result
                );

            return result.Trim();
        }

        // =========================================================
        // NORMALIZE PAK PATH
        // =========================================================

        private static string NormalizePakPath(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result =
                value.Trim();

            result =
                result.Replace(
                    '\\',
                    '/'
                );

            while (result.StartsWith(
                       "./",
                       StringComparison.Ordinal))
            {
                result =
                    result.Substring(2);
            }

            return result.TrimStart('/');
        }

        // =========================================================
        // REMOVE EXTENSION
        // =========================================================

        private static string RemoveImageExtension(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string[] extensions =
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".tga",
                ".gif",
                ".webp"
            };

            for (int i = 0;
                 i < extensions.Length;
                 i++)
            {
                string extension =
                    extensions[i];

                if (value.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring(
                        0,
                        value.Length -
                        extension.Length
                    );
                }
            }

            return value;
        }

        // =========================================================
        // IS IMAGE FILE
        // =========================================================

        private static bool IsImageFile(
            string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return
                path.EndsWith(
                    ".png",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                path.EndsWith(
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                path.EndsWith(
                    ".jpeg",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                path.EndsWith(
                    ".tga",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                path.EndsWith(
                    ".gif",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                path.EndsWith(
                    ".webp",
                    StringComparison.OrdinalIgnoreCase
                );
        }

        // =========================================================
        // CLEAR CACHE
        // =========================================================

        public void ClearCache()
        {
            spriteCache.Clear();

            resolvedPaths.Clear();

            foreach (
                KeyValuePair<
                    string,
                    Texture2D
                > pair
                in textureCache)
            {
                if (pair.Value != null)
                {
                    Destroy(
                        pair.Value
                    );
                }
            }

            textureCache.Clear();
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            foreach (
                KeyValuePair<
                    string,
                    Sprite
                > pair
                in spriteCache)
            {
                if (pair.Value != null)
                {
                    Destroy(
                        pair.Value
                    );
                }
            }

            spriteCache.Clear();

            foreach (
                KeyValuePair<
                    string,
                    Texture2D
                > pair
                in textureCache)
            {
                if (pair.Value != null)
                {
                    Destroy(
                        pair.Value
                    );
                }
            }

            textureCache.Clear();

            resolvedPaths.Clear();
        }
    }
}