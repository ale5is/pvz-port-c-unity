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

        /*
         * Último estado completo conocido de cada pieza.
         *
         * Se usa solamente para completar valores MissingValue.
         * NO se usa para conservar una animación vieja cuando
         * una pieza realmente está oculta.
         */
        private PvZReanimTransform[] lastValidTransforms;

        private PvZReanimFrameTime cachedFrameTime;
        private bool frameTimeDirty = true;

        public PvZReanimDefinition Definition => definition;

        public PvZReanimImageResolver ImageResolver =>
            imageResolver;

        public float AnimTime => animTime;

        public float AnimRate
        {
            get => animRate;
            set
            {
                animRate = value;
                frameTimeDirty = true;
            }
        }

        public bool IsDead => dead;

        public int LoopCount => loopCount;

        public int TrackCount =>
            definition != null
                ? definition.TrackCount
                : 0;

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

                trackInstances[i]
                    .truncateDisappearingFrames =
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

            if (trackRenderers != null)
            {
                for (int i = 0;
                     i < trackRenderers.Length;
                     i++)
                {
                    if (trackRenderers[i] == null)
                        continue;

                    trackRenderers[i]
                        .SetImageResolver(
                            imageResolver
                        );
                }
            }

            UpdateTracks();
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

            if (blendTime > 0)
                StartBlend(blendTime);

            if (!Mathf.Approximately(
                    newAnimRate,
                    0f))
            {
                animRate = newAnimRate;
            }

            loopType = newLoopType;

            /*
             * En esta implementación trackName identifica
             * una pista existente, pero NO define el rango
             * temporal de la animación.
             *
             * Todas las pistas utilizan el timeline global.
             */
            if (!string.IsNullOrWhiteSpace(trackName) &&
                !TrackExists(trackName))
            {
                Debug.LogWarning(
                    "[PvZReanim] Track no encontrado: " +
                    trackName,
                    this
                );
            }

            frameStart = 0;

            frameCount =
                Mathf.Max(
                    1,
                    definition.GetMaxFrameCount()
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
        // GET FRAMES
        // =========================================================

        public bool GetFramesForLayer(
            string animationName,
            out int resultFrameStart,
            out int resultFrameCount)
        {
            resultFrameStart = 0;
            resultFrameCount = 0;

            if (definition == null ||
                string.IsNullOrWhiteSpace(
                    animationName))
            {
                return false;
            }

            if (!TrackExists(animationName))
                return false;

            resultFrameStart = 0;

            resultFrameCount =
                Mathf.Max(
                    1,
                    definition.GetMaxFrameCount()
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

            float fps = definition.fps;

            if (fps <= 0f)
                fps = 12f;

            /*
             * frameCount representa cantidad de posiciones.
             *
             * Una animación de N frames tarda N / FPS segundos.
             */
            float duration =
                Mathf.Max(
                    1f / fps,
                    frameCount / fps
                );

            float deltaNormalized =
                deltaTime / duration;

            animTime +=
                deltaNormalized *
                animRate;

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

            float frame;

            if (last <= start)
            {
                frame = start;
            }
            else
            {
                frame =
                    Mathf.Lerp(
                        start,
                        last,
                        Mathf.Clamp01(animTime)
                    );
            }

            int before =
                Mathf.FloorToInt(frame);

            float fraction =
                frame - before;

            int after =
                before + 1;

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

                if (current == null)
                {
                    renderer.ResetRenderer();
                    continue;
                }

                /*
                 * Un frame negativo explícito SÍ oculta
                 * la pieza.
                 */
                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    renderer.ResetRenderer();

                    if (lastValidTransforms != null &&
                        i < lastValidTransforms.Length)
                    {
                        lastValidTransforms[i] = null;
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
                        1f -
                        (
                            (float)
                            instance.blendCounter /
                            instance.blendTime
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

                if (lastValidTransforms != null &&
                    i < lastValidTransforms.Length)
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
                track.TransformCount == 0)
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

            if (a == null &&
                b == null)
            {
                return null;
            }

            /*
             * Si uno de los dos puntos no existe,
             * utilizamos el existente.
             */
            if (a == null)
                a = b;

            if (b == null)
                b = a;

            PvZReanimTrackInstance instance =
                trackInstances != null &&
                trackIndex < trackInstances.Length
                    ? trackInstances[trackIndex]
                    : null;

            if (instance != null &&
                instance.truncateDisappearingFrames &&
                a != null &&
                b != null &&
                a.HasFrame &&
                b.HasFrame &&
                a.GetFrame() >= 0f &&
                b.GetFrame() < 0f &&
                frameTime.fraction > 0f)
            {
                return null;
            }

            return
                PvZReanimInterpolator.Interpolate(
                    a,
                    b,
                    frameTime.fraction
                );
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
            return
                FindTrackIndex(trackName) >= 0;
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

            if (a == null ||
                b == null)
            {
                return 0f;
            }

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

                if (current == null &&
                    lastValidTransforms != null &&
                    i < lastValidTransforms.Length)
                {
                    current =
                        lastValidTransforms[i];
                }

                if (current == null)
                    continue;

                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    continue;
                }

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

                instance.blendTransform.image =
                    null;
            }
        }

        // =========================================================
        // POSITION
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
        // IMAGE OVERRIDE
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
                trackInstances[index].imageOverride;
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
                    trackInstances[index].imageOverride;
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
        // STOP
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