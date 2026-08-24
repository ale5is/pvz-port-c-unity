using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimBodyHeadRig : MonoBehaviour
    {
        [Header("Reanim")]
        [SerializeField]
        private string relativePath = "";

        [Header("Sub-animaciones")]
        [SerializeField]
        private string bodyAnimName = "anim_idle";

        [SerializeField]
        private string headAnimName = "anim_head_idle";

        [Header("Attachment")]
        [SerializeField]
        private string attachTrackName = "anim_stem";

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

        public PvZReanimAttachment HeadAttachment =>
            headAttachment;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            ResolveImageComponentsFallback();

            BuildBody();
            BuildHead();
        }

        private void Start()
        {
            ConnectAttachment();
        }

        // =========================================================
        // IMAGE
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
                    PvZReanimRuntimeLoader
                >();

            ConfigureLoader(
                bodyLoader,
                bodyAnimName,
                bodyLoopType
            );
        }

        // =========================================================
        // HEAD
        // =========================================================

        private void BuildHead()
        {
            GameObject headObj =
                new GameObject("Head");

            headObj.transform.SetParent(
                transform,
                false
            );

            /*
             * La cabeza empieza en identidad.
             * NO intentamos colocarla aquí usando anim_stem.
             * El attachment original de PvZ hace eso mediante
             * mOverlayMatrix sobre la Reanimation de la cabeza.
             */
            headObj.transform.localPosition =
                Vector3.zero;

            headObj.transform.localRotation =
                Quaternion.identity;

            headObj.transform.localScale =
                Vector3.one;

            headAttachment =
                headObj.AddComponent<
                    PvZReanimAttachment
                >();

            headLoader =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader
                >();

            ConfigureLoader(
                headLoader,
                headAnimName,
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
        // ATTACH
        // =========================================================

        private void ConnectAttachment()
        {
            if (headAttachment == null)
                return;

            PvZReanimation body =
                Body;

            PvZReanimation head =
                Head;

            if (body == null)
            {
                Debug.LogWarning(
                    "[PvZReanimBodyHeadRig] " +
                    "Body no existe.",
                    this
                );

                return;
            }

            if (head == null)
            {
                Debug.LogWarning(
                    "[PvZReanimBodyHeadRig] " +
                    "Head no existe.",
                    this
                );

                return;
            }

            /*
             * =====================================================
             * EQUIVALENTE REAL AL PVZ ORIGINAL:
             *
             * if (body->mFrameBasePose == -1)
             *     body->mFrameBasePose = body->mFrameStart;
             *
             * head->AttachToAnotherReanimation(
             *     body,
             *     "anim_stem"
             * );
             * =====================================================
             */
            body.SetFrameBasePose(
                body.FrameStart
            );

            /*
             * El Attachment obtiene la matriz:
             *
             * current(anim_stem) * inverse(base(anim_stem))
             *
             * y la coloca en:
             *
             * head.overlayMatrix
             *
             * NO mueve el GameObject Head.
             */
            headAttachment.SetTarget(head);

            headAttachment.SetSource(
                body,
                attachTrackName
            );

            /*
             * Aplicar inmediatamente la pose inicial para evitar
             * un frame donde la cabeza aparezca sin attachment.
             */
            headAttachment.Refresh();
        }
    }
}
