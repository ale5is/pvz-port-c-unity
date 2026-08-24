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
        public void SetOverlayMatrix(
            PvZReanimMatrix matrix)
        {
            overlayMatrix = matrix;
        }
        public void ResetOverlayMatrix()
        {
            overlayMatrix =
                PvZReanimMatrix.Identity;
        }

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
        public int GetSortingOrderForTrack(
            int trackIndex)
        {
            return sortingOrderBase +
                   trackIndex * TRACK_SORTING_STEP;
        }

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

            if (frameBasePose < 0)
                frameBasePose = frameStart;

            frameTimeDirty = true;

            UpdateTracks();
        }

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

        private PvZReanimTransform ApplyOverlayToTransform(
            PvZReanimTransform original)
        {
            if (original == null)
                return null;

            if (overlayMatrix.Equals(PvZReanimMatrix.Identity))
                return original;
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