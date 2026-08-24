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

        /*
         * IMPORTANTE:
         *
         * El Recompiled recalcula el attachment durante
         * la actualización de la Reanimation.
         *
         * Nosotros hacemos exactamente lo mismo:
         *
         * 1. El cuerpo avanza.
         * 2. Se obtiene anim_stem.
         * 3. Se recalcula el overlay.
         * 4. Se actualiza la cabeza.
         */
        private void LateUpdate()
        {
            if (headAttachment == null)
                return;

            if (Body == null || Head == null)
                return;

            headAttachment.Refresh();
        }

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

        private void BuildHead()
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

        private void ConnectAttachment()
        {
            if (headAttachment == null)
                return;

            PvZReanimation body = Body;
            PvZReanimation head = Head;

            if (body == null)
            {
                Debug.LogWarning(
                    "[PvZReanimBodyHeadRig] Body no existe.",
                    this
                );

                return;
            }

            if (head == null)
            {
                Debug.LogWarning(
                    "[PvZReanimBodyHeadRig] Head no existe.",
                    this
                );

                return;
            }

            /*
             * Igual que:
             *
             * if (mFrameBasePose == -1)
             *     mFrameBasePose = mFrameStart;
             */
            body.SetFrameBasePose(
                body.FrameStart
            );

            headAttachment.SetTarget(
                head
            );

            headAttachment.SetSource(
                body,
                attachTrackName
            );

            /*
             * Primera actualización inmediata.
             */
            headAttachment.Refresh();
        }
    }
}