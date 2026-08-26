using UnityEngine;

namespace PvZReanim
{
    /*
     * Replica la creación de reanimaciones de cabeza que hace
     * Plant::PlantInitialize en el recompilado original.
     *
     * IMPORTANTE:
     *
     * La cabeza NO se obtiene aislando tracks que empiecen por
     * "anim_head".
     *
     * "anim_head_idle", "anim_splitpea_idle", etc. son capas
     * de animación. Son las que determinan el rango de frames
     * que debe reproducir la reanimación.
     *
     * El juego original hace:
     *
     *   AddReanimation(...)
     *   head->SetFramesForLayer("anim_head_idle")
     *   head->AttachToAnotherReanimation(body, "anim_stem")
     *
     * Por eso aquí hacemos exactamente lo mismo mediante
     * PlayReanim().
     *
     * NO ocultamos tracks manualmente.
     */

    public class PvZReanimBodyHeadRig : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath = "";

        [Header("Animación del cuerpo")]
        [SerializeField]
        private string bodyAnimName = "anim_idle";

        [Header("Cabeza 1")]
        [SerializeField]
        private bool useHead1 = true;

        [SerializeField]
        private string head1AnimName = "anim_head_idle";

        [SerializeField]
        private string head1AttachTrack = "anim_stem";

        [Header("Cabeza 2")]
        [SerializeField]
        private bool useHead2 = false;

        [SerializeField]
        private string head2AnimName = "anim_splitpea_idle";

        [SerializeField]
        private string head2AttachTrack = "anim_idle";

        [Header("Cabeza 3")]
        [SerializeField]
        private bool useHead3 = false;

        [SerializeField]
        private string head3AnimName = "anim_head_idle3";

        [SerializeField]
        private string head3AttachTrack = "anim_head3";

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
        private float animRate = 15f;

        private PvZReanimRuntimeLoader bodyLoader;

        private PvZReanimRuntimeLoader headLoader1;
        private PvZReanimRuntimeLoader headLoader2;
        private PvZReanimRuntimeLoader headLoader3;

        private PvZReanimAttachment headAttachment1;
        private PvZReanimAttachment headAttachment2;
        private PvZReanimAttachment headAttachment3;

        private bool connected;

        public PvZReanimation Body =>
            bodyLoader != null
                ? bodyLoader.Reanimation
                : null;

        public PvZReanimation Head =>
            headLoader1 != null
                ? headLoader1.Reanimation
                : null;

        public PvZReanimation Head2 =>
            headLoader2 != null
                ? headLoader2.Reanimation
                : null;

        public PvZReanimation Head3 =>
            headLoader3 != null
                ? headLoader3.Reanimation
                : null;

        public PvZReanimAttachment HeadAttachment =>
            headAttachment1;

        private void Awake()
        {
            ResolveImageComponentsFallback();

            BuildBody();

            if (useHead1)
                BuildHead1();

            if (useHead2)
                BuildHead2();

            if (useHead3)
                BuildHead3();
        }

        private void Start()
        {
            ConnectAttachments();
        }

        private void LateUpdate()
        {
            if (!connected)
                return;

            RefreshAttachments();
        }

        // =========================================================
        // IMAGE SYSTEM
        // =========================================================

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
        // BODY
        // =========================================================

        private void BuildBody()
        {
            GameObject bodyObj =
                new GameObject("Body");

            bodyObj.transform.SetParent(
                transform,
                false
            );

            bodyObj.transform.localPosition =
                Vector3.zero;

            bodyObj.transform.localRotation =
                Quaternion.identity;

            bodyObj.transform.localScale =
                Vector3.one;

            bodyLoader =
                bodyObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                bodyLoader,
                bodyAnimName,
                bodyLoopType
            );
        }

        // =========================================================
        // HEAD 1
        // =========================================================

        private void BuildHead1()
        {
            GameObject headObj =
                new GameObject("Head");

            headObj.transform.SetParent(
                transform,
                false
            );

            headObj.transform.localPosition =
                Vector3.zero;

            headObj.transform.localRotation =
                Quaternion.identity;

            headObj.transform.localScale =
                Vector3.one;

            headAttachment1 =
                headObj.AddComponent<
                    PvZReanimAttachment>();

            headLoader1 =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                headLoader1,
                head1AnimName,
                headLoopType
            );
        }

        // =========================================================
        // HEAD 2
        // =========================================================

        private void BuildHead2()
        {
            GameObject headObj =
                new GameObject("Head2");

            headObj.transform.SetParent(
                transform,
                false
            );

            headObj.transform.localPosition =
                Vector3.zero;

            headObj.transform.localRotation =
                Quaternion.identity;

            headObj.transform.localScale =
                Vector3.one;

            headAttachment2 =
                headObj.AddComponent<
                    PvZReanimAttachment>();

            headLoader2 =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                headLoader2,
                head2AnimName,
                headLoopType
            );
        }

        // =========================================================
        // HEAD 3
        // =========================================================

        private void BuildHead3()
        {
            GameObject headObj =
                new GameObject("Head3");

            headObj.transform.SetParent(
                transform,
                false
            );

            headObj.transform.localPosition =
                Vector3.zero;

            headObj.transform.localRotation =
                Quaternion.identity;

            headObj.transform.localScale =
                Vector3.one;

            headAttachment3 =
                headObj.AddComponent<
                    PvZReanimAttachment>();

            headLoader3 =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader>();

            ConfigureLoader(
                headLoader3,
                head3AnimName,
                headLoopType
            );
        }

        // =========================================================
        // LOADER
        // =========================================================

        private void ConfigureLoader(
            PvZReanimRuntimeLoader loader,
            string animName,
            PvZReanimLoopType loop)
        {
            if (loader == null)
                return;

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

        // =========================================================
        // ATTACHMENTS
        // =========================================================

        private void ConnectAttachments()
        {
            connected = false;

            PvZReanimation body = Body;

            if (body == null)
            {
                Debug.LogWarning(
                    "[PvZReanimBodyHeadRig] " +
                    "Body no existe.",
                    this
                );

                return;
            }

            /*
             * Igual que Plant.cpp:
             *
             * body -> anim_idle
             *
             * head -> anim_head_idle
             *
             * y cada cabeza se pega al track correspondiente.
             */

            body.PlayReanim(
                bodyAnimName,
                bodyLoopType,
                0,
                animRate
            );

            body.SetFrameBasePose(
                body.FrameStart
            );

            if (useHead1 &&
                headLoader1 != null &&
                headLoader1.Reanimation != null)
            {
                PvZReanimation head =
                    headLoader1.Reanimation;

                head.PlayReanim(
                    head1AnimName,
                    headLoopType,
                    0,
                    animRate
                );

                head.SetFrameBasePose(
                    head.FrameStart
                );

                headAttachment1.SetTarget(head);
                headAttachment1.SetSource(
                    body,
                    head1AttachTrack
                );
            }

            if (useHead2 &&
                headLoader2 != null &&
                headLoader2.Reanimation != null)
            {
                PvZReanimation head =
                    headLoader2.Reanimation;

                head.PlayReanim(
                    head2AnimName,
                    headLoopType,
                    0,
                    animRate
                );

                head.SetFrameBasePose(
                    head.FrameStart
                );

                headAttachment2.SetTarget(head);
                headAttachment2.SetSource(
                    body,
                    head2AttachTrack
                );
            }

            if (useHead3 &&
                headLoader3 != null &&
                headLoader3.Reanimation != null)
            {
                PvZReanimation head =
                    headLoader3.Reanimation;

                head.PlayReanim(
                    head3AnimName,
                    headLoopType,
                    0,
                    animRate
                );

                head.SetFrameBasePose(
                    head.FrameStart
                );

                headAttachment3.SetTarget(head);
                headAttachment3.SetSource(
                    body,
                    head3AttachTrack
                );
            }

            connected = true;

            RefreshAttachments();
        }

        private void RefreshAttachments()
        {
            if (headAttachment1 != null)
                headAttachment1.Refresh();

            if (headAttachment2 != null)
                headAttachment2.Refresh();

            if (headAttachment3 != null)
                headAttachment3.Refresh();
        }

        // =========================================================
        // PUBLIC CONFIGURATION
        // =========================================================

        public void SetReanimPath(
            string newRelativePath)
        {
            relativePath =
                newRelativePath;

            if (bodyLoader != null)
            {
                bodyLoader.SetReanimPath(
                    relativePath,
                    false
                );
            }

            if (headLoader1 != null)
            {
                headLoader1.SetReanimPath(
                    relativePath,
                    false
                );
            }

            if (headLoader2 != null)
            {
                headLoader2.SetReanimPath(
                    relativePath,
                    false
                );
            }

            if (headLoader3 != null)
            {
                headLoader3.SetReanimPath(
                    relativePath,
                    false
                );
            }
        }

        public void SetAttachTrackName(
            string newAttachTrackName)
        {
            head1AttachTrack =
                newAttachTrackName;
        }

        public void SetHead2AttachTrackName(
            string newAttachTrackName)
        {
            head2AttachTrack =
                newAttachTrackName;
        }

        public void SetHead3AttachTrackName(
            string newAttachTrackName)
        {
            head3AttachTrack =
                newAttachTrackName;
        }

        public void SetHeadCount(
            int count)
        {
            useHead1 = count >= 1;
            useHead2 = count >= 2;
            useHead3 = count >= 3;
        }

        public void SetHead1AnimName(
            string animationName)
        {
            head1AnimName =
                animationName;
        }

        public void SetHead2AnimName(
            string animationName)
        {
            head2AnimName =
                animationName;
        }

        public void SetHead3AnimName(
            string animationName)
        {
            head3AnimName =
                animationName;
        }

        /*
         * Compatibilidad con el código anterior.
         *
         * Ya NO se utiliza para ocultar tracks.
         */
        public void SetHeadTrackPrefix(
            string unusedPrefix)
        {
            // Intencionalmente vacío.
            //
            // El sistema anterior estaba equivocado:
            // "anim_head" no es un grupo de meshes.
        }

        public void SetAnimNames(
            string newBodyAnimName,
            string newHeadAnimName)
        {
            bodyAnimName =
                newBodyAnimName;

            head1AnimName =
                newHeadAnimName;

            if (bodyLoader != null)
            {
                bodyLoader.SetDefaultAnimName(
                    bodyAnimName
                );
            }

            if (headLoader1 != null)
            {
                headLoader1.SetDefaultAnimName(
                    head1AnimName
                );
            }
        }

        public void Rebuild()
        {
            connected = false;

            if (bodyLoader != null)
                bodyLoader.ForceReload();

            if (headLoader1 != null)
                headLoader1.ForceReload();

            if (headLoader2 != null)
                headLoader2.ForceReload();

            if (headLoader3 != null)
                headLoader3.ForceReload();

            ConnectAttachments();
        }

        public void SetAnimationRate(
            float rate)
        {
            animRate = rate;

            if (Body != null)
                Body.AnimRate = rate;

            if (Head != null)
                Head.AnimRate = rate;

            if (Head2 != null)
                Head2.AnimRate = rate;

            if (Head3 != null)
                Head3.AnimRate = rate;
        }

        public void PlayBody()
        {
            PvZReanimation body = Body;

            if (body == null)
                return;

            body.PlayReanim(
                bodyAnimName,
                bodyLoopType,
                0,
                animRate
            );
        }

        public void PlayHead()
        {
            PvZReanimation head = Head;

            if (head == null)
                return;

            head.PlayReanim(
                head1AnimName,
                headLoopType,
                0,
                animRate
            );

            if (headAttachment1 != null)
                headAttachment1.Refresh();
        }

        public void PlayBoth()
        {
            PlayBody();

            if (useHead1)
                PlayHead();

            if (useHead2 &&
                Head2 != null)
            {
                Head2.PlayReanim(
                    head2AnimName,
                    headLoopType,
                    0,
                    animRate
                );
            }

            if (useHead3 &&
                Head3 != null)
            {
                Head3.PlayReanim(
                    head3AnimName,
                    headLoopType,
                    0,
                    animRate
                );
            }

            RefreshAttachments();
        }

        public void Reconnect()
        {
            ConnectAttachments();
        }
    }
}