using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Carga sprites desde archivos de imagen y los registra
    /// en PvZReanimImageProvider.
    ///
    /// Esta clase NO interpreta PAK todavía.
    /// Sirve como capa de entrada de imágenes para Reanim.
    /// </summary>
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
        private bool loadOnStart = false;

        [SerializeField]
        private bool recursiveSearch = true;

        private readonly Dictionary<string, Sprite> loadedSprites =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase
            );

        public PvZReanimImageProvider Provider =>
            provider;

        public string ImageFolder =>
            imageFolder;

        public int LoadedSpriteCount =>
            loadedSprites.Count;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            FindProvider();

            if (loadOnStart)
            {
                LoadAll();
            }
        }

        // =========================================================
        // PROVIDER
        // =========================================================

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

        // =========================================================
        // LOAD ALL
        // =========================================================

        public int LoadAll()
        {
            if (provider == null)
            {
                FindProvider();
            }

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
                    "No existe la carpeta de imágenes: " +
                    folder,
                    this
                );

                return 0;
            }

            string[] files;

            SearchOption searchOption =
                recursiveSearch
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

            files =
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
                if (!IsSupportedImage(
                        files[i]))
                {
                    continue;
                }

                if (LoadFile(
                        files[i]))
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

        // =========================================================
        // LOAD FILE
        // =========================================================

        public bool LoadFile(
            string path)
        {
            if (provider == null)
            {
                FindProvider();
            }

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
                    "No existe el archivo: " +
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
                    "No se pudo leer: " +
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
                DestroyTexture(
                    texture
                );

                Debug.LogWarning(
                    "PvZReanimSpriteLoader: " +
                    "No se pudo decodificar: " +
                    path,
                    this
                );

                return false;
            }

            texture.name =
                Path.GetFileNameWithoutExtension(
                    path
                );

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

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
                Path.GetFileNameWithoutExtension(
                    path
                );

            string imageName =
                Path.GetFileNameWithoutExtension(
                    path
                );

            Register(
                imageName,
                sprite
            );

            return true;
        }

        // =========================================================
        // REGISTER
        // =========================================================

        public void Register(
            string imageName,
            Sprite sprite)
        {
            if (string.IsNullOrEmpty(imageName))
                return;

            if (sprite == null)
                return;

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return;

            Sprite previous;

            if (loadedSprites.TryGetValue(
                    normalized,
                    out previous))
            {
                if (previous != null &&
                    previous != sprite)
                {
                    DestroySprite(
                        previous
                    );
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

        // =========================================================
        // GET
        // =========================================================

        public Sprite Get(
            string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (loadedSprites.TryGetValue(
                    normalized,
                    out Sprite sprite))
            {
                return sprite;
            }

            return null;
        }

        // =========================================================
        // FOLDER
        // =========================================================

        private string GetImageFolderPath()
        {
            string projectPath =
                Application.dataPath;

            string folder =
                Path.Combine(
                    projectPath,
                    imageFolder
                );

            return folder;
        }

        // =========================================================
        // SUPPORTED FILES
        // =========================================================

        private bool IsSupportedImage(
            string path)
        {
            string extension =
                Path.GetExtension(
                    path
                );

            if (string.IsNullOrEmpty(extension))
                return false;

            extension =
                extension.ToLowerInvariant();

            return
                extension == ".png" ||
                extension == ".jpg" ||
                extension == ".jpeg";
        }

        // =========================================================
        // CLEAR
        // =========================================================

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
                    DestroySprite(
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

        // =========================================================
        // CLEANUP
        // =========================================================

        private void OnDestroy()
        {
            Clear();
        }

        // =========================================================
        // DESTROY
        // =========================================================

        private void DestroySprite(
            Sprite sprite)
        {
            if (sprite == null)
                return;

            Texture2D texture =
                sprite.texture;

            if (Application.isPlaying)
            {
                Destroy(
                    sprite
                );

                if (texture != null)
                {
                    Destroy(
                        texture
                    );
                }
            }
            else
            {
                DestroyImmediate(
                    sprite
                );

                if (texture != null)
                {
                    DestroyImmediate(
                        texture
                    );
                }
            }
        }

        private void DestroyTexture(
            Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(
                    texture
                );
            }
            else
            {
                DestroyImmediate(
                    texture
                );
            }
        }
    }
}