using UnityEngine;

namespace PvZReanim
{
    /*
     * Replica el patr�n que usa el motor original para
     * plantas tipo lanzaguisantes (Plant.cpp, case
     * SEED_PEASHOOTER / SEED_REPEATER / SEED_GATLINGPEA /
     * SEED_SNOWPEA):
     *
     *   aBodyReanim->SetFramesForLayer("anim_idle");
     *   ...
     *   aHeadReanim->SetFramesForLayer("anim_head_idle");
     *   aHeadReanim->AttachToAnotherReanimation(
     *       aBodyReanim, "anim_stem");
     *
     * Es decir: DOS objetos Reanimation separados, no uno.
     * El body queda siempre en su propio loop (normalmente
     * "anim_idle") y el head cambia de sub-animaci�n seg�n
     * el estado del juego (idle, disparo, etc.) sin afectar
     * al body, seguido en posici�n por PvZReanimAttachment.
     *
     * Uso t�pico:
     *
     *   rig.PlayHeadAnim("anim_shooting", PvZReanimLoopType.Once);
     *   // ... el body sigue en anim_idle sin cortarse ...
     *   rig.PlayHeadAnim("anim_head_idle", PvZReanimLoopType.Loop);
     */
    public class PvZReanimBodyHeadRig : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath =
            "";

        [Header("Sub-animaciones")]
        [SerializeField]
        private string bodyAnimName =
            "anim_idle";

        [SerializeField]
        private string headAnimName =
            "anim_head_idle";

        /*
         * Track del body que el head sigue en posici�n.
         * En el original: "anim_stem" si existe, si no
         * "anim_idle" como fallback (Plant.cpp l�nea 213).
         */
        [SerializeField]
        private string attachTrackName =
            "anim_stem";

        [Header("Image System")]
        [SerializeField]
        private PvZReanimImageProvider imageProvider;

        [SerializeField]
        private PvZReanimImageResolver imageResolver;

        [Header("Playback")]
        [SerializeField]
        private PvZReanimLoopType bodyLoopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private PvZReanimLoopType headLoopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private float animRate = 1f;

        private PvZReanimRuntimeLoader bodyLoader;

        private PvZReanimRuntimeLoader headLoader;

        private PvZReanimAttachment headAttachment;

        public PvZReanimation Body =>
            bodyLoader != null
                ? bodyLoader.Reanimation
                : null;

        public PvZReanimation Head =>
            headLoader != null
                ? headLoader.Reanimation
                : null;

        private void Awake()
        {
            ResolveImageComponentsFallback();
            BuildBody();
            BuildHead();
        }

        /*
         * BuildBody()/BuildHead() llaman a loader.Load()
         * de forma S�NCRONA ac� en Awake() (no esperan al
         * Start() autom�tico del loader), as� que si
         * imageProvider/imageResolver no est�n asignados en
         * el Inspector del rig, hay que resolverlos ANTES,
         * si no la primera carga no va a poder resolver
         * ning�n sprite.
         */
        private void ResolveImageComponentsFallback()
        {
            if (imageProvider == null)
            {
                imageProvider =
                    FindFirstObjectByType<
                        PvZReanimImageProvider>();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    FindFirstObjectByType<
                        PvZReanimImageResolver>();
            }
        }

        // =========================================================
        // BUILD
        // =========================================================

        private void BuildBody()
        {
            GameObject bodyObj =
                new GameObject(
                    "Body"
                );

            bodyObj.transform.SetParent(
                transform,
                false
            );

            bodyLoader =
                bodyObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                bodyLoader,
                bodyAnimName,
                bodyLoopType
            );
        }

        private void BuildHead()
        {
            GameObject headAnchor =
                new GameObject(
                    "HeadAnchor"
                );

            headAnchor.transform.SetParent(
                transform,
                false
            );

            /*
             * El anchor sigue al track "anim_stem" del
             * body. El head cuelga de este anchor, as� que
             * se mueve autom�ticamente con el balanceo del
             * tallo sin que el head necesite saber nada del
             * body.
             */
            headAttachment =
                headAnchor.AddComponent<
                    PvZReanimAttachment>();

            GameObject headObj =
                new GameObject(
                    "Head"
                );

            headObj.transform.SetParent(
                headAnchor.transform,
                false
            );

            headLoader =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                headLoader,
                headAnimName,
                headLoopType
            );
        }

        private void ConfigureLoader(
            PvZReanimRuntimeLoader loader,
            string animName,
            PvZReanimLoopType loop)
        {
            loader.SetReanimPath(
                relativePath,
                false
            );

            loader.SetDefaultAnimName(
                animName
            );

            loader.SetImageComponents(
                imageProvider,
                imageResolver
            );

            loader.SetPlaybackDefaults(
                loop,
                animRate
            );

            loader.Load();
        }

        private void Start()
        {
            /*
             * El attachment necesita al body ya cargado
             * (con su PvZReanimation creada) antes de poder
             * buscar el track "anim_stem" -> se conecta en
             * Start(), despu�s de que Awake() de ambos
             * loaders ya corri� Load().
             */
            if (headAttachment != null &&
                Body != null)
            {
                headAttachment.SetSource(
                    Body,
                    attachTrackName
                );
            }
        }

        // =========================================================
        // API P�BLICA
        // =========================================================

        public void SetReanimPath(
            string newRelativePath)
        {
            relativePath =
                newRelativePath;
        }

        public void PlayHeadAnim(
            string animName,
            PvZReanimLoopType loop =
                PvZReanimLoopType.Loop,
            int blendTime = 0,
            float rate = -1f)
        {
            if (Head == null)
                return;

            Head.PlayReanim(
                animName,
                loop,
                blendTime,
                rate >= 0f
                    ? rate
                    : animRate
            );
        }

        public void PlayBodyAnim(
            string animName,
            PvZReanimLoopType loop =
                PvZReanimLoopType.Loop,
            int blendTime = 0,
            float rate = -1f)
        {
            if (Body == null)
                return;

            Body.PlayReanim(
                animName,
                loop,
                blendTime,
                rate >= 0f
                    ? rate
                    : animRate
            );
        }
    }
}