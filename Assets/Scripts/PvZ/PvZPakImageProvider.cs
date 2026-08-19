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

            Texture2D cachedTexture;

            if (textureCache.TryGetValue(
                    normalizedName,
                    out cachedTexture))
            {
                if (cachedTexture != null)
                    return cachedTexture;

                textureCache.Remove(normalizedName);
            }

            string path;

            if (!resolvedPaths.TryGetValue(
                    normalizedName,
                    out path))
            {
                path =
                    ResolveImagePath(
                        imageName
                    );

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
                    path,
                    this
                );

                return null;
            }

            texture.name =
                Path.GetFileNameWithoutExtension(
                    path
                );

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

            Sprite cachedSprite;

            if (spriteCache.TryGetValue(
                    normalizedName,
                    out cachedSprite))
            {
                if (cachedSprite != null)
                    return cachedSprite;

                spriteCache.Remove(normalizedName);
            }

            Texture2D texture =
                LoadTexture(
                    normalizedName
                );

            if (texture == null)
                return null;

            // =====================================================
            // IMPORTANTE
            //
            // Las imágenes originales de PvZ están expresadas
            // esencialmente en píxeles para Reanim.
            //
            // Con 100 PPU:
            //
            // 100 píxeles = 1 unidad Unity
            //
            // Eso hace que cada pieza quede diminuta.
            //
            // Con 1 PPU:
            //
            // 1 píxel = 1 unidad
            //
            // El MeshRenderer usa este PPU para construir el
            // tamaño real del quad.
            // =====================================================

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
                    1f
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
            // RUTA REAL COMPLETA
            // =====================================================

            string originalPath =
                NormalizePakPath(
                    imageName
                );

            if (!string.IsNullOrEmpty(originalPath))
            {
                if (pakReader.Contains(
                        originalPath))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada por ruta directa: " +
                            originalPath,
                            this
                        );
                    }

                    return originalPath;
                }
            }

            // =====================================================
            // NOMBRE REANIM
            //
            // IMAGE_REANIM_PEASHOOTER_BLINK1
            //
            // ->
            //
            // PEASHOOTER_BLINK1
            //
            // ->
            //
            // reanim/PeaShooter_blink1.png
            // =====================================================

            string reanimName =
                ExtractReanimImageName(
                    imageName
                );

            if (!string.IsNullOrEmpty(reanimName))
            {
                if (logSearches)
                {
                    Debug.Log(
                        "[PvZPakImageProvider] " +
                        "Nombre Reanim detectado: " +
                        imageName +
                        " -> " +
                        reanimName,
                        this
                    );
                }

                string reanimPath =
                    FindImageByName(
                        reanimName,
                        true
                    );

                if (!string.IsNullOrEmpty(
                        reanimPath))
                {
                    return reanimPath;
                }
            }

            // =====================================================
            // EXTENSIONES
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

            // =====================================================
            // BUSQUEDA DIRECTA
            // =====================================================

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
            // BUSQUEDA REAL
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
            // NOMBRE EXACTO
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        match
                    );

                if (string.Equals(
                        fileName,
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada por nombre exacto: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // PRIORIDAD TGA
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

                if (!match.EndsWith(
                        ".tga",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            // =====================================================
            // PRIORIDAD REANIM
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

                if (match.StartsWith(
                        "reanim/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada en reanim/: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // =====================================================
            // FALLBACK
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

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

            return null;
        }

        // =========================================================
        // EXTRACT REANIM IMAGE NAME
        // =========================================================

        private static string ExtractReanimImageName(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return string.Empty;

            string value =
                imageName.Trim();

            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[value.Length - 1] == '"')
            {
                value =
                    value.Substring(
                        1,
                        value.Length - 2
                    );
            }

            value =
                value.Replace(
                    '\\',
                    '/'
                );

            while (value.StartsWith(
                       "./",
                       StringComparison.Ordinal))
            {
                value =
                    value.Substring(2);
            }

            value =
                value.TrimStart('/');

            int slash =
                value.LastIndexOf('/');

            if (slash >= 0 &&
                slash + 1 < value.Length)
            {
                value =
                    value.Substring(
                        slash + 1
                    );
            }

            value =
                RemoveImageExtension(
                    value
                );

            const string prefix =
                "IMAGE_REANIM_";

            if (value.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring(
                    prefix.Length
                );
            }

            return string.Empty;
        }

        // =========================================================
        // FIND IMAGE BY NAME
        // =========================================================

        private string FindImageByName(
            string imageName,
            bool preferReanim)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            List<string> matches =
                pakReader.Find(
                    imageName
                );

            if (matches == null ||
                matches.Count == 0)
            {
                return null;
            }

            // =====================================================
            // NOMBRE EXACTO
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        match
                    );

                if (!string.Equals(
                        fileName,
                        imageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (preferReanim &&
                    !match.StartsWith(
                        "reanim/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (logSearches)
                {
                    Debug.Log(
                        "[PvZPakImageProvider] " +
                        "Reanim encontrado por búsqueda: " +
                        match,
                        this
                    );
                }

                return match;
            }

            // =====================================================
            // REANIM CONTIENE EL NOMBRE
            // =====================================================

            if (preferReanim)
            {
                for (int i = 0;
                     i < matches.Count;
                     i++)
                {
                    string match =
                        NormalizePakPath(
                            matches[i]
                        );

                    if (!IsImageFile(match))
                        continue;

                    if (!match.StartsWith(
                            "reanim/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName =
                        Path.GetFileNameWithoutExtension(
                            match
                        );

                    if (fileName.IndexOf(
                            imageName,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (logSearches)
                        {
                            Debug.Log(
                                "[PvZPakImageProvider] " +
                                "Reanim encontrado por coincidencia: " +
                                match,
                                this
                            );
                        }

                        return match;
                    }
                }
            }

            // =====================================================
            // CUALQUIER COINCIDENCIA
            // =====================================================

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                string match =
                    NormalizePakPath(
                        matches[i]
                    );

                if (!IsImageFile(match))
                    continue;

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        match
                    );

                if (fileName.IndexOf(
                        imageName,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (logSearches)
                    {
                        Debug.Log(
                            "[PvZPakImageProvider] " +
                            "Imagen encontrada por coincidencia: " +
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

            if (extension == ".tga")
            {
                return DecodeTGA(
                    data
                );
            }

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
                Destroy(
                    texture
                );

                return null;
            }

            return texture;
        }

        // =========================================================
        // TGA DECODER
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

            if (colorMapType != 0)
            {
                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "TGA con color map no soportado.",
                    this
                );

                return null;
            }

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
                new Color32[
                    pixelCount
                ];

            int offset =
                18 + idLength;

            if (offset > data.Length)
                return null;

            bool topOrigin =
                (descriptor & 0x20) != 0;

            bool rightOrigin =
                (descriptor & 0x10) != 0;

            int currentPixel = 0;

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
            // SIN COMPRESIÓN
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
                while (
                    currentPixel < pixelCount &&
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

            result =
                result.TrimStart('/');

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
        // IS IMAGE
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