using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimFileTest : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath =
            "PvZ/Reanim/test.reanim";

        [Header("Playback")]
        [SerializeField]
        private PvZReanimLoopType loopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private float animRate = 1f;

        private PvZReanimation reanimation;

        private void Start()
        {
            PvZReanimDefinition definition =
                PvZReanimFileLoader.LoadStreamingAsset(
                    relativePath
                );

            if (definition == null)
            {
                Debug.LogError(
                    "PvZReanimFileTest: " +
                    "No se pudo cargar el .reanim."
                );

                return;
            }

            GameObject obj =
                new GameObject(
                    "Reanim_From_File"
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

            Debug.Log(
                "PvZReanimFileTest: " +
                "Reanim cargado correctamente. " +
                "Tracks: " +
                definition.TrackCount +
                " | FPS: " +
                definition.fps +
                " | Frames: " +
                definition.GetMaxFrameCount()
            );
        }
    }
}