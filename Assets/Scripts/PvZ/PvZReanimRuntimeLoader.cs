using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimRuntimeLoader : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath = "";

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

        [SerializeField]
        private string defaultAnimName =
            "";

        [Header("Debug")]
        [SerializeField]
        private bool logInformation = true;

        private PvZReanimation reanimation;

        public PvZReanimation Reanimation =>
            reanimation;

        public string RelativePath =>
            relativePath;

        public void SetDefaultAnimName(
            string newAnimName)
        {
            defaultAnimName =
                newAnimName;
        }

        public void SetImageComponents(
            PvZReanimImageProvider newImageProvider,
            PvZReanimImageResolver newImageResolver,
            PvZReanimSpriteLoader newSpriteLoader = null)
        {
            imageProvider =
                newImageProvider;

            imageResolver =
                newImageResolver;

            if (newSpriteLoader != null)
            {
                spriteLoader =
                    newSpriteLoader;
            }
        }

        public void SetPlaybackDefaults(
            PvZReanimLoopType newLoopType,
            float newAnimRate)
        {
            loopType =
                newLoopType;

            animRate =
                newAnimRate;
        }


        public void SetReanimPath(
            string newRelativePath,
            bool reloadNow = true)
        {
            relativePath =
                newRelativePath;

            if (reloadNow &&
                Application.isPlaying)
            {
                ForceReload();
            }
        }

        public void ForceReload()
        {
            hasLoaded = false;
            Load();
        }

        private void Start()
        {
            FindImageComponents();
            Load();
        }

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
                imageProvider.SearchResources = false;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Componentes encontrados | " +
                "Provider: " +
                (imageProvider != null) +
                " | Resolver: " +
                (imageResolver != null) +
                " | SpriteLoader: " +
                (spriteLoader != null)
            );
        }

        private bool hasLoaded;

        public void Load()
        {
            if (hasLoaded)
                return;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "La ruta del Reanim est� vac�a."
                );

                return;
            }

            hasLoaded = true;

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
                "Definici�n obtenida | " +
                "Nombre: " +
                definition.name +
                " | Tracks: " +
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
                    "La definici�n cargada no es v�lida."
                );

                return;
            }

            CreateReanimation(
                definition
            );
        }

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
                    "No se encontr� main.pak:\n" +
                    pakPath
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Buscando Reanim original en PAK:\n" +
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

            string normalizedPath =
                relativePath
                    .Replace('\\', '/')
                    .TrimStart('/');

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Buscando dentro del PAK:\n" +
                normalizedPath
            );

            byte[] data;

            if (!pak.TryGetFile(
                    normalizedPath,
                    out data))
            {
                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "No se encontr� el Reanim original:\n" +
                    normalizedPath
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Reanim encontrado en PAK:\n" +
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
                Debug.LogError(
                    "[PvZReanimRuntimeLoader] " +
                    "El Reanim fue encontrado en el PAK, " +
                    "pero no pudo parsearse."
                );

                return null;
            }

            definition.name =
                Path.GetFileNameWithoutExtension(
                    normalizedPath
                );

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Reanim parseado correctamente."
            );

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Nombre: " +
                definition.name +
                " | FPS: " +
                definition.fps +
                " | Tracks: " +
                definition.TrackCount +
                " | Frames: " +
                definition.GetMaxFrameCount()
            );

            return definition;
        }

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
                    "No existe archivo f�sico:\n" +
                    path
                );

                return null;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Cargando Reanim f�sico:\n" +
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

            if (string.IsNullOrWhiteSpace(
                    defaultAnimName))
            {
                Debug.LogWarning(
                    "[PvZReanimRuntimeLoader] " +
                    "defaultAnimName vac�o: se va a " +
                    "reproducir el .reanim COMPLETO " +
                    "(todas las sub-animaciones " +
                    "concatenadas) en vez de una sola. " +
                    "Asign� defaultAnimName (ej. " +
                    "\"anim_idle\") en el Inspector o " +
                    "con SetDefaultAnimName()."
                );

                reanimation.Play(
                    loopType,
                    animRate
                );
            }
            else
            {
                reanimation.PlayReanim(
                    defaultAnimName,
                    loopType,
                    0,
                    animRate
                );
            }

            if (logInformation)
            {
                PvZReanimAssetLoader.LogDefinitionInfo(
                    definition
                );
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
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

            reanimation.Die();
        }

        public void Restart()
        {
            if (reanimation == null)
                return;

            if (string.IsNullOrWhiteSpace(
                    defaultAnimName))
            {
                reanimation.Play(
                    loopType,
                    animRate
                );
            }
            else
            {
                reanimation.PlayReanim(
                    defaultAnimName,
                    loopType,
                    0,
                    animRate
                );
            }
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