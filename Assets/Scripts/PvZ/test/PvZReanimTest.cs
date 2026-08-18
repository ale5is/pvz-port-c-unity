using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTest : MonoBehaviour
    {
        private PvZReanimation reanimation;

        private void Start()
        {
            PvZReanimDefinition definition =
                PvZReanimLoader.CreateDefinition(
                    "Test",
                    12f
                );

            PvZReanimTrack body =
                PvZReanimLoader.AddTrack(
                    definition,
                    "body"
                );

            PvZReanimTransform frame0 =
                PvZReanimLoader.AddFrame(
                    body
                );

            frame0.x = 0f;
            frame0.y = 0f;

            frame0.scaleX = 1f;
            frame0.scaleY = 1f;

            frame0.alpha = 1f;

            PvZReanimTransform frame1 =
                PvZReanimLoader.AddFrame(
                    body
                );

            frame1.x = 0f;
            frame1.y = 30f;

            frame1.scaleX = 1.1f;
            frame1.scaleY = 1.1f;

            frame1.alpha = 1f;

            GameObject obj =
                new GameObject(
                    "ReanimTest"
                );

            obj.transform.SetParent(
                transform,
                false
            );

            reanimation =
                obj.AddComponent<
                    PvZReanimation
                >();

            // Inicializamos directamente
            // la definición.
            reanimation.Initialize(
                definition
            );

            reanimation.Play(
                PvZReanimLoopType.Loop,
                1f
            );
        }
    }
}