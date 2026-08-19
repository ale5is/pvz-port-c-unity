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

        // Guarda el último transform válido de cada track.
        // Esto evita que una pieza desaparezca cuando un frame
        // concreto no contiene todos sus datos.
        private PvZReanimTransform[] lastValidTransforms;

        public PvZReanimDefinition Definition =>
            definition;

        public PvZReanimImageResolver ImageResolver =>
            imageResolver;

        public float AnimTime =>
            animTime;

        public float AnimRate
        {
            get => animRate;
            set => animRate = value;
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

                lastValidTransforms[i] =
                    null;
            }

            frameStart = 0;

            frameCount =
                definition.GetMaxFrameCount();

            animTime = 0f;

            loopCount = 0;

            dead = false;

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
                        ? $"Track_{i}"
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
        // DEFINITION / RESOLVER
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
        // PLAYBACK
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
                Mathf.Max(
                    0,
                    newFrameStart
                );

            frameCount =
                newFrameCount > 0
                    ? newFrameCount
                    : definition.GetMaxFrameCount();

            animTime = 0f;

            loopCount = 0;

            dead = false;

            // Al comenzar una nueva animación NO borramos
            // los transforms anteriores inmediatamente.
            // Esto permite que las piezas continúen visibles
            // hasta que el nuevo frame proporcione sus datos.

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

            loopType =
                newLoopType;

            animRate =
                newAnimRate;

            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            if (blendTime > 0)
            {
                StartBlend(
                    blendTime
                );
            }

            animTime = 0f;

            dead = false;

            UpdateTracks();
        }

        private void AdvanceTime(
            float deltaTime)
        {
            if (definition == null ||
                definition.fps <= 0f)
            {
                return;
            }

            float duration =
                GetDuration();

            if (duration <= 0f)
                return;

            animTime +=
                deltaTime *
                animRate;

            switch (loopType)
            {
                case PvZReanimLoopType.Once:

                    if (animTime >= duration)
                    {
                        animTime =
                            duration;

                        dead = true;
                    }

                    break;

                case PvZReanimLoopType.Loop:

                    if (animTime >= duration)
                    {
                        int loops =
                            Mathf.FloorToInt(
                                animTime /
                                duration
                            );

                        loopCount +=
                            loops;

                        animTime =
                            Mathf.Repeat(
                                animTime,
                                duration
                            );
                    }

                    break;

                case PvZReanimLoopType.PingPong:

                    animTime =
                        Mathf.PingPong(
                            animTime,
                            duration
                        );

                    break;
            }
        }

        private float GetDuration()
        {
            int count =
                frameCount > 0
                    ? frameCount
                    : definition.GetMaxFrameCount();

            if (count <= 1)
                return 0f;

            return
                (count - 1) /
                definition.fps;
        }

        // =========================================================
        // TRACK UPDATE
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

                // -------------------------------------------------
                // SI EL FRAME NO TIENE TRANSFORM:
                // usar el último transform válido.
                // -------------------------------------------------

                if (current == null)
                {
                    current =
                        lastValidTransforms[i];

                    if (current == null)
                    {
                        continue;
                    }
                }
                else
                {
                    // -------------------------------------------------
                    // GUARDAR TRANSFORM VÁLIDO
                    // -------------------------------------------------

                    lastValidTransforms[i] =
                        current.Clone();
                }

                // -------------------------------------------------
                // BLENDING
                // -------------------------------------------------

                PvZReanimTransform renderTransform =
                    current;

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
                        instance.blendTransform =
                            null;

                        instance.blendTime =
                            0;
                    }
                }

                // -------------------------------------------------
                // RENDER
                // -------------------------------------------------

                renderer.Apply(
                    renderTransform,
                    instance
                );
            }
        }

        // =========================================================
        // FRAME
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

            float frame =
                frameStart +
                animTime *
                definition.fps;

            int before =
                Mathf.FloorToInt(
                    frame
                );

            int after =
                before + 1;

            int maxFrame =
                Mathf.Max(
                    frameStart,
                    frameStart +
                    frameCount -
                    1
                );

            before =
                Mathf.Clamp(
                    before,
                    frameStart,
                    maxFrame
                );

            after =
                Mathf.Clamp(
                    after,
                    frameStart,
                    maxFrame
                );

            float fraction =
                frame -
                Mathf.Floor(
                    frame
                );

            return new PvZReanimFrameTime(
                fraction,
                before,
                after
            );
        }

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

            PvZReanimTransform a =
                track.GetTransform(
                    frameTime.frameBefore
                );

            PvZReanimTransform b =
                track.GetTransform(
                    frameTime.frameAfter
                );

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
            return definition != null
                ? definition.FindTrackIndex(
                    trackName
                )
                : -1;
        }

        public bool TrackExists(
            string trackName)
        {
            return FindTrackIndex(
                trackName
            ) >= 0;
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

                instance.blendCounter =
                    blendTime;

                instance.blendTime =
                    blendTime;

                PvZReanimTransform current =
                    GetCurrentTransform(i);

                if (current != null)
                {
                    instance.blendTransform =
                        current.Clone();
                }
            }
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
        // RENDER GROUPS
        // =========================================================

        public void ShowOnlyTrack(
            string trackName)
        {
            int target =
                FindTrackIndex(
                    trackName
                );

            if (target < 0 ||
                trackInstances == null)
            {
                return;
            }

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                trackInstances[i]
                    .renderGroup =
                    i == target
                        ? PvZReanimRenderGroup.Normal
                        : PvZReanimRenderGroup.Hidden;
            }

            UpdateTracks();
        }

        public void ShowAllTracks()
        {
            if (trackInstances == null)
                return;

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                trackInstances[i]
                    .renderGroup =
                    PvZReanimRenderGroup.Normal;
            }

            UpdateTracks();
        }

        public void AssignRenderGroupToTrack(
            string trackName,
            PvZReanimRenderGroup group)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0 ||
                trackInstances == null)
            {
                return;
            }

            trackInstances[index]
                .renderGroup =
                group;

            UpdateTracks();
        }

        // =========================================================
        // IMAGE OVERRIDE
        // =========================================================

        public void SetImageOverride(
            string trackName,
            Sprite sprite)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0 ||
                trackInstances == null)
            {
                return;
            }

            trackInstances[index]
                .imageOverride =
                sprite;

            UpdateTracks();
        }

        public Sprite GetImageOverride(
            string trackName)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0 ||
                trackInstances == null)
            {
                return null;
            }

            return trackInstances[index]
                .imageOverride;
        }

        // =========================================================
        // DEATH
        // =========================================================

        public void ReanimationDie()
        {
            dead = true;
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private void OnDestroy()
        {
            DestroyTrackObjects();
        }
    }
}