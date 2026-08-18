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

        private PvZReanimation reanimation;

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

            CreateReanimation(
                definition
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
                    string.IsNullOrEmpty(
                        definition.name
                    )
                        ? "Reanimation"
                        : definition.name +
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

            GameObject obj =
                reanimation.gameObject;

            reanimation = null;

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void OnDestroy()
        {
            DestroyReanimation();
        }
    }
}