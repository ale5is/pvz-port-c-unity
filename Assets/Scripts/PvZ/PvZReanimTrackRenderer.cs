using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        private PvZReanimMeshRenderer meshRenderer;

        private PvZReanimAtlas atlas;

        private SpriteRenderer legacySpriteRenderer;

        private void Awake()
        {
            InitializeRenderer();
        }

        private void InitializeRenderer()
        {
            meshRenderer =
                GetComponent<PvZReanimMeshRenderer>();

            if (meshRenderer == null)
            {
                meshRenderer =
                    gameObject.AddComponent<
                        PvZReanimMeshRenderer
                    >();
            }
        }

        public void SetAtlas(
            PvZReanimAtlas newAtlas)
        {
            atlas =
                newAtlas;
        }

        public PvZReanimAtlas Atlas =>
            atlas;

        public void Apply(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (reanimTransform == null ||
                instance == null)
            {
                Hide();
                return;
            }

            if (meshRenderer == null)
            {
                InitializeRenderer();
            }

            Sprite sprite =
                ResolveSprite(
                    reanimTransform,
                    instance
                );

            if (sprite == null)
            {
                Hide();
                return;
            }

            meshRenderer.Apply(
                sprite,
                reanimTransform,
                instance
            );
        }

        private Sprite ResolveSprite(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            /*
             * Prioridad:
             *
             * 1. Override manual
             * 2. Sprite almacenado directamente
             * 3. Sprite obtenido del atlas
             */

            if (instance.imageOverride != null)
            {
                return instance.imageOverride;
            }

            if (reanimTransform.image != null)
            {
                return reanimTransform.image;
            }

            if (atlas != null &&
                !string.IsNullOrEmpty(
                    reanimTransform.imageName))
            {
                Sprite sprite =
                    atlas.GetSprite(
                        reanimTransform.imageName
                    );

                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private void Hide()
        {
            if (meshRenderer == null)
                return;

            /*
             * Aplicamos un estado oculto.
             *
             * El renderer del mesh controla
             * posteriormente su visibilidad.
             */

            MeshRenderer renderer =
                GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        public void SetSorting(
            int sortingLayerID,
            int sortingOrder)
        {
            if (meshRenderer == null)
            {
                InitializeRenderer();
            }

            meshRenderer.SetSorting(
                sortingLayerID,
                sortingOrder
            );
        }

        /*
         * Compatibilidad con código anterior.
         *
         * Si alguna parte del proyecto todavía
         * intenta acceder al SpriteRenderer,
         * no rompemos inmediatamente el sistema.
         */
        public SpriteRenderer GetLegacySpriteRenderer()
        {
            if (legacySpriteRenderer == null)
            {
                legacySpriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            return legacySpriteRenderer;
        }
    }
}