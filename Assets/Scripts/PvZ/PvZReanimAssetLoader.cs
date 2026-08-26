using UnityEngine;

namespace PvZReanim
{
    // Antes tenía LoadReanim/LoadReanimText/LoadTextAsset/
    // LoadResource/LoadStreamingAsset para cargar un .reanim
    // suelto desde disco, Resources o StreamingAssets. Nada los
    // llamaba (el único camino real es PvZReanimRuntimeLoader
    // leyendo bytes desde main.pak), así que se borraron. Queda
    // sólo lo que PvZReanimRuntimeLoader sí usa: validar la
    // definición cargada y loguearla.
    public static class PvZReanimAssetLoader
    {
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
