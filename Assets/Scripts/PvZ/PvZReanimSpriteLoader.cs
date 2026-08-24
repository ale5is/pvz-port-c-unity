using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimSpriteLoader : MonoBehaviour
    {
        [Header("Provider")]
        [SerializeField]
        private PvZReanimImageProvider provider;

        [Header("Image Folder")]
        [SerializeField]
        private string imageFolder = "PvZImages";

        [Header("Options")]
        [SerializeField]
        private bool loadOnStart = true;

        [SerializeField]
        private bool recursiveSearch = true;

        [SerializeField]
        private bool pointFilter = true;

        [SerializeField]
        private bool logLoadedSprites = true;

        private readonly Dictionary<string, Sprite> loadedSprites =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase
            );

        public PvZReanimImageProvider Provider => provider;

        public string ImageFolder => imageFolder;

        public int LoadedSpriteCount => loadedSprites.Count;

        private void Awake()
        {
            FindProvider();

            if (loadOnStart)
            {
                LoadAll();
            }
        }

        private void FindProvider()
        {
            if (provider != null)
                return;

            provider =
                GetComponent<PvZReanimImageProvider>();

            if (provider == null)
            {
                provider =
                    GetComponentInParent<
                        PvZReanimImageProvider
                    >();
            }

            if (provider == null)
            {
                provider =
                    FindFirstObjectByType<
                        PvZReanimImageProvider
                    >();
            }
        }

        public void SetProvider(
            PvZReanimImageProvider newProvider)
        {
            provider =
                newProvider;
        }
        public int LoadAll()
        {
            FindProvider();

            if (provider == null)
            {
                Debug.LogError(
                    "PvZReanimSpriteLoader: " +
                    "No se encontró PvZReanimImageProvider.",
                    this
                );

                return 0;
            }

            string folder =
                GetImageFolderPath();

            if (!Directory.Exists(folder))
            {
                Debug.LogWarning(
                    "PvZReanimSpriteLoader: " +
                    "No existe la carpeta de imágenes:\n" +
                    folder,
                    this
                );

                return 0;
            }

            SearchOption searchOption =
                recursiveSearch
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

            string[] files =
                Directory.GetFiles(
                    folder,
                    "*.*",
                    searchOption
                );

            int loaded = 0;

            for (int i = 0;
                 i < files.Length;
                 i++)
            {
                string file =
                    files[i];

                if (!IsSupportedImage(file))
                    continue;

                if (LoadFile(file))
                {
                    loaded++;
                }
            }

            Debug.Log(
                "PvZReanimSpriteLoader: " +
                "Sprites cargados: " +
                loaded,
                this
            );

            return loaded;
        }
        public bool LoadFile(
            string path)
        {
            FindProvider();

            if (provider == null)
            {
                Debug.LogError(
                    "PvZReanimSpriteLoader: " +
                    "No hay Provider.",
                    this
                );

                return false;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PvZReanimSpriteLoader: " +
                    "No existe el archivo:\n" +
                    path,
                    this
                );

                return false;
            }

            byte[] data;

            try
            {
                data =
                    File.ReadAllBytes(
                        path
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimSpriteLoader: " +
                    "No se pudo leer:\n" +
                    path +
                    "\n" +
                    exception.Message,
                    this
                );

                return false;
            }

            if (data == null ||
                data.Length == 0)
            {
                return false;
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
                DestroyObject(
                    texture
                );

                Debug.LogWarning(
                    "PvZReanimSpriteLoader: " +
                    "No se pudo decodificar:\n" +
                    path,
                    this
                );

                return false;
            }

            string fileName =
                Path.GetFileNameWithoutExtension(
                    path
                );

            string relativePath =
                GetRelativeImagePath(
                    path
                );

            if (pointFilter)
            {
                texture.filterMode =
                    FilterMode.Point;
            }

            texture.wrapMode =
                TextureWrapMode.Clamp;

            texture.name =
                fileName;

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
                fileName;

            RegisterSpriteAliases(
                sprite,
                fileName,
                relativePath
            );

            if (logLoadedSprites)
            {
                Debug.Log(
                    "[PvZReanim] Sprite cargado: " +
                    fileName +
                    " | " +
                    texture.width +
                    "x" +
                    texture.height,
                    this
                );
            }

            return true;
        }

        private void RegisterSpriteAliases(
            Sprite sprite,
            string fileName,
            string relativePath)
        {
            if (sprite == null)
                return;

            Register(
                fileName,
                sprite
            );

            if (!string.IsNullOrEmpty(relativePath))
            {
                Register(
                    relativePath,
                    sprite
                );

                string noExtension =
                    RemoveExtension(
                        relativePath
                    );

                Register(
                    noExtension,
                    sprite
                );

                string slashPath =
                    relativePath.Replace(
                        '\\',
                        '/'
                    );

                Register(
                    slashPath,
                    sprite
                );

                Register(
                    RemoveExtension(
                        slashPath
                    ),
                    sprite
                );
            }
        }

        public void Register(
            string imageName,
            Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return;
            }

            if (sprite == null)
                return;

            string normalized =
                NormalizeKey(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return;
            }

            Sprite previous;

            if (loadedSprites.TryGetValue(
                    normalized,
                    out previous))
            {
                if (previous != null &&
                    previous != sprite)
                {
                    return;
                }
            }

            loadedSprites[
                normalized
            ] = sprite;

            provider.RegisterSprite(
                normalized,
                sprite
            );
        }
        public Sprite Get(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            string normalized =
                NormalizeKey(
                    imageName
                );

            Sprite sprite;

            if (loadedSprites.TryGetValue(
                    normalized,
                    out sprite))
            {
                return sprite;
            }

            return null;
        }

        public bool Contains(
            string imageName)
        {
            return Get(
                imageName
            ) != null;
        }

        private string GetImageFolderPath()
        {
            return Path.Combine(
                Application.dataPath,
                imageFolder
            );
        }

        private string GetRelativeImagePath(
            string fullPath)
        {
            string folder =
                GetImageFolderPath();

            string normalizedFolder =
                folder.Replace(
                    '\\',
                    '/'
                );

            string normalizedPath =
                fullPath.Replace(
                    '\\',
                    '/'
                );

            if (!normalizedFolder.EndsWith(
                    "/"))
            {
                normalizedFolder += "/";
            }

            if (normalizedPath.StartsWith(
                    normalizedFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(
                    normalizedFolder.Length
                );
            }

            return Path.GetFileName(
                fullPath
            );
        }

        private string NormalizeKey(
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

            while (result.Contains("//"))
            {
                result =
                    result.Replace(
                        "//",
                        "/"
                    );
            }

            while (result.StartsWith("/"))
            {
                result =
                    result.Substring(1);
            }

            while (result.EndsWith("/"))
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 1
                    );
            }

            result =
                RemoveExtension(
                    result
                );

            return result.Trim();
        }

        private string RemoveExtension(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string extension =
                Path.GetExtension(
                    value
                );

            if (string.IsNullOrEmpty(
                    extension))
            {
                return value;
            }

            return value.Substring(
                0,
                value.Length -
                extension.Length
            );
        }

        private bool IsSupportedImage(
            string path)
        {
            string extension =
                Path.GetExtension(
                    path
                );

            if (string.IsNullOrEmpty(
                    extension))
            {
                return false;
            }

            extension =
                extension.ToLowerInvariant();

            return
                extension == ".png" ||
                extension == ".jpg" ||
                extension == ".jpeg";
        }

        public void Clear()
        {
            foreach (
                KeyValuePair<
                    string,
                    Sprite
                > pair
                in loadedSprites)
            {
                if (pair.Value != null)
                {
                    DestroyObject(
                        pair.Value
                    );
                }
            }

            loadedSprites.Clear();

            if (provider != null)
            {
                provider.ClearRegisteredSprites();
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
        private new void DestroyObject(
            UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(
                    obj
                );
            }
            else
            {
                DestroyImmediate(
                    obj
                );
            }
        }
    }
}