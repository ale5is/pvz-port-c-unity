using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimImageProvider : MonoBehaviour
    {

        [Header("Atlas")]
        [SerializeField]
        private PvZReanimAtlas atlas;

        [Header("Resources")]
        [SerializeField]
        private bool searchResources = false;

        [Header("PAK")]
        [SerializeField]
        private bool searchPak = true;

        [SerializeField]
        private PvZPakImageProvider pakProvider;

        private readonly Dictionary<string, Sprite> runtimeSprites =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase
            );

        public PvZReanimAtlas Atlas
        {
            get
            {
                return atlas;
            }

            set
            {
                atlas = value;
            }
        }

        public bool SearchResources
        {
            get
            {
                return searchResources;
            }

            set
            {
                searchResources = value;
            }
        }

        public bool SearchPak
        {
            get
            {
                return searchPak;
            }

            set
            {
                searchPak = value;
            }
        }

        public PvZPakImageProvider PakProvider
        {
            get
            {
                return pakProvider;
            }

            set
            {
                pakProvider = value;
            }
        }

        public int RegisteredSpriteCount
        {
            get
            {
                return runtimeSprites.Count;
            }
        }

        private void Awake()
        {
            FindPakProvider();
        }

        private void FindPakProvider()
        {
            if (pakProvider != null)
                return;

            pakProvider =
                PvZPakImageProvider.Instance;

            if (pakProvider != null)
                return;

            pakProvider =
                FindFirstObjectByType<
                    PvZPakImageProvider
                >();
        }

        public virtual Sprite Resolve(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return null;
            }

            Sprite sprite;

            if (searchPak)
            {
                FindPakProvider();

                if (pakProvider != null &&
                    pakProvider.IsReady)
                {
                    sprite =
                        pakProvider.LoadSprite(
                            imageName
                        );

                    if (sprite != null)
                    {
                        Debug.Log(
                            "[PvZReanimImageProvider] " +
                            "Sprite resuelto desde PAK: " +
                            imageName,
                            this
                        );

                        return sprite;
                    }
                }
            }

            if (runtimeSprites.TryGetValue(
                    normalized,
                    out sprite))
            {
                if (sprite != null)
                    return sprite;

                runtimeSprites.Remove(
                    normalized
                );
            }

            if (atlas != null)
            {
                sprite =
                    atlas.GetSprite(
                        normalized
                    );

                if (sprite != null)
                    return sprite;

                if (!string.Equals(
                        imageName,
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    sprite =
                        atlas.GetSprite(
                            imageName
                        );

                    if (sprite != null)
                        return sprite;
                }
            }

            if (searchResources)
            {
                sprite =
                    Resources.Load<Sprite>(
                        normalized
                    );

                if (sprite != null)
                    return sprite;

                sprite =
                    Resources.Load<Sprite>(
                        imageName
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        public virtual bool TryResolve(
            string imageName,
            out Sprite sprite)
        {
            sprite =
                Resolve(
                    imageName
                );

            return sprite != null;
        }

        public virtual bool Contains(
            string imageName)
        {
            return Resolve(
                imageName
            ) != null;
        }

        public void RegisterSprite(
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
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return;
            }

            runtimeSprites[
                normalized
            ] = sprite;
        }

        public bool UnregisterSprite(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return false;
            }

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return false;
            }

            return runtimeSprites.Remove(
                normalized
            );
        }

        public bool HasRegisteredSprite(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return false;
            }

            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(
                    normalized))
            {
                return false;
            }

            Sprite sprite;

            if (runtimeSprites.TryGetValue(
                    normalized,
                    out sprite))
            {
                return sprite != null;
            }

            return false;
        }

        // =========================================================
        // CLEAR
        // =========================================================

        public void ClearRegisteredSprites()
        {
            runtimeSprites.Clear();
        }

        // =========================================================
        // ATLAS
        // =========================================================

        public void SetAtlas(
            PvZReanimAtlas newAtlas)
        {
            atlas =
                newAtlas;
        }

        public PvZReanimAtlas GetAtlas()
        {
            return atlas;
        }

        public void SetPakProvider(
            PvZPakImageProvider newProvider)
        {
            pakProvider =
                newProvider;
        }

        public PvZPakImageProvider GetPakProvider()
        {
            FindPakProvider();

            return pakProvider;
        }

        public virtual void Clear()
        {
            runtimeSprites.Clear();
        }
    }
}