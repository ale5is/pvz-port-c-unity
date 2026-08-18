using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Resuelve los nombres de imágenes utilizados por Reanim
    /// hasta obtener un Sprite de Unity.
    ///
    /// Orden de búsqueda:
    ///
    /// 1. Cache interno
    /// 2. Atlas
    /// 3. Resources
    ///
    /// El sistema está preparado para que posteriormente
    /// el proveedor de imágenes pueda ser reemplazado
    /// por el sistema de recursos/PAK de PvZ.
    /// </summary>
    public class PvZReanimImageResolver :
        MonoBehaviour
    {
        [Header("Atlas")]
        [SerializeField]
        private PvZReanimAtlas atlas;

        [Header("Resources")]
        [SerializeField]
        private bool searchResourcesIfMissing;

        [Header("Debug")]
        [SerializeField]
        private bool logMissingImages;

        private Dictionary<string, Sprite> cache;

        public PvZReanimAtlas Atlas
        {
            get => atlas;

            set
            {
                atlas = value;

                ClearCache();
            }
        }

        public bool SearchResourcesIfMissing
        {
            get => searchResourcesIfMissing;

            set =>
                searchResourcesIfMissing =
                    value;
        }

        public bool LogMissingImages
        {
            get => logMissingImages;

            set =>
                logMissingImages =
                    value;
        }

        private void Awake()
        {
            BuildCache();
        }

        // =========================================================
        // CACHE
        // =========================================================

        public void BuildCache()
        {
            cache =
                new Dictionary<string, Sprite>(
                    StringComparer.OrdinalIgnoreCase
                );
        }

        public void ClearCache()
        {
            if (cache == null)
            {
                BuildCache();
                return;
            }

            cache.Clear();
        }

        // =========================================================
        // RESOLVE
        // =========================================================

        public Sprite Resolve(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                imageName))
            {
                return null;
            }

            if (cache == null)
            {
                BuildCache();
            }

            string normalizedName =
                NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                normalizedName))
            {
                return null;
            }

            // -----------------------------------------------------
            // 1. CACHE
            // -----------------------------------------------------

            Sprite cachedSprite;

            if (cache.TryGetValue(
                normalizedName,
                out cachedSprite))
            {
                return cachedSprite;
            }

            // -----------------------------------------------------
            // 2. ATLAS
            // -----------------------------------------------------

            Sprite sprite =
                ResolveFromAtlas(
                    imageName,
                    normalizedName
                );

            if (sprite != null)
            {
                AddToCache(
                    imageName,
                    normalizedName,
                    sprite
                );

                return sprite;
            }

            // -----------------------------------------------------
            // 3. RESOURCES
            // -----------------------------------------------------

            if (searchResourcesIfMissing)
            {
                sprite =
                    ResolveFromResources(
                        imageName,
                        normalizedName
                    );

                if (sprite != null)
                {
                    AddToCache(
                        imageName,
                        normalizedName,
                        sprite
                    );

                    return sprite;
                }
            }

            // -----------------------------------------------------
            // MISSING
            // -----------------------------------------------------

            if (logMissingImages)
            {
                Debug.LogWarning(
                    "[PvZReanim] " +
                    "No se pudo resolver la imagen: " +
                    imageName,
                    this
                );
            }

            return null;
        }

        // =========================================================
        // ATLAS
        // =========================================================

        private Sprite ResolveFromAtlas(
            string originalName,
            string normalizedName)
        {
            if (atlas == null)
                return null;

            // Primero intentamos el nombre original.

            Sprite sprite =
                atlas.GetSprite(
                    originalName
                );

            if (sprite != null)
                return sprite;

            // Después el nombre normalizado.

            if (!string.Equals(
                originalName,
                normalizedName,
                StringComparison.OrdinalIgnoreCase))
            {
                sprite =
                    atlas.GetSprite(
                        normalizedName
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        // =========================================================
        // RESOURCES
        // =========================================================

        private Sprite ResolveFromResources(
            string originalName,
            string normalizedName)
        {
            string resourcePath =
                NormalizeResourcePath(
                    originalName
                );

            if (!string.IsNullOrEmpty(
                resourcePath))
            {
                Sprite sprite =
                    Resources.Load<Sprite>(
                        resourcePath
                    );

                if (sprite != null)
                    return sprite;
            }

            if (!string.Equals(
                resourcePath,
                normalizedName,
                StringComparison.OrdinalIgnoreCase))
            {
                Sprite sprite =
                    Resources.Load<Sprite>(
                        normalizedName
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        // =========================================================
        // CACHE INSERT
        // =========================================================

        private void AddToCache(
            string originalName,
            string normalizedName,
            Sprite sprite)
        {
            if (cache == null)
            {
                BuildCache();
            }

            if (sprite == null)
                return;

            if (!string.IsNullOrEmpty(
                originalName))
            {
                cache[
                    originalName.Trim()
                ] = sprite;
            }

            if (!string.IsNullOrEmpty(
                normalizedName))
            {
                cache[
                    normalizedName
                ] = sprite;
            }
        }

        // =========================================================
        // NAME NORMALIZATION
        // =========================================================

        public static string NormalizeName(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                imageName))
            {
                return string.Empty;
            }

            string result =
                imageName.Trim();

            // -----------------------------------------------------
            // Comillas
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
            // Separadores
            // -----------------------------------------------------

            result =
                result.Replace(
                    '\\',
                    '/'
                );

            // -----------------------------------------------------
            // Quitar ruta
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
            // Quitar extensión
            // -----------------------------------------------------

            result =
                RemoveExtension(
                    result,
                    ".png"
                );

            result =
                RemoveExtension(
                    result,
                    ".jpg"
                );

            result =
                RemoveExtension(
                    result,
                    ".jpeg"
                );

            result =
                RemoveExtension(
                    result,
                    ".webp"
                );

            return result;
        }

        private static string RemoveExtension(
            string value,
            string extension)
        {
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

            return value;
        }

        private static string NormalizeResourcePath(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                imageName))
            {
                return string.Empty;
            }

            string result =
                imageName.Trim();

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

            result =
                RemoveExtension(
                    result,
                    ".png"
                );

            result =
                RemoveExtension(
                    result,
                    ".jpg"
                );

            result =
                RemoveExtension(
                    result,
                    ".jpeg"
                );

            result =
                RemoveExtension(
                    result,
                    ".webp"
                );

            return result;
        }

        // =========================================================
        // QUERY
        // =========================================================

        public bool HasImage(
            string imageName)
        {
            return Resolve(
                imageName
            ) != null;
        }

        public bool TryResolve(
            string imageName,
            out Sprite sprite)
        {
            sprite =
                Resolve(
                    imageName
                );

            return sprite != null;
        }

        public int CachedImageCount
        {
            get
            {
                return cache != null
                    ? cache.Count
                    : 0;
            }
        }
    }
}