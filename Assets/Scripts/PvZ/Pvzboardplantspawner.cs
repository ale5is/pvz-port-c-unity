using UnityEngine;

namespace PvZReanim
{
    /*
     * Equivalente a Board::AddPlant + Plant::PlantInitialize.
     *
     * La regla importante es:
     *
     *   PEASHOOTER
     *   SNOWPEA
     *   REPEATER
     *   GATLINGPEA
     *       -> Body + 1 Head
     *
     *   SPLITPEA
     *       -> Body + 2 Heads
     *
     *   THREEPEATER
     *       -> Body + 3 Heads
     *
     *   TODAS LAS DEMÁS
     *       -> solamente Body
     *
     * Esto sigue Plant.cpp del recompilado.
     */

    public class PvZBoardPlantSpawner : MonoBehaviour
    {
        [Header("Grilla del tablero")]
        [SerializeField]
        private Vector2 originPixel =
            new Vector2(0f, 0f);

        [SerializeField]
        private Vector2 cellSize =
            new Vector2(80f, 80f);

        [Header("Prefabs")]
        [SerializeField]
        private PvZReanimRuntimeLoader singleReanimPrefab;

        [SerializeField]
        private PvZReanimBodyHeadRig bodyHeadRigPrefab;

        [SerializeField]
        private PvZReanimImageProvider imageProvider;

        [SerializeField]
        private PvZReanimImageResolver imageResolver;

        // =========================================================
        // TIPO DE RIG
        // =========================================================

        private enum PlantRigType
        {
            Single,
            OneHead,
            TwoHeads,
            ThreeHeads
        }

        private static PlantRigType GetRigType(
            string seedName)
        {
            switch (
                seedName
                    .Trim()
                    .ToUpperInvariant()
            )
            {
                case "PEASHOOTER":
                case "SNOWPEA":
                case "REPEATER":
                case "GATLINGPEA":
                    return PlantRigType.OneHead;

                case "SPLITPEA":
                    return PlantRigType.TwoHeads;

                case "THREEPEATER":
                    return PlantRigType.ThreeHeads;

                default:
                    return PlantRigType.Single;
            }
        }

        // =========================================================
        // POSICIÓN
        // =========================================================

        public Vector2 GridToWorld(
            int gridX,
            int gridY)
        {
            return new Vector2(
                originPixel.x +
                    gridX * cellSize.x,

                originPixel.y -
                    gridY * cellSize.y
            );
        }

        public int CalcRenderOrder(
            int gridY)
        {
            const int ROW_STEP = 1000;

            return gridY * ROW_STEP;
        }

        // =========================================================
        // CREAR PLANTA
        // =========================================================

        public GameObject PlantAt(
            string seedName,
            int gridX,
            int gridY)
        {
            string reanimPath =
                PvZPlantReanimTable.GetReanimPath(
                    seedName
                );

            if (string.IsNullOrEmpty(reanimPath))
            {
                Debug.LogError(
                    "[PvZBoardPlantSpawner] " +
                    "No hay .reanim registrado para '" +
                    seedName +
                    "'."
                );

                return null;
            }

            Vector2 worldPos =
                GridToWorld(
                    gridX,
                    gridY
                );

            int renderOrder =
                CalcRenderOrder(
                    gridY
                );

            PlantRigType rigType =
                GetRigType(
                    seedName
                );

            switch (rigType)
            {
                case PlantRigType.OneHead:
                    return SpawnOneHead(
                        seedName,
                        reanimPath,
                        worldPos,
                        renderOrder
                    );

                case PlantRigType.TwoHeads:
                    return SpawnTwoHeads(
                        seedName,
                        reanimPath,
                        worldPos,
                        renderOrder
                    );

                case PlantRigType.ThreeHeads:
                    return SpawnThreeHeads(
                        seedName,
                        reanimPath,
                        worldPos,
                        renderOrder
                    );

                default:
                    return SpawnSingle(
                        seedName,
                        reanimPath,
                        worldPos,
                        renderOrder
                    );
            }
        }

        // =========================================================
        // PLANTA NORMAL
        // =========================================================

        private GameObject SpawnSingle(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimRuntimeLoader instance =
                Instantiate(
                    singleReanimPrefab,
                    worldPos,
                    Quaternion.identity,
                    transform
                );

            instance.name =
                seedName;

            instance.SetImageComponents(
                imageProvider,
                imageResolver
            );

            instance.SetPlaybackDefaults(
                PvZReanimLoopType.Loop,
                15f
            );

            instance.SetReanimPath(
                reanimPath
            );

            if (instance.Reanimation != null)
            {
                instance.Reanimation
                    .SetSortingOrderBase(
                        renderOrder
                    );
            }

            return instance.gameObject;
        }

        // =========================================================
        // PEASHOOTER / SNOWPEA / REPEATER / GATLING
        // =========================================================

        private GameObject SpawnOneHead(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimBodyHeadRig rig =
                CreateRig(
                    seedName,
                    reanimPath,
                    worldPos,
                    renderOrder
                );

            rig.SetHeadCount(1);

            rig.SetHead1AnimName(
                "anim_head_idle"
            );

            rig.SetAttachTrackName(
                "anim_stem"
            );

            rig.Rebuild();

            rig.PlayBoth();

            return rig.gameObject;
        }

        // =========================================================
        // SPLIT PEA
        // =========================================================

        private GameObject SpawnTwoHeads(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimBodyHeadRig rig =
                CreateRig(
                    seedName,
                    reanimPath,
                    worldPos,
                    renderOrder
                );

            rig.SetHeadCount(2);

            /*
             * Plant.cpp:
             *
             * Head 1:
             *   anim_head_idle
             *   -> anim_idle
             *
             * Head 2:
             *   anim_splitpea_idle
             *   -> anim_idle
             */

            rig.SetHead1AnimName(
                "anim_head_idle"
            );

            rig.SetAttachTrackName(
                "anim_idle"
            );

            rig.SetHead2AnimName(
                "anim_splitpea_idle"
            );

            rig.SetHead2AttachTrackName(
                "anim_idle"
            );

            rig.Rebuild();

            rig.PlayBoth();

            return rig.gameObject;
        }

        // =========================================================
        // THREEPEATER
        // =========================================================

        private GameObject SpawnThreeHeads(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimBodyHeadRig rig =
                CreateRig(
                    seedName,
                    reanimPath,
                    worldPos,
                    renderOrder
                );

            rig.SetHeadCount(3);

            /*
             * Plant.cpp:
             *
             * Head 1:
             *   anim_head_idle1
             *   -> anim_head1
             *
             * Head 2:
             *   anim_head_idle2
             *   -> anim_head2
             *
             * Head 3:
             *   anim_head_idle3
             *   -> anim_head3
             */

            rig.SetHead1AnimName(
                "anim_head_idle1"
            );

            rig.SetAttachTrackName(
                "anim_head1"
            );

            rig.SetHead2AnimName(
                "anim_head_idle2"
            );

            rig.SetHead2AttachTrackName(
                "anim_head2"
            );

            rig.SetHead3AnimName(
                "anim_head_idle3"
            );

            rig.SetHead3AttachTrackName(
                "anim_head3"
            );

            rig.Rebuild();

            rig.PlayBoth();

            return rig.gameObject;
        }

        // =========================================================
        // CREAR RIG
        // =========================================================

        private PvZReanimBodyHeadRig CreateRig(
            string seedName,
            string reanimPath,
            Vector2 worldPos,
            int renderOrder)
        {
            PvZReanimBodyHeadRig rig =
                Instantiate(
                    bodyHeadRigPrefab,
                    worldPos,
                    Quaternion.identity,
                    transform
                );

            rig.name =
                seedName;

            rig.SetReanimPath(
                reanimPath
            );

            /*
             * Siempre usamos anim_idle para el cuerpo.
             */
            rig.SetAnimNames(
                "anim_idle",
                "anim_head_idle"
            );

            if (rig.Body != null)
            {
                rig.Body
                    .SetSortingOrderBase(
                        renderOrder
                    );
            }

            return rig;
        }
    }
}