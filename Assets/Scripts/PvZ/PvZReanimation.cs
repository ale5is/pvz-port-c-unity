using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimation : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField]
        private PvZReanimDefinition definition;

        [Header("Image Resolver")]
        [SerializeField]
        private PvZReanimImageResolver imageResolver;

        [Header("Playback")]
        [SerializeField]
        private float animTime;

        [SerializeField]
        private float animRate = 1f;

        [SerializeField]
        private PvZReanimLoopType loopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private int frameStart;

        [SerializeField]
        private int frameCount = -1;

        private int loopCount;
        private bool dead;

        private PvZReanimTrackInstance[] trackInstances;
        private PvZReanimTrackRenderer[] trackRenderers;

        private PvZReanimTransform[] lastValidTransforms;

        private PvZReanimFrameTime cachedFrameTime;
        private bool frameTimeDirty = true;

        private int frameBasePose = -1;

        private PvZReanimMatrix overlayMatrix =
            PvZReanimMatrix.Identity;

        // =========================================================
        // SORTING
        // =========================================================

        /*
         * En el Reanim original, un attachment (por ejemplo la
         * cabeza sobre el cuerpo) NO se dibuja como un bloque
         * aparte: se intercala exactamente en el punto del loop
         * donde se encuentra el track anfitrión (DrawRenderGroup,
         * Reanimator.cpp linea 860).
         *
         * Para reproducir eso con sortingOrder de Unity, cada
         * track no usa +1, sino saltos de
         * TRACK_SORTING_STEP. Asi queda "espacio" de sortingOrder
         * libre entre un track y el siguiente para poder insertar
         * ahi los tracks de una Reanimation adjunta
         * (ver PvZReanimAttachment).
         */
        private const int TRACK_SORTING_STEP = 1000;

        [SerializeField]
        private int sortingLayerId;

        private int sortingOrderBase;

        public int SortingOrderBase =>
            sortingOrderBase;

        public PvZReanimDefinition Definition =>
            definition;

        public PvZReanimImageResolver ImageResolver =>
            imageResolver;

        public float AnimTime =>
            animTime;

        public float AnimRate
        {
            get => animRate;

            set
            {
                animRate = value;
                frameTimeDirty = true;
            }
        }

        public bool IsDead =>
            dead;

        public int LoopCount =>
            loopCount;

        public int TrackCount =>
            definition != null
                ? definition.TrackCount
                : 0;

        public int FrameStart =>
            frameStart;

        public int FrameCount =>
            frameCount;

        public int FrameBasePose =>
            frameBasePose;

        public PvZReanimMatrix OverlayMatrix =>
            overlayMatrix;

        // =========================================================
        // ATTACHMENT OVERLAY
        // =========================================================

        /// <summary>
        /// Aplica directamente la matriz de attachment a esta
        /// Reanimation. Esto equivale a que el Reanim original
        /// reciba su mOverlayMatrix desde AttachReanim().
        /// </summary>
        public void SetOverlayMatrix(
            PvZReanimMatrix matrix)
        {
            overlayMatrix = matrix;
        }

        /// <summary>
        /// Quita el attachment y vuelve la overlay a identidad.
        /// </summary>
        public void ResetOverlayMatrix()
        {
            overlayMatrix =
                PvZReanimMatrix.Identity;
        }

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            FindImageResolver();
            Initialize();
        }

        private void Update()
        {
            if (dead)
                return;

            AdvanceTime(Time.deltaTime);
            UpdateTracks();
        }

        // =========================================================
        // INITIALIZATION
        // =========================================================

        public void Initialize(
            PvZReanimDefinition newDefinition)
        {
            definition = newDefinition;
            Initialize();
        }

        public void Initialize()
        {
            if (definition == null)
                return;

            FindImageResolver();

            DestroyTrackObjects();
            CreateTrackObjects();

            trackInstances =
                new PvZReanimTrackInstance[
                    definition.TrackCount
                ];

            lastValidTransforms =
                new PvZReanimTransform[
                    definition.TrackCount
                ];

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                trackInstances[i] =
                    new PvZReanimTrackInstance();

                trackInstances[i].renderGroup =
                    PvZReanimRenderGroup.Normal;

                trackInstances[i].trackColor =
                    Color.white;

                trackInstances[i].truncateDisappearingFrames =
                    false;

                lastValidTransforms[i] = null;
            }

            frameStart = 0;

            frameCount =
                Mathf.Max(
                    1,
                    definition.GetMaxFrameCount()
                );

            animTime = 0f;
            loopCount = 0;
            dead = false;

            frameBasePose = -1;

            overlayMatrix =
                PvZReanimMatrix.Identity;

            frameTimeDirty = true;

            UpdateTracks();
        }

        private void FindImageResolver()
        {
            if (imageResolver != null)
                return;

            imageResolver =
                GetComponent<PvZReanimImageResolver>();

            if (imageResolver == null)
            {
                imageResolver =
                    GetComponentInParent<
                        PvZReanimImageResolver
                    >();
            }

            if (imageResolver == null)
            {
                imageResolver =
                    FindFirstObjectByType<
                        PvZReanimImageResolver
                    >();
            }
        }

        private void DestroyTrackObjects()
        {
            if (trackRenderers == null)
                return;

            for (int i = 0;
                 i < trackRenderers.Length;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                GameObject obj =
                    trackRenderers[i].gameObject;

                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }

            trackRenderers = null;
        }

        private void CreateTrackObjects()
        {
            if (definition == null)
                return;

            trackRenderers =
                new PvZReanimTrackRenderer[
                    definition.TrackCount
                ];

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                PvZReanimTrack track =
                    definition.GetTrack(i);

                if (track == null)
                    continue;

                string trackName =
                    string.IsNullOrEmpty(track.name)
                        ? "Track_" + i
                        : track.name;

                GameObject child =
                    new GameObject(trackName);

                child.transform.SetParent(
                    transform,
                    false
                );

                PvZReanimTrackRenderer renderer =
                    child.AddComponent<
                        PvZReanimTrackRenderer
                    >();

                renderer.SetImageResolver(
                    imageResolver
                );

                renderer.SetSorting(
                    sortingLayerId,
                    sortingOrderBase +
                    i * TRACK_SORTING_STEP
                );

                trackRenderers[i] = renderer;
            }
        }

        // =========================================================
        // SORTING (PUBLIC)
        // =========================================================

        /// <summary>
        /// Cambia el sortingLayer y/o el "piso" de sortingOrder de
        /// TODOS los tracks de esta Reanimation, manteniendo el
        /// orden relativo entre ellos (track i sigue yendo
        /// delante del track i-1).
        ///
        /// Lo usa PvZReanimAttachment para ubicar una Reanimation
        /// adjunta (ej: la cabeza) exactamente en el hueco de
        /// sortingOrder que le corresponde segun el track del
        /// padre al que esta pegada, replicando el intercalado
        /// del DrawRenderGroup original.
        /// </summary>
        public void SetSortingOrderBase(
            int newSortingOrderBase,
            int newSortingLayerId = -1)
        {
            if (newSortingLayerId >= 0)
                sortingLayerId = newSortingLayerId;

            sortingOrderBase =
                newSortingOrderBase;

            if (trackRenderers == null)
                return;

            for (int i = 0;
                 i < trackRenderers.Length;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                trackRenderers[i].SetSorting(
                    sortingLayerId,
                    sortingOrderBase +
                    i * TRACK_SORTING_STEP
                );
            }
        }

        /// <summary>
        /// sortingOrder que le corresponde al track "trackIndex"
        /// de esta Reanimation. Sirve para calcular en que hueco
        /// hay que insertar una Reanimation adjunta.
        /// </summary>
        public int GetSortingOrderForTrack(
            int trackIndex)
        {
            return sortingOrderBase +
                   trackIndex * TRACK_SORTING_STEP;
        }

        // =========================================================
        // DEFINITION
        // =========================================================

        public void SetDefinition(
            PvZReanimDefinition newDefinition)
        {
            definition = newDefinition;
            Initialize();
        }

        public void SetImageResolver(
            PvZReanimImageResolver newResolver)
        {
            imageResolver = newResolver;

            if (trackRenderers == null)
                return;

            for (int i = 0;
                 i < trackRenderers.Length;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                trackRenderers[i].SetImageResolver(
                    imageResolver
                );
            }

            UpdateTracks();
        }

        // =========================================================
        // BASE POSE
        // =========================================================

        public void SetFrameBasePose(
            int frame)
        {
            if (definition == null)
            {
                frameBasePose = frame;
                return;
            }

            int maxFrame =
                Mathf.Max(
                    0,
                    definition.GetMaxFrameCount() - 1
                );

            frameBasePose =
                Mathf.Clamp(
                    frame,
                    0,
                    maxFrame
                );
        }

        public void ClearFrameBasePose()
        {
            frameBasePose = -1;
        }

        public PvZReanimMatrix GetTrackBasePoseMatrix(
            int trackIndex)
        {
            if (definition == null)
                return PvZReanimMatrix.Identity;

            if (trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return PvZReanimMatrix.Identity;
            }

            /*
             * Igual que el original:
             *
             * mFrameBasePose == -1
             *     ? mFrameStart
             *     : mFrameBasePose
             */
            int baseFrame =
                frameBasePose >= 0
                    ? frameBasePose
                    : frameStart;

            int maxFrame =
                Mathf.Max(
                    0,
                    definition.GetMaxFrameCount() - 1
                );

            baseFrame =
                Mathf.Clamp(
                    baseFrame,
                    0,
                    maxFrame
                );

            PvZReanimFrameTime baseTime =
                new PvZReanimFrameTime(
                    0f,
                    baseFrame,
                    Mathf.Min(
                        baseFrame + 1,
                        maxFrame
                    )
                );

            PvZReanimTransform baseTransform =
                GetTransformAtTime(
                    trackIndex,
                    baseTime
                );

            if (baseTransform == null)
                return PvZReanimMatrix.Identity;

            return PvZReanimMatrix.FromTransform(
                baseTransform
            );
        }

        // =========================================================
        // ATTACHMENT OVERLAY
        // =========================================================

        public PvZReanimMatrix
            GetAttachmentOverlayMatrix(
                int trackIndex)
        {
            if (definition == null)
                return PvZReanimMatrix.Identity;

            if (trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return PvZReanimMatrix.Identity;
            }

            /*
             * ORIGINAL PVZ:
             *
             * GetCurrentTransform()
             * MatrixFromTransform()
             *
             * aTransformMatrix *= mOverlayMatrix
             *
             * aBasePoseMatrix = GetTrackBasePoseMatrix()
             * aBasePoseMatrixInv = inverse(base)
             *
             * result =
             *     aTransformMatrix *
             *     aBasePoseMatrixInv
             *
             * ESTE ORDEN ES MUY IMPORTANTE.
             */

            PvZReanimTransform current =
                GetCurrentTransform(
                    trackIndex
                );

            if (current == null)
                return PvZReanimMatrix.Identity;

            PvZReanimMatrix currentMatrix =
                PvZReanimMatrix.FromTransform(
                    current
                );

            /*
             * Primero se aplica el overlay de la reanimation.
             */
            currentMatrix =
                PvZReanimMatrix.Multiply(
                    currentMatrix,
                    overlayMatrix
                );

            PvZReanimMatrix baseMatrix =
                GetTrackBasePoseMatrix(
                    trackIndex
                );

            PvZReanimMatrix inverseBase =
                InverseAffine(
                    baseMatrix
                );

            /*
             * CORRECTO:
             *
             * current * overlay * inverseBase
             *
             * NO:
             *
             * inverseBase * current
             */
            return PvZReanimMatrix.Multiply(
                currentMatrix,
                inverseBase
            );
        }

        private static PvZReanimMatrix
            InverseAffine(
                PvZReanimMatrix matrix)
        {
            float determinant =
                matrix.m00 * matrix.m11 -
                matrix.m01 * matrix.m10;

            if (Mathf.Abs(determinant) <
                0.000001f)
            {
                return PvZReanimMatrix.Identity;
            }

            float inv =
                1f / determinant;

            float i00 =
                matrix.m11 * inv;

            float i01 =
                -matrix.m01 * inv;

            float i10 =
                -matrix.m10 * inv;

            float i11 =
                matrix.m00 * inv;

            float i02 =
                -(
                    i00 * matrix.m02 +
                    i01 * matrix.m12
                );

            float i12 =
                -(
                    i10 * matrix.m02 +
                    i11 * matrix.m12
                );

            return new PvZReanimMatrix(
                i00,
                i01,
                i02,

                i10,
                i11,
                i12,

                0f,
                0f,
                1f
            );
        }

        // =========================================================
        // PLAY
        // =========================================================

        public void Play(
            PvZReanimLoopType newLoopType,
            float newAnimRate = 1f,
            int newFrameStart = 0,
            int newFrameCount = -1)
        {
            if (definition == null)
                return;

            loopType = newLoopType;
            animRate = newAnimRate;

            int maxFrames =
                Mathf.Max(
                    1,
                    definition.GetMaxFrameCount()
                );

            frameStart =
                Mathf.Clamp(
                    newFrameStart,
                    0,
                    maxFrames - 1
                );

            if (newFrameCount > 0)
            {
                frameCount =
                    Mathf.Min(
                        newFrameCount,
                        maxFrames - frameStart
                    );
            }
            else
            {
                frameCount =
                    maxFrames - frameStart;
            }

            frameCount =
                Mathf.Max(
                    1,
                    frameCount
                );

            animTime =
                animRate >= 0f
                    ? 0f
                    : 0.9999999f;

            loopCount = 0;
            dead = false;

            /*
             * Al comenzar una animación normal,
             * si no existe una base pose explícita,
             * la base vuelve a ser el frame inicial.
             */
            if (frameBasePose < 0)
                frameBasePose = frameStart;

            frameTimeDirty = true;

            UpdateTracks();
        }

        // =========================================================
        // PLAY REANIM
        // =========================================================

        public void PlayReanim(
            string trackName,
            PvZReanimLoopType newLoopType,
            int blendTime,
            float newAnimRate)
        {
            if (definition == null)
                return;

            if (string.IsNullOrWhiteSpace(trackName))
            {
                Play(
                    newLoopType,
                    newAnimRate
                );

                return;
            }

            if (blendTime > 0)
                StartBlend(blendTime);

            if (!Mathf.Approximately(
                    newAnimRate,
                    0f))
            {
                animRate = newAnimRate;
            }

            loopType = newLoopType;

            int newFrameStart;
            int newFrameCount;

            if (!GetFramesForLayer(
                    trackName,
                    out newFrameStart,
                    out newFrameCount))
            {
                Debug.LogWarning(
                    "[PvZReanim] No se encontró el rango de animación: " +
                    trackName,
                    this
                );

                newFrameStart = 0;

                newFrameCount =
                    definition.GetMaxFrameCount();
            }

            frameStart =
                Mathf.Max(
                    0,
                    newFrameStart
                );

            frameCount =
                Mathf.Max(
                    1,
                    newFrameCount
                );

            /*
             * ESTE ES IMPORTANTE.
             *
             * El original, al adjuntar una reanimation,
             * usa mFrameStart como base si todavía no existe
             * mFrameBasePose.
             */
            if (frameBasePose < 0)
                frameBasePose = frameStart;

            animTime =
                animRate >= 0f
                    ? 0f
                    : 0.9999999f;

            loopCount = 0;
            dead = false;
            frameTimeDirty = true;

            UpdateTracks();
        }

        // =========================================================
        // FRAMES FOR LAYER
        // =========================================================

        public bool GetFramesForLayer(
            string animationName,
            out int resultFrameStart,
            out int resultFrameCount)
        {
            resultFrameStart = 0;
            resultFrameCount = 0;

            if (definition == null ||
                string.IsNullOrWhiteSpace(animationName))
            {
                return false;
            }

            string wanted =
                animationName.Trim();

            PvZReanimTrack animationTrack =
                definition.GetTrack(wanted);

            if (animationTrack == null)
            {
                int index =
                    definition.FindTrackIndex(
                        wanted
                    );

                if (index >= 0)
                    animationTrack =
                        definition.GetTrack(index);
            }

            if (animationTrack == null)
            {
                for (int i = 0;
                     i < definition.TrackCount;
                     i++)
                {
                    PvZReanimTrack track =
                        definition.GetTrack(i);

                    if (track == null)
                        continue;

                    if (string.Equals(
                            track.name,
                            wanted,
                            System.StringComparison
                                .OrdinalIgnoreCase))
                    {
                        animationTrack = track;
                        break;
                    }
                }
            }

            if (animationTrack == null ||
                animationTrack.transforms == null ||
                animationTrack.transforms.Count == 0)
            {
                return false;
            }

            /*
             * Igual que PvZ original:
             *
             * frameStart = primer transform cuyo frame >= 0
             *
             * frameCount =
             * desde ese índice hasta el último transform
             * cuyo frame >= 0
             */
            int first = -1;
            int last = -1;

            for (int i = 0;
                 i < animationTrack.TransformCount;
                 i++)
            {
                PvZReanimTransform t =
                    animationTrack.transforms[i];

                if (t == null ||
                    !t.HasFrame ||
                    t.GetFrame() < 0f)
                {
                    continue;
                }

                if (first < 0)
                    first = i;

                last = i;
            }

            if (first < 0)
            {
                resultFrameStart = 0;

                resultFrameCount =
                    Mathf.Max(
                        1,
                        definition.GetMaxFrameCount()
                    );

                return true;
            }

            resultFrameStart = first;

            resultFrameCount =
                Mathf.Max(
                    1,
                    last - first + 1
                );

            return true;
        }

        // =========================================================
        // TIME
        // =========================================================

        private void AdvanceTime(
            float deltaTime)
        {
            if (definition == null ||
                frameCount <= 0 ||
                Mathf.Approximately(
                    animRate,
                    0f))
            {
                return;
            }

            float fps =
                definition.fps;

            if (fps <= 0f)
                fps = 12f;

            /*
             * PvZ usa:
             *
             * animPosition =
             * frameStart +
             * animTime * frameCount
             *
             * excepto los loops normales,
             * donde la última frame se usa como límite.
             */
            float frameSpan;

            switch (loopType)
            {
                case PvZReanimLoopType.Once:
                    frameSpan =
                        Mathf.Max(
                            1,
                            frameCount - 1
                        );
                    break;

                case PvZReanimLoopType.Loop:
                    frameSpan =
                        Mathf.Max(
                            1,
                            frameCount - 1
                        );
                    break;

                case PvZReanimLoopType.PingPong:
                    frameSpan =
                        Mathf.Max(
                            1,
                            frameCount - 1
                        );
                    break;

                default:
                    frameSpan =
                        Mathf.Max(
                            1,
                            frameCount - 1
                        );
                    break;
            }

            float deltaFrames =
                deltaTime *
                fps *
                Mathf.Abs(animRate);

            float normalizedDelta =
                deltaFrames /
                frameSpan;

            if (animRate >= 0f)
                animTime += normalizedDelta;
            else
                animTime -= normalizedDelta;

            switch (loopType)
            {
                case PvZReanimLoopType.Loop:

                    while (animTime >= 1f)
                    {
                        animTime -= 1f;
                        loopCount++;
                    }

                    while (animTime < 0f)
                    {
                        animTime += 1f;
                        loopCount++;
                    }

                    break;

                case PvZReanimLoopType.PingPong:

                    animTime =
                        Mathf.PingPong(
                            animTime,
                            1f
                        );

                    break;

                case PvZReanimLoopType.Once:

                    if (animRate >= 0f)
                    {
                        if (animTime >= 1f)
                        {
                            animTime = 1f;
                            loopCount = 1;
                            dead = true;
                        }
                    }
                    else
                    {
                        if (animTime <= 0f)
                        {
                            animTime = 0f;
                            loopCount = 1;
                            dead = true;
                        }
                    }

                    break;
            }

            frameTimeDirty = true;
        }

        // =========================================================
        // FRAME TIME
        // =========================================================

        public PvZReanimFrameTime GetFrameTime()
        {
            if (definition == null)
            {
                return new PvZReanimFrameTime(
                    0f,
                    0,
                    0
                );
            }

            if (!frameTimeDirty)
                return cachedFrameTime;

            int count =
                Mathf.Max(
                    1,
                    frameCount
                );

            int start =
                Mathf.Max(
                    0,
                    frameStart
                );

            int maxFrame =
                Mathf.Max(
                    0,
                    definition.GetMaxFrameCount() - 1
                );

            int last =
                Mathf.Min(
                    start + count - 1,
                    maxFrame
                );

            /*
             * PvZ original:
             *
             * animPosition =
             * frameStart +
             * animTime * (frameCount - 1)
             *
             * Para el último frame,
             * before y after son el mismo.
             */
            float frame =
                start +
                Mathf.Clamp01(animTime) *
                Mathf.Max(
                    0,
                    last - start
                );

            int before =
                Mathf.FloorToInt(frame);

            float fraction =
                frame - before;

            int after =
                before + 1;

            if (before >= last)
            {
                before = last;
                after = last;
                fraction = 0f;
            }
            else
            {
                after =
                    Mathf.Min(
                        after,
                        last
                    );
            }

            before =
                Mathf.Clamp(
                    before,
                    0,
                    maxFrame
                );

            after =
                Mathf.Clamp(
                    after,
                    0,
                    maxFrame
                );

            cachedFrameTime =
                new PvZReanimFrameTime(
                    fraction,
                    before,
                    after
                );

            frameTimeDirty = false;

            return cachedFrameTime;
        }

        // =========================================================
        // OVERLAY
        // =========================================================

        private PvZReanimTransform ApplyOverlayToTransform(
            PvZReanimTransform original)
        {
            if (original == null)
                return null;

            if (overlayMatrix.Equals(PvZReanimMatrix.Identity))
                return original;

            // En Reanim el attachment no mueve el GameObject padre.
            // La matriz se aplica a CADA track de la reanimation destino.
            //
            // transformDeLaPieza * overlayMatrix
            //
            // Esto es lo que faltaba: antes SetOverlayMatrix() guardaba
            // la matriz, pero UpdateTracks() nunca la aplicaba al render.
            PvZReanimMatrix pieceMatrix =
                PvZReanimMatrix.FromTransform(original);

            PvZReanimMatrix combined =
                PvZReanimMatrix.Multiply(
                    pieceMatrix,
                    overlayMatrix
                );

            PvZReanimTransform result =
                original.Clone();

            result.x = combined.m02;
            result.y = combined.m12;

            float scaleX =
                Mathf.Sqrt(
                    combined.m00 * combined.m00 +
                    combined.m10 * combined.m10
                );

            float scaleY =
                Mathf.Sqrt(
                    combined.m01 * combined.m01 +
                    combined.m11 * combined.m11
                );

            if (scaleX < 0.000001f)
                scaleX = 1f;

            if (scaleY < 0.000001f)
                scaleY = 1f;

            float angleX =
                Mathf.Atan2(
                    -combined.m10,
                    combined.m00
                ) * Mathf.Rad2Deg;

            float angleY =
                Mathf.Atan2(
                    combined.m01,
                    combined.m11
                ) * Mathf.Rad2Deg;

            result.scaleX = scaleX;
            result.scaleY = scaleY;
            result.skewX = -angleX;
            result.skewY = -angleY;

            return result;
        }

        // =========================================================
        // UPDATE TRACKS
        // =========================================================

        private void UpdateTracks()
        {
            if (definition == null ||
                trackInstances == null ||
                trackRenderers == null)
            {
                return;
            }

            PvZReanimFrameTime frameTime =
                GetFrameTime();

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                if (i >= trackInstances.Length ||
                    i >= trackRenderers.Length)
                {
                    continue;
                }

                PvZReanimTrackRenderer renderer =
                    trackRenderers[i];

                if (renderer == null)
                    continue;

                PvZReanimTrackInstance instance =
                    trackInstances[i];

                PvZReanimTransform current =
                    GetTransformAtTime(
                        i,
                        frameTime
                    );

                if (current == null)
                {
                    PvZReanimTransform previous =
                        GetLastValidTransform(i);

                    if (previous != null)
                    {
                        renderer.Apply(
                            previous,
                            instance
                        );
                    }

                    continue;
                }

                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    PvZReanimTransform previous =
                        GetLastValidTransform(i);

                    if (previous != null &&
                        !previous.HasFrame)
                    {
                        renderer.Apply(
                            previous,
                            instance
                        );
                    }
                    else
                    {
                        renderer.Apply(
                            current,
                            instance
                        );
                    }

                    continue;
                }

                PvZReanimTransform renderTransform =
                    current;

                /*
                 * Mantener el blending compatible con el
                 * comportamiento del Reanimator original.
                 */
                if (instance != null &&
                    instance.blendCounter > 0 &&
                    instance.blendTransform != null &&
                    instance.blendTime > 0)
                {
                    float factor =
                        (float)instance.blendCounter /
                        instance.blendTime;

                    factor =
                        Mathf.Clamp01(
                            factor
                        );

                    /*
                     * Original:
                     *
                     * BlendTransform(
                     *     current,
                     *     blendTransform,
                     *     counter / time
                     * )
                     */
                    renderTransform =
                        PvZReanimInterpolator.Interpolate(
                            current,
                            instance.blendTransform,
                            factor
                        );

                    instance.blendCounter--;

                    if (instance.blendCounter <= 0)
                    {
                        instance.blendCounter = 0;
                        instance.blendTime = 0;
                        instance.blendTransform = null;
                    }
                }

                // Aplicar el attachment DESPUÉS de obtener la animación
                // propia de esta pieza. Así la cabeza conserva su animación
                // y además sigue al anim_stem del cuerpo.
                renderTransform =
                    ApplyOverlayToTransform(renderTransform);

                if (lastValidTransforms != null &&
                    i < lastValidTransforms.Length &&
                    renderTransform != null)
                {
                    lastValidTransforms[i] =
                        renderTransform.Clone();
                }

                renderer.Apply(
                    renderTransform,
                    instance
                );
            }
        }

        // =========================================================
        // TRANSFORM
        // =========================================================

        public PvZReanimTransform GetTransformAtTime(
            int trackIndex,
            PvZReanimFrameTime frameTime)
        {
            if (definition == null ||
                trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return null;
            }

            PvZReanimTrack track =
                definition.GetTrack(trackIndex);

            if (track == null ||
                track.TransformCount <= 0)
            {
                return null;
            }

            /*
             * IMPORTANTE:
             *
             * Los índices de transform corresponden a los
             * frames reales del reanimation, igual que en PvZ.
             */
            int before =
                Mathf.Clamp(
                    frameTime.frameBefore,
                    0,
                    track.TransformCount - 1
                );

            int after =
                Mathf.Clamp(
                    frameTime.frameAfter,
                    0,
                    track.TransformCount - 1
                );

            PvZReanimTransform a =
                track.GetTransform(before);

            PvZReanimTransform b =
                track.GetTransform(after);

            if (a == null)
            {
                a =
                    FindPreviousValidTransform(
                        track,
                        before
                    );
            }

            if (b == null)
            {
                b =
                    FindPreviousValidTransform(
                        track,
                        after
                    );
            }

            if (a == null && b == null)
                return null;

            if (ReferenceEquals(a, b))
                return a;

            return PvZReanimInterpolator.Interpolate(
                a,
                b,
                frameTime.fraction
            );
        }

        private PvZReanimTransform
            GetLastValidTransform(
                int trackIndex)
        {
            if (lastValidTransforms == null ||
                trackIndex < 0 ||
                trackIndex >= lastValidTransforms.Length)
            {
                return null;
            }

            return lastValidTransforms[
                trackIndex
            ];
        }

        private PvZReanimTransform
            FindPreviousValidTransform(
                PvZReanimTrack track,
                int startIndex)
        {
            if (track == null ||
                track.transforms == null ||
                track.transforms.Count == 0)
            {
                return null;
            }

            int index =
                Mathf.Clamp(
                    startIndex,
                    0,
                    track.transforms.Count - 1
                );

            for (int i = index;
                 i >= 0;
                 i--)
            {
                PvZReanimTransform value =
                    track.transforms[i];

                if (value != null)
                    return value;
            }

            return null;
        }

        public PvZReanimTransform GetCurrentTransform(
            int trackIndex)
        {
            return GetTransformAtTime(
                trackIndex,
                GetFrameTime()
            );
        }

        // =========================================================
        // TRACK SEARCH
        // =========================================================

        public int FindTrackIndex(
            string trackName)
        {
            if (definition == null ||
                string.IsNullOrEmpty(trackName))
            {
                return -1;
            }

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                PvZReanimTrack track =
                    definition.GetTrack(i);

                if (track == null)
                    continue;

                if (string.Equals(
                        track.name,
                        trackName,
                        System.StringComparison
                            .OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetTrackIndex(
            string trackName)
        {
            return FindTrackIndex(trackName);
        }

        public bool TrackExists(
            string trackName)
        {
            return FindTrackIndex(trackName) >= 0;
        }

        // =========================================================
        // VELOCITY
        // =========================================================

        public float GetTrackVelocity(
            string trackName)
        {
            return GetTrackVelocity(
                GetTrackIndex(trackName)
            );
        }

        public float GetTrackVelocity(
            int trackIndex)
        {
            if (definition == null ||
                trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return 0f;
            }

            PvZReanimTrack track =
                definition.GetTrack(trackIndex);

            if (track == null ||
                track.TransformCount < 2)
            {
                return 0f;
            }

            PvZReanimFrameTime time =
                GetFrameTime();

            PvZReanimTransform a =
                track.GetTransform(
                    Mathf.Clamp(
                        time.frameBefore,
                        0,
                        track.TransformCount - 1
                    )
                );

            PvZReanimTransform b =
                track.GetTransform(
                    Mathf.Clamp(
                        time.frameAfter,
                        0,
                        track.TransformCount - 1
                    )
                );

            if (a == null || b == null)
                return 0f;

            float secondsPerFrame =
                definition.fps > 0f
                    ? 1f / definition.fps
                    : 1f / 12f;

            return
                (
                    b.GetX() -
                    a.GetX()
                ) *
                secondsPerFrame *
                animRate;
        }

        // =========================================================
        // BLENDING
        // =========================================================

        public void StartBlend(
            int blendTime)
        {
            if (trackInstances == null ||
                blendTime <= 0)
            {
                return;
            }

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                PvZReanimTrackInstance instance =
                    trackInstances[i];

                if (instance == null)
                    continue;

                PvZReanimTransform current =
                    GetCurrentTransform(i);

                if (current == null)
                    current =
                        GetLastValidTransform(i);

                if (current == null)
                    continue;

                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    current =
                        GetLastValidTransform(i);
                }

                if (current == null)
                    continue;

                instance.blendTransform =
                    current.Clone();

                /*
                 * El original utiliza directamente blendTime.
                 */
                instance.blendCounter =
                    Mathf.Max(
                        1,
                        blendTime
                    );

                instance.blendTime =
                    instance.blendCounter;

                instance.blendTransform.image = null;
                instance.blendTransform.fontName = null;
                instance.blendTransform.text = null;
            }

            frameTimeDirty = true;
        }

        // =========================================================
        // POSITION / SCALE
        // =========================================================

        public void SetPosition(
            float x,
            float y)
        {
            overlayMatrix.m02 = x;
            overlayMatrix.m12 = y;
        }

        public void OverrideScale(
            float x,
            float y)
        {
            overlayMatrix.m00 = x;
            overlayMatrix.m11 = y;
        }

        // =========================================================
        // RENDER GROUP
        // =========================================================

        public void ShowOnlyTrack(
            string trackName)
        {
            if (trackInstances == null)
                return;

            int target =
                FindTrackIndex(trackName);

            if (target < 0)
                return;

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                trackInstances[i].renderGroup =
                    i == target
                        ? PvZReanimRenderGroup.Normal
                        : PvZReanimRenderGroup.Hidden;
            }

            UpdateTracks();
        }

        public void AssignRenderGroupToTrack(
            string trackName,
            PvZReanimRenderGroup renderGroup)
        {
            if (trackInstances == null)
                return;

            int index =
                FindTrackIndex(trackName);

            if (index < 0)
                return;

            trackInstances[index].renderGroup =
                renderGroup;

            UpdateTracks();
        }

        public void AssignRenderGroupToPrefix(
            string prefix,
            PvZReanimRenderGroup renderGroup)
        {
            if (trackInstances == null ||
                definition == null ||
                string.IsNullOrEmpty(prefix))
            {
                return;
            }

            string lowerPrefix =
                prefix.ToLowerInvariant();

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                PvZReanimTrack track =
                    definition.GetTrack(i);

                if (track == null ||
                    string.IsNullOrEmpty(track.name))
                {
                    continue;
                }

                if (track.name
                    .ToLowerInvariant()
                    .StartsWith(lowerPrefix))
                {
                    trackInstances[i].renderGroup =
                        renderGroup;
                }
            }

            UpdateTracks();
        }

        public bool IsTrackShowing(
            string trackName)
        {
            int index =
                FindTrackIndex(trackName);

            if (index < 0 ||
                trackInstances == null ||
                index >= trackInstances.Length)
            {
                return false;
            }

            if (trackInstances[index].renderGroup ==
                PvZReanimRenderGroup.Hidden)
            {
                return false;
            }

            PvZReanimTransform current =
                GetCurrentTransform(index);

            if (current == null)
                current =
                    GetLastValidTransform(index);

            if (current == null)
                return false;

            if (current.HasFrame &&
                current.GetFrame() < 0f)
            {
                return false;
            }

            return true;
        }

        // =========================================================
        // TRUNCATE
        // =========================================================

        public void SetTruncateDisappearingFrames(
            string trackName,
            bool value)
        {
            if (trackInstances == null)
                return;

            if (string.IsNullOrEmpty(trackName))
            {
                for (int i = 0;
                     i < trackInstances.Length;
                     i++)
                {
                    trackInstances[i]
                        .truncateDisappearingFrames =
                        value;
                }

                return;
            }

            int index =
                FindTrackIndex(trackName);

            if (index < 0)
                return;

            trackInstances[index]
                .truncateDisappearingFrames =
                value;
        }

        // =========================================================
        // IMAGE
        // =========================================================

        public void SetImageOverride(
            string trackName,
            Sprite sprite)
        {
            if (trackInstances == null)
                return;

            int index =
                FindTrackIndex(trackName);

            if (index < 0)
                return;

            trackInstances[index].imageOverride =
                sprite;

            UpdateTracks();
        }

        public Sprite GetImageOverride(
            string trackName)
        {
            if (trackInstances == null)
                return null;

            int index =
                FindTrackIndex(trackName);

            if (index < 0)
                return null;

            return
                trackInstances[index]
                    .imageOverride;
        }

        public Sprite GetCurrentTrackImage(
            string trackName)
        {
            int index =
                FindTrackIndex(trackName);

            if (index < 0)
                return null;

            PvZReanimTransform transform =
                GetCurrentTransform(index);

            if (transform == null)
                transform =
                    GetLastValidTransform(index);

            if (transform == null)
                return null;

            if (transform.HasFrame &&
                transform.GetFrame() < 0f)
            {
                return null;
            }

            if (trackInstances != null &&
                index < trackInstances.Length &&
                trackInstances[index].imageOverride != null)
            {
                return
                    trackInstances[index]
                        .imageOverride;
            }

            if (transform.image != null)
                return transform.image;

            if (imageResolver == null)
                FindImageResolver();

            if (imageResolver == null ||
                string.IsNullOrEmpty(
                    transform.imageName))
            {
                return null;
            }

            return imageResolver.Resolve(
                transform.imageName
            );
        }

        // =========================================================
        // RESET
        // =========================================================

        public void ResetReanimation()
        {
            animTime = 0f;
            loopCount = 0;
            dead = false;

            frameStart = 0;

            frameCount =
                definition != null
                    ? Mathf.Max(
                        1,
                        definition.GetMaxFrameCount()
                    )
                    : 0;

            frameBasePose = -1;

            overlayMatrix =
                PvZReanimMatrix.Identity;

            frameTimeDirty = true;

            if (trackInstances != null)
            {
                for (int i = 0;
                     i < trackInstances.Length;
                     i++)
                {
                    if (trackInstances[i] == null)
                        continue;

                    trackInstances[i].blendCounter = 0;
                    trackInstances[i].blendTime = 0;
                    trackInstances[i].blendTransform = null;
                }
            }

            if (lastValidTransforms != null)
            {
                for (int i = 0;
                     i < lastValidTransforms.Length;
                     i++)
                {
                    lastValidTransforms[i] = null;
                }
            }

            UpdateTracks();
        }

        // =========================================================
        // DIE
        // =========================================================

        public void Stop()
        {
            Die();
        }

        public void ReanimationDie()
        {
            Die();
        }

        public void Die()
        {
            dead = true;

            if (trackRenderers == null)
                return;

            for (int i = 0;
                 i < trackRenderers.Length;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                trackRenderers[i].ResetRenderer();
            }
        }
    }
}