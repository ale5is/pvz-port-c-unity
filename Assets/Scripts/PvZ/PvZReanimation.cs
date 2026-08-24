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
                GetComponent<
                    PvZReanimImageResolver
                >();

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
                    0,
                    i
                );

                trackRenderers[i] =
                    renderer;
            }
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
        // BASE POSE / ATTACHMENT
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
            if (definition == null ||
                trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return PvZReanimMatrix.Identity;
            }

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
                    baseFrame
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

        public PvZReanimMatrix
            GetAttachmentOverlayMatrix(
                int trackIndex)
        {
            if (definition == null ||
                trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return PvZReanimMatrix.Identity;
            }

            PvZReanimTransform current =
                GetCurrentTransform(trackIndex);

            if (current == null)
                return PvZReanimMatrix.Identity;

            PvZReanimMatrix currentMatrix =
                PvZReanimMatrix.FromTransform(
                    current
                );

            PvZReanimMatrix baseMatrix =
                GetTrackBasePoseMatrix(
                    trackIndex
                );

            PvZReanimMatrix inverseBase =
                InverseAffine(baseMatrix);

            PvZReanimMatrix result =
                PvZReanimMatrix.Multiply(
                    inverseBase,
                    currentMatrix
                );

            result =
                PvZReanimMatrix.Multiply(
                    result,
                    overlayMatrix
                );

            return result;
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
                Mathf.Clamp(
                    newFrameStart,
                    0,
                    Mathf.Max(
                        0,
                        definition.GetMaxFrameCount() - 1
                    )
                );

            frameCount =
                Mathf.Clamp(
                    newFrameCount,
                    1,
                    Mathf.Max(
                        1,
                        definition.GetMaxFrameCount() -
                        frameStart
                    )
                );

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
        // GET FRAMES FOR LAYER
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

            PvZReanimTrack animationTrack = null;

            int animationTrackIndex =
                definition.FindTrackIndex(wanted);

            if (animationTrackIndex >= 0)
            {
                animationTrack =
                    definition.GetTrack(
                        animationTrackIndex
                    );
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
             * IMPORTANTE:
             *
             * TransformCount NO necesariamente representa
             * frames globales consecutivos.
             *
             * Buscamos el frame REAL guardado dentro
             * de cada transform.
             */
            int firstFrame = int.MaxValue;
            int lastFrame = int.MinValue;

            for (int i = 0;
                 i < animationTrack.transforms.Count;
                 i++)
            {
                PvZReanimTransform t =
                    animationTrack.transforms[i];

                if (t == null ||
                    !t.HasFrame)
                {
                    continue;
                }

                float frameValue =
                    t.GetFrame();

                if (frameValue < 0f)
                    continue;

                int realFrame =
                    Mathf.RoundToInt(frameValue);

                if (realFrame < firstFrame)
                    firstFrame = realFrame;

                if (realFrame > lastFrame)
                    lastFrame = realFrame;
            }

            if (firstFrame == int.MaxValue)
            {
                resultFrameStart = 0;

                resultFrameCount =
                    Mathf.Max(
                        1,
                        definition.GetMaxFrameCount()
                    );

                return true;
            }

            resultFrameStart =
                Mathf.Max(
                    0,
                    firstFrame
                );

            resultFrameCount =
                Mathf.Max(
                    1,
                    lastFrame - firstFrame + 1
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
             * La duración se calcula con frameCount-1,
             * porque el último frame no necesita otra
             * unidad completa para llegar a él.
             */
            float duration =
                Mathf.Max(
                    1f,
                    frameCount - 1
                ) / fps;

            if (duration <= 0f)
                duration = 1f / fps;

            float deltaNormalized =
                deltaTime / duration;

            animTime +=
                deltaNormalized *
                Mathf.Abs(animRate) *
                Mathf.Sign(animRate);

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

            int maxFrame =
                Mathf.Max(
                    0,
                    definition.GetMaxFrameCount() - 1
                );

            int start =
                Mathf.Clamp(
                    frameStart,
                    0,
                    maxFrame
                );

            int count =
                Mathf.Max(
                    1,
                    frameCount
                );

            int last =
                Mathf.Clamp(
                    start + count - 1,
                    start,
                    maxFrame
                );

            float normalized =
                Mathf.Clamp01(animTime);

            float frame =
                Mathf.Lerp(
                    start,
                    last,
                    normalized
                );

            int before =
                Mathf.FloorToInt(frame);

            int after =
                before + 1;

            float fraction =
                frame - before;

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

                /*
                 * No hay transform:
                 * mantener la última pose.
                 */
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

                /*
                 * Frame negativo:
                 * no sustituimos la posición por una pose
                 * inválida/lejana.
                 */
                if (current.HasFrame &&
                    current.GetFrame() < 0f)
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

                // -------------------------------------------------
                // BLEND
                // -------------------------------------------------

                if (instance != null &&
                    instance.blendCounter > 0 &&
                    instance.blendTransform != null &&
                    instance.blendTime > 0)
                {
                    float factor =
                        1f -
                        (
                            (float)instance.blendCounter /
                            instance.blendTime
                        );

                    factor =
                        Mathf.Clamp01(
                            factor
                        );

                    renderTransform =
                        PvZReanimInterpolator.Interpolate(
                            instance.blendTransform,
                            current,
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

                /*
                 * Guardar siempre una copia.
                 */
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
             * frameBefore/frameAfter son FRAMES GLOBALES.
             * No son índices del array transforms.
             *
             * El error anterior hacía que, por ejemplo,
             * frame 20 accediera al transform [20] aunque
             * el transform [20] pudiera pertenecer a otro
             * frame completamente distinto.
             */

            PvZReanimTransform a =
                FindTransformAtOrBeforeFrame(
                    track,
                    frameTime.frameBefore
                );

            PvZReanimTransform b =
                FindTransformAtOrAfterFrame(
                    track,
                    frameTime.frameAfter
                );

            /*
             * Si no existe uno de los extremos,
             * usamos el otro sin inventar una posición.
             */
            if (a == null && b == null)
                return null;

            if (a == null)
                return b;

            if (b == null)
                return a;

            /*
             * Si uno de los transforms tiene frame negativo,
             * no lo interpolamos como posición válida.
             */
            if (a.HasFrame &&
                a.GetFrame() < 0f)
            {
                return b;
            }

            if (b.HasFrame &&
                b.GetFrame() < 0f)
            {
                return a;
            }

            if (ReferenceEquals(a, b))
                return a;

            /*
             * Interpolación únicamente entre los dos keyframes
             * correctos del track.
             */
            return PvZReanimInterpolator.Interpolate(
                a,
                b,
                frameTime.fraction
            );
        }

        private PvZReanimTransform
            FindTransformAtOrBeforeFrame(
                PvZReanimTrack track,
                int targetFrame)
        {
            if (track == null ||
                track.transforms == null ||
                track.transforms.Count == 0)
            {
                return null;
            }

            PvZReanimTransform result = null;

            float bestFrame =
                float.MinValue;

            for (int i = 0;
                 i < track.transforms.Count;
                 i++)
            {
                PvZReanimTransform t =
                    track.transforms[i];

                if (t == null ||
                    !t.HasFrame)
                {
                    continue;
                }

                float frame =
                    t.GetFrame();

                if (frame < 0f)
                    continue;

                if (frame <= targetFrame &&
                    frame >= bestFrame)
                {
                    bestFrame = frame;
                    result = t;
                }
            }

            return result;
        }

        private PvZReanimTransform
            FindTransformAtOrAfterFrame(
                PvZReanimTrack track,
                int targetFrame)
        {
            if (track == null ||
                track.transforms == null ||
                track.transforms.Count == 0)
            {
                return null;
            }

            PvZReanimTransform result = null;

            float bestFrame =
                float.MaxValue;

            for (int i = 0;
                 i < track.transforms.Count;
                 i++)
            {
                PvZReanimTransform t =
                    track.transforms[i];

                if (t == null ||
                    !t.HasFrame)
                {
                    continue;
                }

                float frame =
                    t.GetFrame();

                if (frame < 0f)
                    continue;

                if (frame >= targetFrame &&
                    frame <= bestFrame)
                {
                    bestFrame = frame;
                    result = t;
                }
            }

            return result;
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

            return lastValidTransforms[trackIndex];
        }

        /*
         * Se conserva por compatibilidad con el resto del proyecto.
         */
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

                if (value == null)
                    continue;

                if (value.HasFrame &&
                    value.GetFrame() < 0f)
                {
                    continue;
                }

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
                FindTransformAtOrBeforeFrame(
                    track,
                    time.frameBefore
                );

            PvZReanimTransform b =
                FindTransformAtOrAfterFrame(
                    track,
                    time.frameAfter
                );

            if (a == null || b == null)
                return 0f;

            float secondsPerFrame =
                definition.fps > 0f
                    ? 1f / definition.fps
                    : 1f / 12f;

            float frameDistance =
                Mathf.Max(
                    1f,
                    b.GetFrame() -
                    a.GetFrame()
                );

            return
                (
                    b.GetX() -
                    a.GetX()
                ) /
                (
                    frameDistance *
                    secondsPerFrame
                ) *
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
                {
                    current =
                        GetLastValidTransform(i);
                }

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

                int realBlend =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            blendTime / 3f
                        )
                    );

                instance.blendCounter =
                    realBlend;

                instance.blendTime =
                    realBlend;

                /*
                 * No mezclar información discreta.
                 */
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
            transform.position =
                new Vector3(
                    x,
                    y,
                    transform.position.z
                );
        }

        public void OverrideScale(
            float x,
            float y)
        {
            transform.localScale =
                new Vector3(
                    x,
                    y,
                    1f
                );
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
            {
                current =
                    GetLastValidTransform(index);
            }

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
            {
                transform =
                    GetLastValidTransform(index);
            }

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