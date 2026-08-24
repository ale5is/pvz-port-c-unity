using UnityEngine;

namespace PvZReanim
{
    /*
     * Equivalente simplificado a
     * Reanimation::AttachToAnotherReanimation del motor
     * original (Reanimator.cpp:902).
     *
     * En PvZ real, una planta como el Lanzaguisantes NO es
     * un solo objeto Reanimation: son DOS (o m�s) objetos
     * separados, cada uno con su propio frameStart/frameCount,
     * reproduciendo sub-animaciones distintas al mismo
     * tiempo:
     *
     *   - "body" reproduce "anim_idle" en loop, siempre.
     *   - "head" reproduce lo que corresponda seg�n el
     *     estado (anim_head_idle, anim_shooting, etc.) y
     *     se posiciona siguiendo el track "anim_stem" del
     *     body (Plant.cpp l�nea ~208-214).
     *
     * Este componente hace ese seguimiento: cada frame, lee
     * la pose actual del track indicado en la Reanimation
     * "source" y la aplica como posici�n/rotaci�n/escala
     * local de este GameObject (que deber�a ser el padre/ra�z
     * de la Reanimation "head").
     *
     * OJO: esto es una aproximaci�n. El original compone una
     * matriz completa con skew (cizalladura) real
     * (MatrixFromTransform, Reanimator.cpp:561), que Unity no
     * soporta nativamente en un Transform normal. Ac�
     * aproximamos el skew con una rotaci�n en Z, que para el
     * balanceo lateral de un tallo da un resultado visualmente
     * muy cercano sin tener que armar una matriz de shear
     * manual por SpriteRenderer.
     */
    [DefaultExecutionOrder(100)]
    public class PvZReanimAttachment : MonoBehaviour
    {
        [Header("Fuente a seguir")]
        [SerializeField]
        private PvZReanimation source;

        [SerializeField]
        private string sourceTrackName =
            "anim_stem";

        [Header("Opciones")]
        [SerializeField]
        private bool followPosition = true;

        [SerializeField]
        private bool followRotation = true;

        [SerializeField]
        private bool followScale = false;

        private int cachedTrackIndex = -1;

        private string cachedTrackName;

        public void SetSource(
            PvZReanimation newSource,
            string newTrackName)
        {
            source =
                newSource;

            sourceTrackName =
                newTrackName;

            cachedTrackIndex = -1;
            cachedTrackName = null;
        }

        /*
         * LateUpdate: corre DESPU�S de que source.Update()
         * ya calcul� la pose de este frame (PvZReanimation
         * actualiza en Update()). As� siempre leemos la
         * posici�n ya al d�a, nunca la del frame anterior.
         */
        private void LateUpdate()
        {
            if (source == null)
                return;

            if (cachedTrackIndex < 0 ||
                cachedTrackName != sourceTrackName)
            {
                cachedTrackIndex =
                    source.FindTrackIndex(
                        sourceTrackName
                    );

                cachedTrackName =
                    sourceTrackName;

                if (cachedTrackIndex < 0)
                {
                    Debug.LogWarning(
                        "[PvZReanimAttachment] No se " +
                        "encontr� el track '" +
                        sourceTrackName +
                        "' en " +
                        source.name +
                        ". No se puede seguir su posici�n."
                    );
                }
            }

            if (cachedTrackIndex < 0)
                return;

            PvZReanimTransform current =
                source.GetCurrentTransform(
                    cachedTrackIndex
                );

            if (current == null)
                return;

            if (followPosition)
            {
                transform.localPosition =
                    new Vector3(
                        current.GetX(),
                        current.GetY(),
                        0f
                    );
            }

            if (followRotation)
            {
                /*
                 * Aproximaci�n del skew con rotaci�n en Z.
                 * skewY suele ser el que gobierna el
                 * balanceo lateral del tallo en los reanim
                 * de PvZ.
                 */
                transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        -current.GetSkewY()
                    );
            }

            if (followScale)
            {
                transform.localScale =
                    new Vector3(
                        current.GetScaleX(),
                        current.GetScaleY(),
                        1f
                    );
            }
        }
    }
}