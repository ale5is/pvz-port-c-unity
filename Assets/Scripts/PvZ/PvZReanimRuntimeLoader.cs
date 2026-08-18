using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimRuntimeLoader : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath =
            "reanim/PeaShooter.reanim";

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
        // COMPONENTES
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
                        PvZReanimImageProvider>();
            }

            if (imageProvider == null)
            {
                imageProvider =
                    FindFirstObjectByType<
                        PvZReanimImageProvider>();
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
                        PvZReanimImageResolver>();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    FindFirstObjectByType<
                        PvZReanimImageResolver>();
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
                        PvZReanimSpriteLoader>();
            }

            if (spriteLoader == null)
            {
                spriteLoader =
                    FindFirstObjectByType<
                        PvZReanimSpriteLoader>();
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
                    "La ruta del Reanim está vacía."
                );

                return;
            }

            PvZReanimDefinition definition =
                LoadFromPak();

            if (definition == null)
            {
                definition =
                    LoadFromFile();
            }

            if (definition == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "No se pudo cargar el Reanim:\n" +
                    relativePath
                );

                return;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Definición obtenida | " +
                "Tracks: " +
                definition.TrackCount +
                " | Frames: " +
                definition.GetMaxFrameCount() +
                " | FPS: " +
                definition.fps
            );

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
        // PAK
        // =========================================================

        private PvZReanimDefinition LoadFromPak()
        {
            string pakPath =
                Path.Combine(
                    Application.streamingAssetsPath,
                    "PvZ",
                    "main.pak"
                );

            if (!File.Exists(pakPath))
            {
                Debug.LogWarning(
                    "PvZReanimRuntimeLoader: " +
                    "No se encontró main.pak:\n" +
                    pakPath
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Buscando Reanim compilado en PAK:\n" +
                pakPath
            );

            PvZPakReader pak =
                new PvZPakReader();

            if (!pak.Load(pakPath))
            {
                Debug.LogWarning(
                    "PvZReanimRuntimeLoader: " +
                    "No se pudo cargar main.pak."
                );

                return null;
            }

            // =====================================================
            // CONVERTIR
            // =====================================================

            string normalizedPath =
                relativePath
                    .Replace('\\', '/')
                    .TrimStart('/');

            string compiledPath =
                "compiled/" +
                normalizedPath +
                ".compiled";

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Buscando compilado:\n" +
                compiledPath
            );

            byte[] compiledData;

            if (pak.TryGetFile(
                    compiledPath,
                    out compiledData))
            {
                Debug.Log(
                    "[PvZReanimRuntimeLoader] " +
                    "Reanim compilado encontrado:\n" +
                    compiledPath +
                    " | Bytes: " +
                    compiledData.Length
                );

                PvZReanimDefinition compiledDefinition =
                    PvZReanimCompiledLoader.LoadBytes(
                        compiledData
                    );

                if (compiledDefinition != null)
                {
                    compiledDefinition.name =
                        Path.GetFileNameWithoutExtension(
                            normalizedPath
                        );

                    Debug.Log(
                        "[PvZReanimRuntimeLoader] " +
                        "Reanim compilado reconstruido correctamente."
                    );

                    return compiledDefinition;
                }

                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "El compilado fue encontrado " +
                    "pero no pudo reconstruirse."
                );
            }
            else
            {
                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "No se encontró:\n" +
                    compiledPath
                );
            }

            // =====================================================
            // FALLBACK AL .REANIM
            // =====================================================

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Intentando Reanim original:\n" +
                normalizedPath
            );

            byte[] data;

            if (!pak.TryGetFile(
                    normalizedPath,
                    out data))
            {
                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "No se encontró el Reanim original:\n" +
                    normalizedPath
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Reanim original encontrado:\n" +
                normalizedPath +
                " | Bytes: " +
                data.Length
            );

            PvZReanimDefinition definition =
                PvZReanimFileLoader.LoadBytes(
                    data
                );

            if (definition == null)
            {
                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "El Reanim original no pudo parsearse."
                );

                return null;
            }

            definition.name =
                Path.GetFileNameWithoutExtension(
                    normalizedPath
                );

            return definition;
        }

        // =========================================================
        // ARCHIVO FÍSICO
        // =========================================================

        private PvZReanimDefinition LoadFromFile()
        {
            string path =
                Path.Combine(
                    Application.streamingAssetsPath,
                    relativePath
                );

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PvZReanimRuntimeLoader: " +
                    "No existe archivo físico:\n" +
                    path
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Cargando Reanim físico:\n" +
                path
            );

            PvZReanimDefinition definition =
                PvZReanimFileLoader.LoadFile(
                    path
                );

            if (definition != null)
            {
                definition.name =
                    Path.GetFileNameWithoutExtension(
                        relativePath
                    );
            }

            return definition;
        }

        // =========================================================
        // CREAR REANIMATION
        // =========================================================

        private void CreateReanimation(
            PvZReanimDefinition definition)
        {
            DestroyReanimation();

            string objectName =
                string.IsNullOrEmpty(
                    definition.name)
                    ? "Reanimation"
                    : definition.name +
                      "_Reanimation";

            GameObject obj =
                new GameObject(
                    objectName
                );

            obj.transform.SetParent(
                transform,
                false
            );

            reanimation =
                obj.AddComponent<
                    PvZReanimation>();

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
        // CONTROL
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
            animRate = rate;

            if (reanimation != null)
            {
                reanimation.AnimRate =
                    rate;
            }
        }

        // =========================================================
        // LIMPIEZA
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