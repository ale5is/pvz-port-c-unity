using System;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimAssetLoader
    {
        public static PvZReanimDefinition LoadReanim(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "La ruta está vacía."
                );

                return null;
            }

            if (!File.Exists(path))
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "No existe el archivo:\n" +
                    path
                );

                return null;
            }

            try
            {
                PvZReanimDefinition definition =
                    PvZReanimParser.LoadFile(
                        path
                    );

                if (definition == null)
                {
                    Debug.LogError(
                        "PvZReanimAssetLoader: " +
                        "El parser devolvió null."
                    );

                    return null;
                }

                definition.name =
                    Path.GetFileNameWithoutExtension(
                        path
                    );

                return definition;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "Error cargando:\n" +
                    path +
                    "\n\n" +
                    exception
                );

                return null;
            }
        }

        public static PvZReanimDefinition LoadReanimText(
            string text,
            string assetName = "Reanim")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "El contenido está vacío."
                );

                return null;
            }

            try
            {
                PvZReanimDefinition definition =
                    PvZReanimParser.Parse(
                        text
                    );

                if (definition == null)
                {
                    return null;
                }

                definition.name =
                    string.IsNullOrWhiteSpace(
                        assetName
                    )
                        ? "Reanim"
                        : assetName;

                return definition;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "Error procesando Reanim.\n\n" +
                    exception
                );

                return null;
            }
        }

        public static PvZReanimDefinition LoadTextAsset(
            TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "TextAsset nulo."
                );

                return null;
            }

            return LoadReanimText(
                asset.text,
                asset.name
            );
        }

        public static PvZReanimDefinition LoadResource(
            string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(
                    resourcePath))
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "resourcePath está vacío."
                );

                return null;
            }

            TextAsset asset =
                Resources.Load<TextAsset>(
                    resourcePath
                );

            if (asset == null)
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "No se encontró el recurso:\n" +
                    resourcePath
                );

                return null;
            }

            return LoadTextAsset(
                asset
            );
        }

        public static PvZReanimDefinition LoadStreamingAsset(
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(
                    relativePath))
            {
                Debug.LogError(
                    "PvZReanimAssetLoader: " +
                    "relativePath está vacío."
                );

                return null;
            }

            string path =
                Path.Combine(
                    Application.streamingAssetsPath,
                    relativePath
                );

            return LoadReanim(
                path
            );
        }

        public static bool IsValidDefinition(
            PvZReanimDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.TrackCount <= 0)
            {
                return false;
            }

            if (definition.GetMaxFrameCount() <= 0)
            {
                return false;
            }

            if (definition.fps <= 0f)
            {
                return false;
            }

            return true;
        }

        public static void LogDefinitionInfo(
            PvZReanimDefinition definition)
        {
            if (definition == null)
            {
                Debug.Log(
                    "PvZReanimAssetLoader: " +
                    "Definition = null"
                );

                return;
            }

            Debug.Log(
                "========== PvZ REANIM ==========\n" +
                "Nombre: " +
                definition.name +
                "\nFPS: " +
                definition.fps +
                "\nTracks: " +
                definition.TrackCount +
                "\nFrames: " +
                definition.GetMaxFrameCount() +
                "\n================================"
            );

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                PvZReanimTrack track =
                    definition.GetTrack(i);

                if (track == null)
                {
                    continue;
                }

                Debug.Log(
                    "Track [" +
                    i +
                    "]: " +
                    track.name +
                    " | Frames: " +
                    track.TransformCount
                );
            }
        }
    }
}