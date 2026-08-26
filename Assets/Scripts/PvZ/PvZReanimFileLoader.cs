using System;
using UnityEngine;

namespace PvZReanim
{
    // Antes tenía LoadFile/LoadStreamingAsset/LoadResource/
    // FileExists/StreamingAssetExists/LoadText para leer un
    // .reanim suelto de disco, Resources o StreamingAssets. Ya no
    // se usan: PvZReanimRuntimeLoader saca los bytes directo del
    // .pak y sólo necesita LoadBytes para detectar si es el
    // formato binario compilado real del juego o texto/XML y
    // rutearlo al parser correcto.
    public static class PvZReanimFileLoader
    {
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
    }
}
