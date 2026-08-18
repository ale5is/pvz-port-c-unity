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
            Load();
        }

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
                Debug.LogWarning(
                    "PvZReanimRuntimeLoader: " +
                    "No existe todavía el archivo:\n" +
                    path +
                    "\n\n" +
                    "Esto es normal si todavía " +
                    "no tienes un .reanim real."
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

        private void CreateReanimation(
            PvZReanimDefinition definition)
        {
            if (reanimation != null)
            {
                Destroy(
                    reanimation.gameObject
                );

                reanimation = null;
            }

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
                "Reanim cargado correctamente:\n" +
                definition.name
            );
        }

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

        private void OnDestroy()
        {
            if (reanimation != null)
            {
                Destroy(
                    reanimation.gameObject
                );
            }
        }
    }
}