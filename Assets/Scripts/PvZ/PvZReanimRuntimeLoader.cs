using System.IO;
using UnityEngine;

namespace PvZReanim
{
    // Carga un .reanim SIEMPRE desde main.pak. Antes, si no lo
    // encontraba en el PAK, caía a buscar el archivo suelto en
    // StreamingAssets (LoadFromFile) y también manejaba un
    // PvZReanimSpriteLoader para PNG/JPG sueltos. Ya no: sólo se
    // usa el .pak para texturas y animaciones, así que si el
    // Reanim no está en el PAK esto falla (con un error claro) en
    // vez de buscarlo en otro lado.
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
            PvZReanimImageResolver newImageResolver)
        {
            imageProvider =
                newImageProvider;

            imageResolver =
                newImageResolver;
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

            if (imageResolver != null)
            {
                imageResolver.SetProvider(
                    imageProvider
                );
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Componentes encontrados | " +
                "Provider: " +
                (imageProvider != null) +
                " | Resolver: " +
                (imageResolver != null)
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
                    "La ruta del Reanim está vacía."
                );

                return;
            }

            hasLoaded = true;

            PvZReanimDefinition definition =
                LoadFromPak();

            if (definition == null)
            {
                Debug.LogError(
                    "PvZReanimRuntimeLoader: " +
                    "No se pudo cargar el Reanim desde " +
                    "main.pak:\n" +
                    relativePath
                );

                return;
            }

            Debug.Log(
                "[PvZReanimRuntimeLoader] " +
                "Definición obtenida | " +
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
                    "La definición cargada no es válida."
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
                    "No se encontró main.pak:\n" +
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
                    "No se encontró el Reanim original:\n" +
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
                    "defaultAnimName vacío: se va a " +
                    "reproducir el .reanim COMPLETO " +
                    "(todas las sub-animaciones " +
                    "concatenadas) en vez de una sola. " +
                    "Asigná defaultAnimName (ej. " +
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
