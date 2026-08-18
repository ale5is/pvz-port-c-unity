using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimImageResolver :
        MonoBehaviour
    {
        [Header("Atlas")]
        [SerializeField]
        private PvZReanimAtlas atlas;

        [Header("Options")]
        [SerializeField]
        private bool searchResourcesIfMissing = false;

        public PvZReanimAtlas Atlas
        {
            get => atlas;
            set => atlas = value;
        }

        public bool SearchResourcesIfMissing
        {
            get => searchResourcesIfMissing;
            set => searchResourcesIfMissing = value;
        }

        public Sprite Resolve(
            string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;

            /*
             * 1. Atlas.
             */

            if (atlas != null)
            {
                Sprite sprite =
                    atlas.GetSprite(
                        imageName
                    );

                if (sprite != null)
                    return sprite;
            }

            /*
             * 2. Resources.
             *
             * Esto queda como fallback.
             *
             * NO es el sistema final de PvZ.
             * Más adelante será reemplazado
             * por nuestro sistema de recursos PAK.
             */

            if (searchResourcesIfMissing)
            {
                Sprite sprite =
                    Resources.Load<Sprite>(
                        NormalizeResourcePath(
                            imageName
                        )
                    );

                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        public bool HasImage(
            string imageName)
        {
            return Resolve(
                imageName
            ) != null;
        }

        public void ClearAtlas()
        {
            atlas = null;
        }

        private string NormalizeResourcePath(
            string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return string.Empty;

            string path =
                imageName.Trim();

            path =
                path.Replace(
                    '\\',
                    '/'
                );

            /*
             * Resources.Load no necesita
             * extensión.
             */

            if (path.EndsWith(
                ".png",
                System.StringComparison.OrdinalIgnoreCase))
            {
                path =
                    path.Substring(
                        0,
                        path.Length - 4
                    );
            }

            if (path.EndsWith(
                ".jpg",
                System.StringComparison.OrdinalIgnoreCase))
            {
                path =
                    path.Substring(
                        0,
                        path.Length - 4
                    );
            }

            if (path.EndsWith(
                ".jpeg",
                System.StringComparison.OrdinalIgnoreCase))
            {
                path =
                    path.Substring(
                        0,
                        path.Length - 5
                    );
            }

            return path;
        }
    }
}