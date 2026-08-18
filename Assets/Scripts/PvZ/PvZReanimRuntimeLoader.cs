using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimRuntimeLoader : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath =
            "Reanim/test.reanim";

        [Header("Test Sprite")]
        [SerializeField]
        private bool createTestSprite = true;

        [SerializeField]
        private int textureSize = 64;

        [SerializeField]
        private Color testColor = Color.green;

        [SerializeField]
        private float pixelsPerUnit = 64f;

        [Header("Image System")]
        [SerializeField]
        private PvZReanimImageProvider imageProvider;

        [SerializeField]
        private PvZReanimImageResolver imageResolver;

        [SerializeField]
        private PvZReanimSpriteLoader spriteLoader;

        [Header("Playback")]
        [SerializeField]
        private PvZReanimLoopType loopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private float animRate = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool logInformation = true;

        [SerializeField]
        private bool logImageTest = true;

        private PvZReanimation reanimation;

        private Sprite testSprite;

        private Texture2D testTexture;

        public PvZReanimation Reanimation =>
            reanimation;

        private void Start()
        {
            FindImageComponents();
            Load();
        }

        // =========================================================
        // FIND IMAGE COMPONENTS
        // =========================================================

        private void FindImageComponents()
        {
            if (imageProvider == null)
            {
                imageProvider =
                    GetComponent<PvZReanimImageProvider>();
            }

            if (imageProvider == null)
            {
                imageProvider =
                    GetComponentInParent<
                        PvZReanimImageProvider
                    >();
            }

            if (imageProvider == null)
            {
                imageProvider =
                    FindFirstObjectByType<
                        PvZReanimImageProvider
                    >();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    GetComponent<PvZReanimImageResolver>();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    GetComponentInParent<
                        PvZReanimImageResolver
                    >();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    FindFirstObjectByType<
                        PvZReanimImageResolver
                    >();
            }

            if (spriteLoader == null)
            {
                spriteLoader =
                    GetComponent<PvZReanimSpriteLoader>();
            }

            if (spriteLoader == null)
            {
                spriteLoader =
                    GetComponentInParent<
                        PvZReanimSpriteLoader
                    >();
            }

            if (spriteLoader == null)
            {
                spriteLoader =
                    FindFirstObjectByType<
                        PvZReanimSpriteLoader
                    >();
            }

            if (imageResolver != null)
            {
                imageResolver.SetProvider(
                    imageProvider
                );

                imageResolver.SetSpriteLoader(
                    spriteLoader
                );
            }

            if (imageProvider != null)
            {
                imageProvider.SearchResources =
                    false;
            }
        }

        // =========================================================
        // LOAD
        // =========================================================

        public void Load()
        {
            if (string.IsNullOrWhiteSpace(
                    relativePath))
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "La ruta está vacía."
                );

                return;
            }

            string path =
                Path.Combine(
                    Application.streamingAssetsPath,
                    relativePath
                );

            path =
                path.Replace(
                    '\\',
                    Path.DirectorySeparatorChar
                );

            if (!File.Exists(path))
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "No existe el archivo:\n" +
                    path
                );

                return;
            }

            PvZReanimDefinition definition =
                PvZReanimFileLoader.LoadFile(
                    path
                );

            if (definition == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "No se pudo cargar la definición."
                );

                return;
            }

            if (!PvZReanimAssetLoader.IsValidDefinition(
                    definition))
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "La definición cargada no es válida."
                );

                return;
            }

            CreateTestSpriteIfNeeded();

            RegisterTestSprite();

            TestImageResolution();

            CreateReanimation(
                definition
            );
        }

        // =========================================================
        // TEST SPRITE
        // =========================================================

        private void CreateTestSpriteIfNeeded()
        {
            if (!createTestSprite)
                return;

            if (testSprite != null)
                return;

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
                "PvZ_Reanim_Runtime_Test_Texture";

            Color[] pixels =
                new Color[
                    size * size
                ];

            Vector2 center =
                new Vector2(
                    (size - 1) * 0.5f,
                    (size - 1) * 0.5f
                );

            float radius =
                size * 0.42f;

            for (int y = 0;
                 y < size;
                 y++)
            {
                for (int x = 0;
                     x < size;
                     x++)
                {
                    float distance =
                        Vector2.Distance(
                            new Vector2(
                                x,
                                y
                            ),
                            center
                        );

                    int index =
                        y * size + x;

                    if (distance <= radius)
                    {
                        pixels[index] =
                            testColor;
                    }
                    else
                    {
                        pixels[index] =
                            Color.clear;
                    }
                }
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
                "test";
        }

        // =========================================================
        // REGISTER TEST SPRITE
        // =========================================================

        private void RegisterTestSprite()
        {
            if (!createTestSprite ||
                testSprite == null)
            {
                return;
            }

            if (imageProvider == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "No existe PvZReanimImageProvider."
                );

                return;
            }

            imageProvider.RegisterSprite(
                "test",
                testSprite
            );

            if (logImageTest)
            {
                Debug.Log(
                    "[PvZReanim] " +
                    "Sprite de prueba registrado: test"
                );
            }
        }

        // =========================================================
        // TEST RESOLUTION
        // =========================================================

        private void TestImageResolution()
        {
            if (!logImageTest)
                return;

            if (imageResolver == null)
            {
                Debug.LogError(
                    "[PvZReanim] " +
                    "No existe PvZReanimImageResolver."
                );

                return;
            }

            Sprite resolved =
                imageResolver.Resolve(
                    "test"
                );

            if (resolved == null)
            {
                Debug.LogError(
                    "[PvZReanim] " +
                    "ERROR: el Resolver NO pudo encontrar test."
                );

                return;
            }

            Debug.Log(
                "[PvZReanim] " +
                "OK: test resuelto correctamente -> " +
                resolved.name
            );
        }

        // =========================================================
        // REANIMATION
        // =========================================================

        private void CreateReanimation(
            PvZReanimDefinition definition)
        {
            DestroyReanimation();

            GameObject obj =
                new GameObject(
                    definition.name +
                    "_Reanimation"
                );

            obj.transform.SetParent(
                transform,
                false
            );

            reanimation =
                obj.AddComponent<
                    PvZReanimation
                >();

            reanimation.SetImageResolver(
                imageResolver
            );

            reanimation.Initialize(
                definition
            );

            reanimation.Play(
                loopType,
                animRate
            );

            if (logInformation)
            {
                PvZReanimAssetLoader.LogDefinitionInfo(
                    definition
                );
            }

            Debug.Log(
                "PvZReanimRuntimeLoader: " +
                "Reanim cargado correctamente: " +
                definition.name
            );
        }

        // =========================================================
        // CONTROLS
        // =========================================================

        public void Stop()
        {
            if (reanimation == null)
                return;

            reanimation.ReanimationDie();
        }

        public void Restart()
        {
            if (reanimation == null)
                return;

            reanimation.Play(
                loopType,
                animRate
            );
        }

        public void SetAnimRate(
            float rate)
        {
            animRate =
                rate;

            if (reanimation != null)
            {
                reanimation.AnimRate =
                    rate;
            }
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private void DestroyReanimation()
        {
            if (reanimation == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(
                    reanimation.gameObject
                );
            }
            else
            {
                DestroyImmediate(
                    reanimation.gameObject
                );
            }

            reanimation = null;
        }

        private void OnDestroy()
        {
            if (reanimation != null)
            {
                DestroyReanimation();
            }

            if (testSprite != null)
            {
                Destroy(
                    testSprite
                );

                testSprite = null;
            }

            if (testTexture != null)
            {
                Destroy(
                    testTexture
                );

                testTexture = null;
            }
        }
    }
}