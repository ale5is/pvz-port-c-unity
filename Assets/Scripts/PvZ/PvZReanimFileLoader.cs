using System;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimFileLoader
    {
        // =========================================================
        // LOAD FROM FILE
        // =========================================================

        public static PvZReanimDefinition LoadFile(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "La ruta del archivo está vacía."
                );

                return null;
            }

            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogError(
                        "PvZReanimFileLoader: " +
                        "No existe el archivo:\n" +
                        path
                    );

                    return null;
                }

                return PvZReanimParser.LoadFile(
                    path
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "Error cargando .reanim:\n" +
                    path +
                    "\n\n" +
                    exception
                );

                return null;
            }
        }

        // =========================================================
        // LOAD FROM BYTES
        // =========================================================

        public static PvZReanimDefinition LoadBytes(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "Los datos están vacíos."
                );

                return null;
            }

            try
            {
                return PvZReanimParser.LoadBytes(
                    data
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "Error procesando datos .reanim.\n\n" +
                    exception
                );

                return null;
            }
        }

        // =========================================================
        // LOAD FROM TEXT
        // =========================================================

        public static PvZReanimDefinition LoadText(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "El texto está vacío."
                );

                return null;
            }

            try
            {
                return PvZReanimParser.Parse(
                    text
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "Error procesando texto .reanim.\n\n" +
                    exception
                );

                return null;
            }
        }

        // =========================================================
        // STREAMING ASSETS
        // =========================================================

        public static PvZReanimDefinition LoadStreamingAsset(
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(
                    relativePath))
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "La ruta relativa está vacía."
                );

                return null;
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

            return LoadFile(
                path
            );
        }

        // =========================================================
        // RESOURCES
        // =========================================================

        public static PvZReanimDefinition LoadResource(
            string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(
                    resourcePath))
            {
                Debug.LogError(
                    "PvZReanimFileLoader: " +
                    "La ruta de Resources está vacía."
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
                    "PvZReanimFileLoader: " +
                    "No se encontró TextAsset:\n" +
                    resourcePath
                );

                return null;
            }

            return LoadText(
                asset.text
            );
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        public static bool FileExists(
            string path)
        {
            return
                !string.IsNullOrWhiteSpace(path) &&
                File.Exists(path);
        }

        public static bool StreamingAssetExists(
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(
                    relativePath))
            {
                return false;
            }

            string path =
                Path.Combine(
                    Application.streamingAssetsPath,
                    relativePath
                );

            return File.Exists(path);
        }
    }
}