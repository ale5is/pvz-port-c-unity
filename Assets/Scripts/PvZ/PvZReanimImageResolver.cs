using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimImageResolver : MonoBehaviour
    {
        [Header("Provider")]
        [SerializeField]
        private PvZReanimImageProvider provider;

        [Header("Sprite Loader")]
        [SerializeField]
        private PvZReanimSpriteLoader spriteLoader;

        [Header("Resources")]
        [SerializeField]
        private bool searchResourcesIfMissing;

        [Header("Debug")]
        [SerializeField]
        private bool logMissingImages;

        private Dictionary<string, Sprite> cache;

        public PvZReanimImageProvider Provider
        {
            get => provider;

            set
            {
                provider = value;
                ClearCache();
            }
        }

        public PvZReanimSpriteLoader SpriteLoader
        {
            get => spriteLoader;

            set
            {
                spriteLoader = value;
                ClearCache();
            }
        }

        public bool SearchResourcesIfMissing
        {
            get => searchResourcesIfMissing;
            set => searchResourcesIfMissing = value;
        }

        public bool LogMissingImages
        {
            get => logMissingImages;
            set => logMissingImages = value;
        }

        private void Awake()
        {
            FindProvider();
            FindSpriteLoader();
            BuildCache();
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

        private void FindSpriteLoader()
        {
            if (spriteLoader != null)
                return;

            spriteLoader =
                GetComponent<PvZReanimSpriteLoader>();

            if (spriteLoader == null)
            {
                spriteLoader =
                    GetComponentInParent<
                        PvZReanimSpriteLoader
                    >();
            }

            if (spriteLoader == null)
            {
                spriteLoader =
                    FindFirstObjectByType<
                        PvZReanimSpriteLoader
                    >();
            }
        }

        public void SetProvider(
            PvZReanimImageProvider newProvider)
        {
            provider = newProvider;
            ClearCache();
        }

        public void SetSpriteLoader(
            PvZReanimSpriteLoader newSpriteLoader)
        {
            spriteLoader = newSpriteLoader;
            ClearCache();
        }

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

        public Sprite Resolve(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return null;

            if (cache == null)
                BuildCache();

            string normalizedName =
                NormalizeName(imageName);

            if (string.IsNullOrEmpty(normalizedName))
                return null;

            if (cache.TryGetValue(
                    normalizedName,
                    out Sprite cachedSprite))
            {
                if (cachedSprite != null)
                    return cachedSprite;

                cache.Remove(normalizedName);
            }

            FindProvider();

            Sprite sprite =
                ResolveFromProvider(
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

            FindSpriteLoader();

            sprite =
                ResolveFromSpriteLoader(
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

        private Sprite ResolveFromProvider(
            string originalName,
            string normalizedName)
        {
            if (provider == null)
                return null;

            Sprite sprite =
                provider.Resolve(
                    originalName
                );

            if (sprite != null)
                return sprite;

            if (!string.Equals(
                    originalName,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                sprite =
                    provider.Resolve(
                        normalizedName
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        private Sprite ResolveFromSpriteLoader(
            string originalName,
            string normalizedName)
        {
            if (spriteLoader == null)
                return null;

            Sprite sprite =
                spriteLoader.Get(
                    originalName
                );

            if (sprite != null)
                return sprite;

            if (!string.Equals(
                    originalName,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                sprite =
                    spriteLoader.Get(
                        normalizedName
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        private Sprite ResolveFromResources(
            string originalName,
            string normalizedName)
        {
            string resourcePath =
                NormalizeResourcePath(
                    originalName
                );

            if (!string.IsNullOrEmpty(resourcePath))
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
        private void AddToCache(
            string originalName,
            string normalizedName,
            Sprite sprite)
        {
            if (cache == null)
                BuildCache();

            if (sprite == null)
                return;

            if (!string.IsNullOrEmpty(originalName))
            {
                cache[
                    originalName.Trim()
                ] = sprite;
            }

            if (!string.IsNullOrEmpty(normalizedName))
            {
                cache[
                    normalizedName
                ] = sprite;
            }
        }
        public static string NormalizeName(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return string.Empty;

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

            return result.Trim();
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
            if (string.IsNullOrWhiteSpace(imageName))
                return string.Empty;

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