using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Proveedor de imágenes utilizado por el sistema Reanim.
    ///
    /// Orden de búsqueda:
    /// 1. Sprites registrados en runtime.
    /// 2. PvZReanimAtlas.
    /// 3. Resources de Unity, si está habilitado.
    ///
    /// El lector de recursos de PvZ podrá registrar sprites mediante
    /// RegisterSprite() sin necesidad de modificar el sistema Reanim.
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

        public int RegisteredSpriteCount =>
            runtimeSprites.Count;

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
            // 2. ATLAS
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
            // 3. UNITY RESOURCES
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
        // REGISTER SPRITE
        // =========================================================

        /// <summary>
        /// Registra un Sprite obtenido desde otro sistema de recursos.
        ///
        /// El lector del PAK podrá utilizar este método para entregar
        /// las imágenes al sistema Reanim.
        /// </summary>
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
        // CHECK REGISTERED
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