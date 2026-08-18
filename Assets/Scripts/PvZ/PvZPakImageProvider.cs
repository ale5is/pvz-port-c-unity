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

        public Texture2D LoadTexture(
            string imageName)
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
            // CACHE TEXTURE
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
                path = ResolveImagePath(
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
            // DECODE
            // -----------------------------------------------------

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );

            texture.name =
                Path.GetFileNameWithoutExtension(
                    path
                );

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            bool decoded =
                ImageConversion.LoadImage(
                    texture,
                    data,
                    false
                );

            if (!decoded)
            {
                Destroy(texture);

                Debug.LogWarning(
                    "[PvZPakImageProvider] " +
                    "El recurso no es una imagen válida:\n" +
                    path,
                    this
                );

                return null;
            }

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

        public Sprite LoadSprite(
            string imageName)
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
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            string normalized =
                NormalizeImageName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return null;

            // -----------------------------------------------------
            // SI YA TENEMOS UNA RUTA DEL PAK
            // -----------------------------------------------------

            string directPath =
                NormalizePakPath(
                    imageName
                );

            if (!string.IsNullOrEmpty(
                    directPath))
            {
                if (pakReader.Contains(
                        directPath))
                {
                    return directPath;
                }

                if (Path.HasExtension(
                        directPath))
                {
                    string withoutExtension =
                        RemoveImageExtension(
                            directPath
                        );

                    if (pakReader.Contains(
                            withoutExtension + ".png"))
                    {
                        return withoutExtension +
                               ".png";
                    }
                }
            }

            // -----------------------------------------------------
            // CANDIDATOS DIRECTOS
            // -----------------------------------------------------

            string[] candidates =
            {
                "reanim/" + normalized + ".png",
                "reanim/" + normalized + ".jpg",
                "reanim/" + normalized + ".jpeg",

                "images/" + normalized + ".png",
                "images/" + normalized + ".jpg",
                "images/" + normalized + ".jpeg",

                normalized + ".png",
                normalized + ".jpg",
                normalized + ".jpeg"
            };

            for (int i = 0;
                 i < candidates.Length;
                 i++)
            {
                string candidate =
                    candidates[i];

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

            // -----------------------------------------------------
            // BUSQUEDA REAL DENTRO DEL PAK
            // -----------------------------------------------------

            List<string> matches =
                pakReader.Find(
                    normalized
                );

            if (matches == null ||
                matches.Count == 0)
            {
                return null;
            }

            // -----------------------------------------------------
            // 1. BUSCAR MISMO NOMBRE + PNG
            // -----------------------------------------------------

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
                            "Imagen encontrada por búsqueda: " +
                            match,
                            this
                        );
                    }

                    return match;
                }
            }

            // -----------------------------------------------------
            // 2. PRIORIDAD REANIM
            // -----------------------------------------------------

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

            // -----------------------------------------------------
            // 3. CUALQUIER IMAGEN COINCIDENTE
            // -----------------------------------------------------

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
        // NORMALIZE IMAGE NAME
        // =========================================================

        private static string NormalizeImageName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

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
                result.TrimStart(
                    '/'
                );

            // -----------------------------------------------------
            // SI ES UNA RUTA, USAMOS EL NOMBRE DEL ARCHIVO
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
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

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

            return result.TrimStart(
                '/'
            );
        }

        // =========================================================
        // REMOVE EXTENSION
        // =========================================================

        private static string RemoveImageExtension(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return value;
            }

            string[] extensions =
            {
                ".png",
                ".jpg",
                ".jpeg",
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