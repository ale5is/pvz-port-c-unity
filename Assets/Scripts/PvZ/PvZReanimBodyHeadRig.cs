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

        private bool connected;

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

        private void LateUpdate()
        {
            if (!connected)
                return;

            PvZReanimation body = Body;
            PvZReanimation head = Head;

            if (body == null || head == null)
                return;

            if (headAttachment == null)
                return;

            /*
             * IMPORTANTE:
             *
             * Body y Head ya avanzaron durante Update().
             *
             * Aquí solamente actualizamos el attachment.
             *
             * No volvemos a reproducir animaciones.
             * No modificamos animTime.
             * No usamos anim_idle como pose de la cabeza.
             */
            headAttachment.Refresh();
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
                    PvZReanimRuntimeLoader>();

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

            headObj.transform.localPosition =
                Vector3.zero;

            headObj.transform.localRotation =
                Quaternion.identity;

            headObj.transform.localScale =
                Vector3.one;

            /*
             * El attachment vive en el mismo objeto
             * que contiene la reanimation de la cabeza.
             */
            headAttachment =
                headObj.AddComponent<
                    PvZReanimAttachment>();

            headLoader =
                headObj.AddComponent<
                    PvZReanimRuntimeLoader>();

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
        // ATTACHMENT
        // =========================================================

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
             * =====================================================
             * BODY
             * =====================================================
             *
             * El BODY utiliza exclusivamente anim_idle.
             *
             * Esta es la animación completa del cuerpo.
             */
            body.PlayReanim(
                bodyAnimName,
                bodyLoopType,
                0,
                animRate
            );

            /*
             * =====================================================
             * HEAD
             * =====================================================
             *
             * La HEAD utiliza exclusivamente anim_head_idle.
             *
             * NO se le asigna anim_idle.
             */
            head.PlayReanim(
                headAnimName,
                headLoopType,
                0,
                animRate
            );

            /*
             * =====================================================
             * BASE POSE
             * =====================================================
             *
             * La base pose de cada Reanimation es su propio
             * frame inicial.
             *
             * No utilizamos la animación idle del body como
             * base de la cabeza.
             */
            body.SetFrameBasePose(
                body.FrameStart
            );

            head.SetFrameBasePose(
                head.FrameStart
            );

            /*
             * =====================================================
             * ATTACHMENT
             * =====================================================
             *
             * HEAD = destino
             *
             * BODY = fuente
             *
             * anim_stem = track del BODY utilizado para obtener
             * la transformación del attachment.
             */
            headAttachment.SetTarget(
                head
            );

            headAttachment.SetSource(
                body,
                attachTrackName
            );

            connected = true;

            /*
             * Calcular inmediatamente la posición inicial.
             */
            headAttachment.Refresh();
        }

        // =========================================================
        // PUBLIC
        // =========================================================

        public void Reconnect()
        {
            connected = false;

            ConnectAttachment();
        }

        public void SetAnimationRate(
            float rate)
        {
            animRate = rate;

            PvZReanimation body = Body;
            PvZReanimation head = Head;

            if (body != null)
                body.AnimRate = rate;

            if (head != null)
                head.AnimRate = rate;
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
                headAnimName,
                headLoopType,
                0,
                animRate
            );
        }

        public void PlayBoth()
        {
            PvZReanimation body = Body;
            PvZReanimation head = Head;

            if (body == null || head == null)
                return;

            body.PlayReanim(
                bodyAnimName,
                bodyLoopType,
                0,
                animRate
            );

            head.PlayReanim(
                headAnimName,
                headLoopType,
                0,
                animRate
            );

            if (headAttachment != null)
                headAttachment.Refresh();
        }
    }
}