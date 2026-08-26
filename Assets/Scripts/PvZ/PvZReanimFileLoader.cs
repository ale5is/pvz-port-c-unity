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
                    "La ruta del archivo est� vac�a."
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

                return LoadBytes(
                    File.ReadAllBytes(path)
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
                    "Los datos est�n vac�os."
                );

                return null;
            }

            try
            {
                // Los .reanim reales del juego (los que están en
                // assets/compiled/reanim en el recomp) son binarios
                // comprimidos, no XML. Antes esto se mandaba
                // siempre a PvZReanimParser (texto) y con un
                // archivo real fallaba en silencio. Ahora se
                // detecta el formato por su cookie y se rutea.
                if (PvZReanimCompiledLoader.IsCompiledFormat(data))
                {
                    return PvZReanimCompiledLoader.LoadBytes(
                        data
                    );
                }

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
                    "El texto est� vac�o."
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
                    "La ruta relativa est� vac�a."
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
                    "La ruta de Resources est� vac�a."
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
                    "No se encontr� TextAsset:\n" +
                    resourcePath
                );

                return null;
            }

            // OJO: si el .reanim es un binario compilado (.bytes),
            // hay que importarlo como TextAsset igual, pero leer
            // .bytes en vez de .text -- .text rompe datos binarios.
            return LoadBytes(
                asset.bytes
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