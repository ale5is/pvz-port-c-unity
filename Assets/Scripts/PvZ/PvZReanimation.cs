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

        private PvZReanimFrameTime cachedFrameTime;

        private bool frameTimeDirty = true;

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

            AdvanceTime(
                Time.deltaTime
            );

            UpdateTracks();
        }

        // =========================================================
        // INITIALIZATION
        // =========================================================

        public void Initialize(
            PvZReanimDefinition newDefinition)
        {
            definition =
                newDefinition;

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
            }

            frameStart = 0;

            frameCount =
                definition.GetMaxFrameCount();

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
                {
                    Destroy(obj);
                }
                else
                {
                    DestroyImmediate(obj);
                }
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
                    string.IsNullOrEmpty(
                        track.name
                    )
                        ? "Track_" + i
                        : track.name;

                GameObject child =
                    new GameObject(
                        trackName
                    );

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
            definition =
                newDefinition;

            Initialize();
        }

        public void SetImageResolver(
            PvZReanimImageResolver newResolver)
        {
            imageResolver =
                newResolver;

            if (trackRenderers == null)
                return;

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

            loopType =
                newLoopType;

            animRate =
                newAnimRate;

            frameStart =
                Mathf.Clamp(
                    newFrameStart,
                    0,
                    Mathf.Max(
                        0,
                        definition.GetMaxFrameCount() - 1
                    )
                );

            int maxFrames =
                definition.GetMaxFrameCount();

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

            if (frameCount < 1)
                frameCount = 1;

            if (animRate >= 0f)
                animTime = 0f;
            else
                animTime = 0.9999999f;

            loopCount = 0;

            dead = false;

            frameTimeDirty = true;

            UpdateTracks();
        }

        // =========================================================
        // PLAY REANIM
        //
        // Esta es la parte importante.
        //
        // PvZ NO reproduce todo el archivo cuando hace:
        //
        // PlayReanim("anim_idle")
        //
        // Primero obtiene el rango de frames de esa capa.
        // =========================================================

        public void PlayReanim(
            string trackName,
            PvZReanimLoopType newLoopType,
            int blendTime,
            float newAnimRate)
        {
            if (definition == null)
                return;

            if (string.IsNullOrWhiteSpace(
                    trackName))
            {
                Play(
                    newLoopType,
                    newAnimRate
                );

                return;
            }

            // -----------------------------------------------------
            // BLEND
            // -----------------------------------------------------

            if (blendTime > 0)
            {
                StartBlend(
                    blendTime
                );
            }

            // -----------------------------------------------------
            // RATE
            // -----------------------------------------------------

            if (!Mathf.Approximately(
                    newAnimRate,
                    0f))
            {
                animRate =
                    newAnimRate;
            }

            // -----------------------------------------------------
            // LOOP
            // -----------------------------------------------------

            loopType =
                newLoopType;

            // -----------------------------------------------------
            // OBTENER RANGO DE LA ANIMACIÓN
            // -----------------------------------------------------

            int newFrameStart;
            int newFrameCount;

            if (!GetFramesForLayer(
                    trackName,
                    out newFrameStart,
                    out newFrameCount))
            {
                Debug.LogWarning(
                    "[PvZReanim] " +
                    "No se encontró el rango de animación: " +
                    trackName,
                    this
                );

                // Fallback:
                // usar toda la definición.
                newFrameStart = 0;

                newFrameCount =
                    definition.GetMaxFrameCount();
            }

            if (newFrameCount < 1)
                newFrameCount = 1;

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

            // -----------------------------------------------------
            // COMPORTAMIENTO ORIGINAL
            // -----------------------------------------------------

            if (animRate >= 0f)
            {
                animTime = 0f;
            }
            else
            {
                animTime =
                    0.9999999f;
            }

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

            if (definition == null)
                return false;

            if (string.IsNullOrWhiteSpace(
                    animationName))
            {
                return false;
            }

            string wanted =
                animationName.Trim();

            // -----------------------------------------------------
            // 1. BUSCAR TRACK EXACTO
            // -----------------------------------------------------

            PvZReanimTrack animationTrack =
                definition.GetTrack(
                    wanted
                );

            if (animationTrack == null)
            {
                int index =
                    definition.FindTrackIndex(
                        wanted
                    );

                if (index >= 0)
                {
                    animationTrack =
                        definition.GetTrack(
                            index
                        );
                }
            }

            if (animationTrack == null)
            {
                // -------------------------------------------------
                // 2. BUSCAR IGNORANDO MAYÚSCULAS
                // -------------------------------------------------

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
                        animationTrack =
                            track;

                        break;
                    }
                }
            }

            if (animationTrack == null)
                return false;

            if (animationTrack.transforms == null ||
                animationTrack.transforms.Count == 0)
            {
                return false;
            }

            // -----------------------------------------------------
            // 3. EL TRACK DE ANIMACIÓN ACTÚA COMO MARCADOR
            //
            // Los frames válidos delimitan la capa.
            // -----------------------------------------------------

            int firstValid =
                -1;

            int lastValid =
                -1;

            for (int i = 0;
                 i < animationTrack.transforms.Count;
                 i++)
            {
                PvZReanimTransform transform =
                    animationTrack.transforms[i];

                if (transform == null)
                    continue;

                if (transform.HasFrame &&
                    transform.GetFrame() >= 0f)
                {
                    if (firstValid < 0)
                    {
                        firstValid = i;
                    }

                    lastValid = i;
                }
            }

            // -----------------------------------------------------
            // 4. SI NO HAY MARCADORES DE FRAME
            //
            // Algunos archivos usan el propio track como
            // delimitador de la animación.
            // -----------------------------------------------------

            if (firstValid < 0)
            {
                firstValid = 0;

                lastValid =
                    animationTrack.TransformCount - 1;
            }

            if (lastValid < firstValid)
                return false;

            resultFrameStart =
                firstValid;

            resultFrameCount =
                lastValid -
                firstValid +
                1;

            return resultFrameCount > 0;
        }

        // =========================================================
        // TIME
        //
        // PvZ utiliza animTime normalizado:
        //
        // 0.0 = inicio de la animación
        // 1.0 = final de la animación
        // =========================================================

        private void AdvanceTime(
            float deltaTime)
        {
            if (definition == null ||
                frameCount <= 0)
            {
                return;
            }

            if (Mathf.Approximately(
                    animRate,
                    0f))
            {
                return;
            }

            // El Reanimation original avanza usando
            // la cantidad de frames de la capa.
            float framesPerSecond =
                definition.fps;

            if (framesPerSecond <= 0f)
                framesPerSecond = 12f;

            float duration =
                frameCount /
                framesPerSecond;

            if (duration <= 0f)
                duration = 1f;

            float deltaNormalized =
                deltaTime /
                duration;

            animTime +=
                deltaNormalized *
                Mathf.Sign(
                    animRate
                );

            float speed =
                Mathf.Abs(
                    animRate
                );

            if (!Mathf.Approximately(
                    speed,
                    1f))
            {
                animTime +=
                    deltaNormalized *
                    (speed - 1f) *
                    Mathf.Sign(
                        animRate
                    );
            }

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

            int last =
                start +
                count -
                1;

            last =
                Mathf.Min(
                    last,
                    Mathf.Max(
                        0,
                        definition.GetMaxFrameCount() - 1
                    )
                );

            // -----------------------------------------------------
            // PvZ original:
            //
            // frame = frameStart +
            //         (frameCount - 1) * animTime
            // -----------------------------------------------------

            float frame =
                start +
                (last - start) *
                Mathf.Clamp01(
                    animTime
                );

            int before =
                Mathf.FloorToInt(
                    frame
                );

            float fraction =
                frame -
                before;

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
                before =
                    Mathf.Clamp(
                        before,
                        start,
                        last
                    );

                after =
                    Mathf.Clamp(
                        after,
                        start,
                        last
                    );
            }

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

                // -------------------------------------------------
                // FRAME NEGATIVO = PIEZA NO VISIBLE
                //
                // Esto es importante.
                //
                // NO debemos conservar el último transform aquí.
                // PvZ realmente oculta esa pieza.
                // -------------------------------------------------

                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    renderer.ResetRenderer();
                    continue;
                }

                PvZReanimTransform renderTransform =
                    current;

                // -------------------------------------------------
                // BLEND
                // -------------------------------------------------

                if (instance.blendCounter > 0 &&
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

                        instance.blendTransform =
                            null;
                    }
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
            if (definition == null)
                return null;

            PvZReanimTrack track =
                definition.GetTrack(
                    trackIndex
                );

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
                track.GetTransform(
                    before
                );

            PvZReanimTransform b =
                track.GetTransform(
                    after
                );

            if (a == null &&
                b == null)
            {
                return null;
            }

            // -----------------------------------------------------
            // TRUNCATE DISAPPEARING FRAMES
            // -----------------------------------------------------

            PvZReanimTrackInstance instance =
                trackInstances != null &&
                trackIndex >= 0 &&
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
                string.IsNullOrEmpty(
                    trackName))
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
            return FindTrackIndex(
                trackName
            );
        }

        public bool TrackExists(
            string trackName)
        {
            return
                FindTrackIndex(
                    trackName
                ) >= 0;
        }

        // =========================================================
        // TRACK VELOCITY
        // =========================================================

        public float GetTrackVelocity(
            string trackName)
        {
            return GetTrackVelocity(
                GetTrackIndex(
                    trackName
                )
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
                definition.GetTrack(
                    trackIndex
                );

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
                    GetCurrentTransform(
                        i
                    );

                if (current == null)
                    continue;

                if (current.HasFrame &&
                    current.GetFrame() < 0f)
                {
                    continue;
                }

                instance.blendTransform =
                    current.Clone();

                // El original usa aproximadamente
                // blendTime / 3 actualizaciones.
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

                // Igual que el original:
                // el blend no debe interpolar imagen.
                instance.blendTransform.image =
                    null;

                instance.blendTransform.fontName =
                    null;

                instance.blendTransform.text =
                    null;
            }

            frameTimeDirty = true;
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
                FindTrackIndex(
                    trackName
                );

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
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            trackInstances[index]
                .renderGroup =
                renderGroup;

            UpdateTracks();
        }

        public void AssignRenderGroupToPrefix(
            string prefix,
            PvZReanimRenderGroup renderGroup)
        {
            if (trackInstances == null ||
                definition == null ||
                string.IsNullOrEmpty(
                    prefix))
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
                    string.IsNullOrEmpty(
                        track.name))
                {
                    continue;
                }

                if (track.name
                    .ToLowerInvariant()
                    .StartsWith(
                        lowerPrefix))
                {
                    trackInstances[i]
                        .renderGroup =
                        renderGroup;
                }
            }

            UpdateTracks();
        }

        public bool IsTrackShowing(
            string trackName)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0 ||
                trackInstances == null ||
                index >= trackInstances.Length)
            {
                return false;
            }

            if (trackInstances[index]
                    .renderGroup ==
                PvZReanimRenderGroup.Hidden)
            {
                return false;
            }

            PvZReanimTransform transform =
                GetCurrentTransform(
                    index
                );

            if (transform == null)
                return false;

            if (transform.HasFrame &&
                transform.GetFrame() < 0f)
            {
                return false;
            }

            return true;
        }

        // =========================================================
        // TRUNCATE DISAPPEARING FRAMES
        // =========================================================

        public void SetTruncateDisappearingFrames(
            string trackName,
            bool value)
        {
            if (trackInstances == null)
                return;

            if (string.IsNullOrEmpty(
                    trackName))
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
                FindTrackIndex(
                    trackName
                );

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
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            trackInstances[index]
                .imageOverride =
                sprite;

            UpdateTracks();
        }

        public Sprite GetImageOverride(
            string trackName)
        {
            if (trackInstances == null)
                return null;

            int index =
                FindTrackIndex(
                    trackName
                );

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
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return null;

            PvZReanimTransform transform =
                GetCurrentTransform(
                    index
                );

            if (transform == null)
                return null;

            if (transform.HasFrame &&
                transform.GetFrame() < 0f)
            {
                return null;
            }

            if (trackInstances != null &&
                index < trackInstances.Length &&
                trackInstances[index]
                    .imageOverride != null)
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

            return
                imageResolver.Resolve(
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
                    ? definition.GetMaxFrameCount()
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

                    trackInstances[i]
                        .blendCounter = 0;

                    trackInstances[i]
                        .blendTime = 0;

                    trackInstances[i]
                        .blendTransform = null;
                }
            }

            UpdateTracks();
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

                trackRenderers[i]
                    .ResetRenderer();
            }
        }
    }
}
