using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimRuntimeTest : MonoBehaviour
    {
        [Header("Test")]
        [SerializeField]
        private bool createTestSprite = true;

        [SerializeField]
        private int textureSize = 64;

        [SerializeField]
        private Color testColor = Color.green;

        [SerializeField]
        private float pixelsPerUnit = 64f;

        private PvZReanimation reanimation;

        private Sprite testSprite;

        private Texture2D testTexture;

        private void Start()
        {
            CreateTestAnimation();
        }

        private void CreateTestAnimation()
        {
            PvZReanimDefinition definition =
                PvZReanimLoader.CreateDefinition(
                    "RuntimeTest",
                    12f
                );

            if (definition == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeTest: " +
                    "No se pudo crear la definición."
                );

                return;
            }

            PvZReanimTrack body =
                PvZReanimLoader.AddTrack(
                    definition,
                    "body"
                );

            if (body == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeTest: " +
                    "No se pudo crear el track."
                );

                return;
            }

            PvZReanimTransform frame0 =
                PvZReanimLoader.AddFrame(
                    body
                );

            frame0.SetDefaults();

            frame0.x = 0f;
            frame0.y = 0f;

            frame0.scaleX = 1f;
            frame0.scaleY = 1f;

            frame0.skewX = 0f;
            frame0.skewY = 0f;

            frame0.alpha = 1f;

            PvZReanimTransform frame1 =
                PvZReanimLoader.AddFrame(
                    body
                );

            frame1.SetDefaults();

            frame1.x = 0f;
            frame1.y = 30f;

            frame1.scaleX = 1.15f;
            frame1.scaleY = 1.15f;

            frame1.skewX = 0f;
            frame1.skewY = 15f;

            frame1.alpha = 1f;

            PvZReanimTransform frame2 =
                PvZReanimLoader.AddFrame(
                    body
                );

            frame2.SetDefaults();

            frame2.x = 0f;
            frame2.y = 0f;

            frame2.scaleX = 1f;
            frame2.scaleY = 1f;

            frame2.skewX = 0f;
            frame2.skewY = 0f;

            frame2.alpha = 1f;

            if (createTestSprite)
            {
                testSprite =
                    CreateSprite();

                frame0.SetSprite(
                    testSprite
                );

                frame1.SetSprite(
                    testSprite
                );

                frame2.SetSprite(
                    testSprite
                );
            }

            GameObject obj =
                new GameObject(
                    "PvZ_Reanim_Runtime_Test"
                );

            obj.transform.SetParent(
                transform,
                false
            );

            reanimation =
                obj.AddComponent<
                    PvZReanimation
                >();

            reanimation.Initialize(
                definition
            );

            reanimation.Play(
                PvZReanimLoopType.Loop,
                1f
            );

            Debug.Log(
                "========================================\n" +
                "PvZ REANIM RUNTIME TEST\n" +
                "========================================\n" +
                "Definition: " +
                definition.name +
                "\nFPS: " +
                definition.fps +
                "\nTracks: " +
                definition.TrackCount +
                "\nFrames: " +
                definition.GetMaxFrameCount() +
                "\n========================================"
            );
        }

        private Sprite CreateSprite()
        {
            int size =
                Mathf.Max(
                    8,
                    textureSize
                );

            testTexture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false
                );

            testTexture.name =
                "PvZ_Reanim_Test_Texture";

            Color[] pixels =
                new Color[
                    size * size
                ];

            for (int i = 0;
                 i < pixels.Length;
                 i++)
            {
                pixels[i] =
                    testColor;
            }

            testTexture.SetPixels(
                pixels
            );

            testTexture.Apply();

            testSprite =
                Sprite.Create(
                    testTexture,
                    new Rect(
                        0f,
                        0f,
                        size,
                        size
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    pixelsPerUnit
                );

            testSprite.name =
                "PvZ_Reanim_Test_Sprite";

            return testSprite;
        }

        private void OnDestroy()
        {
            if (testSprite != null)
            {
                Destroy(
                    testSprite
                );
            }

            if (testTexture != null)
            {
                Destroy(
                    testTexture
                );
            }
        }
    }
}