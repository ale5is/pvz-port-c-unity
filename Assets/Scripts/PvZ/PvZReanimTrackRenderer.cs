using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Apply(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (reanimTransform == null || instance == null)
                return;

            Sprite sprite =
                instance.imageOverride != null
                    ? instance.imageOverride
                    : reanimTransform.image;

            spriteRenderer.sprite = sprite;

            float x =
                reanimTransform.x ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : reanimTransform.x;

            float y =
                reanimTransform.y ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : reanimTransform.y;

            // Transform de Unity.
            base.transform.localPosition =
                new Vector3(x, y, 0f);

            float scaleX =
                reanimTransform.scaleX ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : reanimTransform.scaleX;

            float scaleY =
                reanimTransform.scaleY ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : reanimTransform.scaleY;

            base.transform.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f
                );

            Color color =
                instance.trackColor;

            float alpha =
                reanimTransform.alpha ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : reanimTransform.alpha;

            color.a *= alpha;

            spriteRenderer.color = color;

            spriteRenderer.enabled =
                sprite != null &&
                instance.renderGroup !=
                PvZReanimRenderGroup.Hidden;
        }
    }
}