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
                    PvZReanimRuntimeLoader>();

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


            body.SetFrameBasePose(
                body.FrameStart
            );

            head.SetFrameBasePose(
                head.FrameStart
            );


            headAttachment.SetTarget(
                head
            );

            headAttachment.SetSource(
                body,
                attachTrackName
            );

            connected = true;

            headAttachment.Refresh();
        }


        public void Reconnect()
        {
            connected = false;

            ConnectAttachment();
        }

        // ---------------------------------------------------------
        // Setters agregados para poder reusar UN mismo prefab de
        // rig con distintas plantas (spawneo por datos desde
        // PvZBoardPlantSpawner), en vez de necesitar un prefab
        // por planta con estos valores fijados en el Inspector.
        // ---------------------------------------------------------

        public void SetReanimPath(
            string newRelativePath)
        {
            relativePath = newRelativePath;

            if (bodyLoader != null)
                bodyLoader.SetReanimPath(relativePath, false);

            if (headLoader != null)
                headLoader.SetReanimPath(relativePath, false);
        }

        public void SetAttachTrackName(
            string newAttachTrackName)
        {
            attachTrackName = newAttachTrackName;
        }

        public void SetAnimNames(
            string newBodyAnimName,
            string newHeadAnimName)
        {
            bodyAnimName = newBodyAnimName;
            headAnimName = newHeadAnimName;

            if (bodyLoader != null)
                bodyLoader.SetDefaultAnimName(bodyAnimName);

            if (headLoader != null)
                headLoader.SetDefaultAnimName(headAnimName);
        }

        // Fuerza a que cuerpo y cabeza recarguen con el path/nombre
        // de animación actuales y vuelve a pegar la cabeza al track
        // del cuerpo. Llamar después de SetReanimPath/SetAnimNames.
        public void Rebuild()
        {
            if (bodyLoader != null)
                bodyLoader.ForceReload();

            if (headLoader != null)
                headLoader.ForceReload();

            Reconnect();
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