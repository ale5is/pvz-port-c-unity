using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Proveedor base de imágenes para el sistema Reanim.
    ///
    /// PvZReanimImageResolver se encarga de decidir
    /// cómo encontrar una imagen.
    ///
    /// Este componente representa la capa que entrega
    /// finalmente el Sprite.
    ///
    /// Actualmente utiliza un Atlas.
    /// Más adelante podrá ser conectado al sistema
    /// de recursos/PAK de PvZ sin modificar Reanimation.
    /// </summary>
    public class PvZReanimImageProvider :
        MonoBehaviour
    {
        [Header("Atlas")]
        [SerializeField]
        private PvZReanimAtlas atlas;

        public PvZReanimAtlas Atlas
        {
            get => atlas;
            set => atlas = value;
        }

        // =========================================================
        // RESOLVE
        // =========================================================

        public virtual Sprite Resolve(
            string imageName)
        {
            if (atlas == null)
                return null;

            if (string.IsNullOrWhiteSpace(
                imageName))
            {
                return null;
            }

            return atlas.GetSprite(
                imageName
            );
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
        // DEBUG
        // =========================================================

        public virtual void Clear()
        {
        }
    }
}