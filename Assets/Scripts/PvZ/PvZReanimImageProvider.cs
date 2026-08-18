using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Proveedor de imágenes utilizado por el sistema Reanim.
    ///
    /// Orden:
    /// 1. Sprites registrados en runtime.
    /// 2. main.pak.
    /// 3. PvZReanimAtlas.
    /// 4. Resources.
    /// </summary>
    public class PvZReanimImageProvider : MonoBehaviour
    {
        // =========================================================
        // ATLAS
        // =========================================================

        [Header("Atlas")]
        [SerializeField]
        private PvZReanimAtlas atlas;

        // =========================================================
        // RESOURCES
        // =========================================================

        [Header("Resources")]
        [SerializeField]
        private bool searchResources = false;

        // =========================================================
        // PAK
        // =========================================================

        [Header("PAK")]
        [SerializeField]
        private bool searchPak = true;

        [SerializeField]
        private PvZPakImageProvider pakProvider;

        // =========================================================
        // RUNTIME SPRITES
        // =========================================================

        private readonly Dictionary<string, Sprite> runtimeSprites =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase
            );

        // =========================================================
        // PROPERTIES
        // =========================================================

        public PvZReanimAtlas Atlas
        {
            get => atlas;
            set => atlas = value;
        }

        public bool SearchResources
        {
            get => searchResources;
            set => searchResources = value;
        }

        public bool SearchPak
        {
            get => searchPak;
            set => searchPak = value;
        }

        public PvZPakImageProvider PakProvider
        {
            get => pakProvider;
            set => pakProvider = value;
        }

        public int RegisteredSpriteCount =>
            runtimeSprites.Count;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            if (pakProvider == null)
            {
                pakProvider =
                    PvZPakImageProvider.Instance;
            }
        }

        // =========================================================
        // RESOLVE
        // =========================================================

        public virtual Sprite Resolve(
            string imageName)
        {
            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return null;

            Sprite sprite;

            // -----------------------------------------------------
            // 1. RUNTIME
            // -----------------------------------------------------

            if (runtimeSprites.TryGetValue(
                    normalized,
                    out sprite))
            {
                return sprite;
            }

            // -----------------------------------------------------
            // 2. PAK
            // -----------------------------------------------------

            if (searchPak)
            {
                if (pakProvider == null)
                {
                    pakProvider =
                        PvZPakImageProvider.Instance;
                }

                if (pakProvider != null &&
                    pakProvider.IsReady)
                {
                    sprite =
                        pakProvider.LoadSprite(
                            normalized
                        );

                    if (sprite != null)
                    {
                        RegisterSprite(
                            normalized,
                            sprite
                        );

                        Debug.Log(
                            "[PvZReanimImageProvider] " +
                            "Sprite resuelto desde PAK: " +
                            normalized,
                            this
                        );

                        return sprite;
                    }

                    // Intentar también con el nombre
                    // original por si conserva
                    // mayúsculas o ruta.
                    if (!string.Equals(
                            imageName,
                            normalized,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        sprite =
                            pakProvider.LoadSprite(
                                imageName
                            );

                        if (sprite != null)
                        {
                            RegisterSprite(
                                normalized,
                                sprite
                            );

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
            }

            // -----------------------------------------------------
            // 3. ATLAS
            // -----------------------------------------------------

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

            // -----------------------------------------------------
            // 4. UNITY RESOURCES
            // -----------------------------------------------------

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

        // =========================================================
        // TRY RESOLVE
        // =========================================================

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

        // =========================================================
        // CONTAINS
        // =========================================================

        public virtual bool Contains(
            string imageName)
        {
            return Resolve(
                imageName
            ) != null;
        }

        // =========================================================
        // REGISTER
        // =========================================================

        public void RegisterSprite(
            string imageName,
            Sprite sprite)
        {
            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return;

            if (sprite == null)
                return;

            runtimeSprites[
                normalized
            ] = sprite;
        }

        // =========================================================
        // UNREGISTER
        // =========================================================

        public bool UnregisterSprite(
            string imageName)
        {
            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return false;

            return runtimeSprites.Remove(
                normalized
            );
        }

        // =========================================================
        // CHECK
        // =========================================================

        public bool HasRegisteredSprite(
            string imageName)
        {
            string normalized =
                PvZReanimImageResolver.NormalizeName(
                    imageName
                );

            if (string.IsNullOrEmpty(normalized))
                return false;

            return runtimeSprites.ContainsKey(
                normalized
            );
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

        // =========================================================
        // CLEANUP
        // =========================================================

        public virtual void Clear()
        {
            runtimeSprites.Clear();
        }
    }
}