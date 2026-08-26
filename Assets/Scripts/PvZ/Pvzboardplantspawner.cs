using UnityEngine;

namespace PvZReanim
{
    /*
     * Equivalente a Board::AddPlant + Plant::PlantInitialize
     * del ResoddedFramework (Board.cpp / Plant.cpp).
     *
     * El framework original, para plantar algo, hace 3 cosas:
     *   1) Convierte la celda de grilla a posición de pantalla
     *      (Board::GridToPixelX/Y).
     *   2) Calcula un orden de dibujado según la fila
     *      (Plant::CalcRenderOrder -> Board::MakeRenderOrder).
     *   3) Crea la reanimación del cuerpo con mApp->AddReanimation(...)
     *      y, si la planta tiene cabeza separada (asoleadora, doble
     *      girasol, etc.), crea una segunda reanimación y la pega
     *      a un track del cuerpo con AttachToAnotherReanimation
     *      (ej. "anim_stem", "anim_idle", "anim_head1"/"anim_head2").
     *
     * Este script hace lo mismo pero apoyándose en las piezas que
     * ya existen en el proyecto:
     *   - PvZPlantReanimTable   -> qué .reanim usa cada semilla
     *   - PvZReanimRuntimeLoader -> carga y reproduce UN reanim
     *   - PvZReanimBodyHeadRig  -> carga el MISMO reanim dos veces
     *                              (cuerpo + cabeza) y los pega,
     *                              igual que hace el juego original
     *   - PvZReanimAttachment   -> el "AttachToAnotherReanimation"
     *
     * No inventa un sistema nuevo: sólo arma en runtime lo que en
     * el original arma Plant::PlantInitialize.
     */
    public class PvZBoardPlantSpawner : MonoBehaviour
    {
        [Header("Grilla del tablero (ajustar a tu Board real)")]
        [SerializeField]
        private Vector2 originPixel = new Vector2(0f, 0f);

        [SerializeField]
        private Vector2 cellSize = new Vector2(80f, 80f);

        [Header("Prefabs base")]
        [SerializeField]
        private PvZReanimRuntimeLoader singleReanimPrefab;

        [SerializeField]
        private PvZReanimBodyHeadRig bodyHeadRigPrefab;

        [SerializeField]
        private PvZReanimImageProvider imageProvider;

        [SerializeField]
        private PvZReanimImageResolver imageResolver;

        // Plantas cuya cabeza/rostro es una segunda instancia del
        // mismo reanim pegada a un track del cuerpo (igual que
        // Plant::PlantInitialize en el original). El nombre del
        // track depende de la planta: la mayoría usa "anim_stem",
        // el girasol usa "anim_idle".
        private static bool NeedsHeadRig(string seedName, out string attachTrack)
        {
            switch (seedName.Trim().ToUpperInvariant())
            {
                case "PEASHOOTER":
                case "REPEATER":
                case "SNOWPEA":
                case "GATLINGPEA":
                case "SPLITPEA":
                case "FIREPEA":
                case "CATTAIL":
                    attachTrack = "anim_stem";
                    return true;

                case "SUNFLOWER":
                    attachTrack = "anim_idle";
                    return true;

                default:
                    attachTrack = null;
                    return false;
            }
        }

        // Equivalente a Board::GridToPixelX / GridToPixelY.
        // Reemplazar por las coordenadas reales de tu Board en
        // cuanto exista Scripts/Board con la grilla de verdad.
        public Vector2 GridToWorld(int gridX, int gridY)
        {
            return new Vector2(
                originPixel.x + gridX * cellSize.x,
                originPixel.y - gridY * cellSize.y
            );
        }

        // Equivalente a Plant::CalcRenderOrder: las filas de más
        // atrás (gridY menor) se dibujan primero.
        public int CalcRenderOrder(int gridY)
        {
            const int ROW_STEP = 1000;
            return gridY * ROW_STEP;
        }

        /*
         * Equivalente a Board::AddPlant(gridX, gridY, seedType, ...)
         * seguido de Plant::PlantInitialize.
         */
        public GameObject PlantAt(string seedName, int gridX, int gridY)
        {
            string reanimPath = PvZPlantReanimTable.GetReanimPath(seedName);
            if (string.IsNullOrEmpty(reanimPath))
            {
                Debug.LogError(
                    "[PvZBoardPlantSpawner] No hay .reanim registrado " +
                    "para la semilla '" + seedName + "' en PvZPlantReanimTable."
                );
                return null;
            }

            Vector2 worldPos = GridToWorld(gridX, gridY);
            int renderOrder = CalcRenderOrder(gridY);

            if (NeedsHeadRig(seedName, out string attachTrack))
            {
                return SpawnBodyHead(seedName, reanimPath, attachTrack, worldPos, renderOrder);
            }

            return SpawnSingle(seedName, reanimPath, worldPos, renderOrder);
        }

        private GameObject SpawnSingle(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimRuntimeLoader instance =
                Instantiate(singleReanimPrefab, worldPos, Quaternion.identity, transform);

            instance.name = seedName;
            instance.SetImageComponents(imageProvider, imageResolver, null);
            instance.SetPlaybackDefaults(PvZReanimLoopType.Loop, 1f);
            instance.SetReanimPath(reanimPath);

            if (instance.Reanimation != null)
            {
                instance.Reanimation.SetSortingOrderBase(renderOrder);
            }

            return instance.gameObject;
        }

        private GameObject SpawnBodyHead(
            string seedName,
            string reanimPath,
            string attachTrack,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimBodyHeadRig rig =
                Instantiate(bodyHeadRigPrefab, worldPos, Quaternion.identity, transform);

            rig.name = seedName;

            // El prefab trae valores por defecto puestos en el
            // Inspector; acá los pisamos con los que corresponden
            // a ESTA planta y forzamos recarga. Es el equivalente
            // en runtime a lo que Plant::PlantInitialize arma al
            // vuelo con AddReanimation + AttachToAnotherReanimation.
            rig.SetReanimPath(reanimPath);
            rig.SetAttachTrackName(attachTrack);
            rig.Rebuild();
            rig.PlayBoth();

            if (rig.Body != null)
            {
                rig.Body.SetSortingOrderBase(renderOrder);
            }

            return rig.gameObject;
        }
    }
}