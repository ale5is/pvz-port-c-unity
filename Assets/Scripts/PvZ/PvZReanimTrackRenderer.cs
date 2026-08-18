using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTrackRenderer :
        MonoBehaviour
    {
        private PvZReanimMeshRenderer meshRenderer;

        private PvZReanimImageResolver imageResolver;

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

            imageResolver =
                GetComponent<
                    PvZReanimImageResolver
                >();
        }

        public void SetImageResolver(
            PvZReanimImageResolver resolver)
        {
            imageResolver =
                resolver;
        }

        public PvZReanimImageResolver
            ImageResolver =>
            imageResolver;

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
            PvZReanimTransform transform,
            PvZReanimTrackInstance instance)
        {
            /*
             * PRIORIDAD 1
             *
             * Override manual.
             */

            if (instance.imageOverride != null)
            {
                return instance.imageOverride;
            }

            /*
             * PRIORIDAD 2
             *
             * Sprite que ya haya sido asignado
             * directamente al transform.
             */

            if (transform.image != null)
            {
                return transform.image;
            }

            /*
             * PRIORIDAD 3
             *
             * Resolver externo.
             */

            if (imageResolver != null &&
                !string.IsNullOrEmpty(
                    transform.imageName))
            {
                return imageResolver.Resolve(
                    transform.imageName
                );
            }

            return null;
        }

        private void Hide()
        {
            if (meshRenderer == null)
                return;

            meshRenderer.Hide();
        }

        public void Show()
        {
            if (meshRenderer == null)
            {
                InitializeRenderer();
            }

            meshRenderer.Show();
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
    }
}