using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimation : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField]
        private PvZReanimDefinition definition;

        [Header("Playback")]
        [SerializeField]
        private float animTime;

        [SerializeField]
        private float animRate = 1f;

        [SerializeField]
        private PvZReanimLoopType loopType =
            PvZReanimLoopType.Loop;

        [SerializeField]
        private int frameStart = 0;

        [SerializeField]
        private int frameCount = -1;

        private int loopCount;

        private bool dead;

        private PvZReanimTrackInstance[] trackInstances;

        private PvZReanimTrackRenderer[] trackRenderers;

        public PvZReanimDefinition Definition =>
            definition;

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

        private void Update()
        {
            if (dead)
                return;

            if (definition == null)
                return;

            if (trackInstances == null ||
                trackRenderers == null)
            {
                return;
            }

            AdvanceTime(Time.deltaTime);

            UpdateTracks();
        }

        /// <summary>
        /// Inicializa la reanimación utilizando una definición.
        /// </summary>
        public void Initialize(
            PvZReanimDefinition newDefinition)
        {
            if (newDefinition == null)
            {
                Debug.LogError(
                    "PvZReanimation: se intentó inicializar con una definición nula.",
                    this
                );

                return;
            }

            definition = newDefinition;

            animTime = 0f;
            loopCount = 0;
            dead = false;

            frameStart = 0;

            frameCount =
                definition.GetMaxFrameCount();

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
            }

            UpdateTracks();
        }

        /// <summary>
        /// Inicializa utilizando la definición asignada
        /// desde el Inspector.
        /// </summary>
        public void Initialize()
        {
            if (definition == null)
            {
                Debug.LogWarning(
                    "PvZReanimation: no hay una PvZReanimDefinition asignada.",
                    this
                );

                return;
            }

            Initialize(definition);
        }

        private void CreateTrackObjects()
        {
            if (definition == null)
                return;

            DestroyExistingTrackObjects();

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
                        ? $"Track_{i}"
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

                trackRenderers[i] =
                    renderer;
            }
        }

        private void DestroyExistingTrackObjects()
        {
            if (trackRenderers == null)
                return;

            for (int i = 0;
                 i < trackRenderers.Length;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                GameObject child =
                    trackRenderers[i].gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            trackRenderers = null;
        }

        public void Play(
            PvZReanimLoopType newLoopType,
            float newAnimRate = 1f,
            int newFrameStart = 0,
            int newFrameCount = -1)
        {
            if (definition == null)
            {
                Debug.LogWarning(
                    "PvZReanimation: no se puede reproducir porque no existe una definición.",
                    this
                );

                return;
            }

            loopType = newLoopType;

            animRate =
                newAnimRate;

            frameStart =
                Mathf.Max(
                    0,
                    newFrameStart
                );

            int availableFrames =
                definition.GetMaxFrameCount();

            if (newFrameCount > 0)
            {
                frameCount =
                    Mathf.Min(
                        newFrameCount,
                        Mathf.Max(
                            1,
                            availableFrames -
                            frameStart
                        )
                    );
            }
            else
            {
                frameCount =
                    Mathf.Max(
                        0,
                        availableFrames -
                        frameStart
                    );
            }

            animTime = 0f;

            loopCount = 0;

            dead = false;

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

            int index =
                FindTrackIndex(trackName);

            if (index < 0)
            {
                Debug.LogWarning(
                    $"PvZReanimation: no existe el track '{trackName}'.",
                    this
                );

                return;
            }

            loopType =
                newLoopType;

            animRate =
                newAnimRate;

            if (blendTime > 0)
            {
                StartBlend(blendTime);
            }

            animTime = 0f;

            loopCount = 0;

            dead = false;

            UpdateTracks();
        }

        public void Stop()
        {
            dead = true;
        }

        public void Restart()
        {
            animTime = 0f;
            loopCount = 0;
            dead = false;

            UpdateTracks();
        }

        private void AdvanceTime(
            float deltaTime)
        {
            if (definition == null)
                return;

            if (definition.fps <= 0f)
                return;

            float duration =
                GetDuration();

            if (duration <= 0f)
                return;

            animTime +=
                deltaTime * animRate;

            switch (loopType)
            {
                case PvZReanimLoopType.Once:
                    {
                        if (animRate >= 0f)
                        {
                            if (animTime >= duration)
                            {
                                animTime =
                                    duration;

                                dead = true;
                            }
                        }
                        else
                        {
                            if (animTime <= 0f)
                            {
                                animTime = 0f;

                                dead = true;
                            }
                        }

                        break;
                    }

                case PvZReanimLoopType.Loop:
                    {
                        if (animRate >= 0f)
                        {
                            if (animTime >= duration)
                            {
                                int loops =
                                    Mathf.FloorToInt(
                                        animTime /
                                        duration
                                    );

                                loopCount += loops;

                                animTime =
                                    Mathf.Repeat(
                                        animTime,
                                        duration
                                    );
                            }
                        }
                        else
                        {
                            while (animTime < 0f)
                            {
                                animTime += duration;

                                loopCount++;
                            }
                        }

                        break;
                    }

                case PvZReanimLoopType.PingPong:
                    {
                        animTime =
                            Mathf.PingPong(
                                animTime,
                                duration
                            );

                        break;
                    }
            }
        }

        private float GetDuration()
        {
            if (definition == null)
                return 0f;

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

        private void UpdateTracks()
        {
            if (definition == null)
                return;

            if (trackInstances == null)
                return;

            if (trackRenderers == null)
                return;

            PvZReanimFrameTime frameTime =
                GetFrameTime();

            int count =
                Mathf.Min(
                    definition.TrackCount,
                    Mathf.Min(
                        trackInstances.Length,
                        trackRenderers.Length
                    )
                );

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (trackRenderers[i] == null)
                    continue;

                PvZReanimTransform current =
                    GetTransformAtTime(
                        i,
                        frameTime
                    );

                if (current == null)
                    continue;

                UpdateBlend(
                    i,
                    current
                );

                trackRenderers[i].Apply(
                    current,
                    trackInstances[i]
                );
            }
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

            float frame =
                frameStart +
                animTime *
                definition.fps;

            int before =
                Mathf.FloorToInt(frame);

            int after =
                before + 1;

            int maxFrame =
                Mathf.Max(
                    frameStart,
                    frameStart +
                    Mathf.Max(
                        1,
                        frameCount
                    ) -
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
                Mathf.Floor(frame);

            fraction =
                Mathf.Clamp01(
                    fraction
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

            if (track == null)
                return null;

            if (track.TransformCount == 0)
                return null;

            PvZReanimTransform a =
                track.GetTransform(
                    frameTime.frameBefore
                );

            PvZReanimTransform b =
                track.GetTransform(
                    frameTime.frameAfter
                );

            if (a == null)
                return b;

            if (b == null)
                return a;

            return PvZReanimInterpolator.Interpolate(
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

        public int FindTrackIndex(
            string trackName)
        {
            if (definition == null)
                return -1;

            return definition.FindTrackIndex(
                trackName
            );
        }

        public bool TrackExists(
            string trackName)
        {
            return FindTrackIndex(
                trackName
            ) >= 0;
        }

        public void StartBlend(
            int blendTime)
        {
            if (trackInstances == null)
                return;

            if (blendTime <= 0)
                return;

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                PvZReanimTrackInstance instance =
                    trackInstances[i];

                if (instance == null)
                    continue;

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

        private void UpdateBlend(
            int trackIndex,
            PvZReanimTransform current)
        {
            if (trackInstances == null)
                return;

            if (trackIndex < 0 ||
                trackIndex >= trackInstances.Length)
            {
                return;
            }

            PvZReanimTrackInstance instance =
                trackInstances[trackIndex];

            if (instance == null)
                return;

            if (instance.blendCounter <= 0)
                return;

            if (instance.blendTime <= 0)
            {
                instance.blendCounter = 0;
                return;
            }

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

            PvZReanimTransform blended =
                PvZReanimInterpolator.Interpolate(
                    instance.blendTransform,
                    current,
                    factor
                );

            instance.blendCounter--;

            if (trackRenderers != null &&
                trackIndex < trackRenderers.Length &&
                trackRenderers[trackIndex] != null)
            {
                trackRenderers[trackIndex].Apply(
                    blended,
                    instance
                );
            }
        }

        public void SetPosition(
            float x,
            float y)
        {
            Vector3 currentPosition =
                transform.position;

            transform.position =
                new Vector3(
                    x,
                    y,
                    currentPosition.z
                );
        }

        public void SetPosition(
            Vector3 position)
        {
            transform.position =
                position;
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

        public void OverrideScale(
            Vector2 scale)
        {
            OverrideScale(
                scale.x,
                scale.y
            );
        }

        public void SetTrackVisible(
            string trackName,
            bool visible)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            if (trackInstances == null)
                return;

            if (index >= trackInstances.Length)
                return;

            trackInstances[index]
                .renderGroup =
                visible
                    ? PvZReanimRenderGroup.Normal
                    : PvZReanimRenderGroup.Hidden;
        }

        public void ShowOnlyTrack(
            string trackName)
        {
            int target =
                FindTrackIndex(
                    trackName
                );

            if (target < 0)
                return;

            if (trackInstances == null)
                return;

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
        }

        public void ShowAllTracks()
        {
            if (trackInstances == null)
                return;

            for (int i = 0;
                 i < trackInstances.Length;
                 i++)
            {
                if (trackInstances[i] == null)
                    continue;

                trackInstances[i]
                    .renderGroup =
                    PvZReanimRenderGroup.Normal;
            }
        }

        public void AssignRenderGroupToTrack(
            string trackName,
            PvZReanimRenderGroup group)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            if (trackInstances == null)
                return;

            if (index >= trackInstances.Length)
                return;

            trackInstances[index]
                .renderGroup = group;
        }

        public void SetImageOverride(
            string trackName,
            Sprite sprite)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            if (trackInstances == null)
                return;

            if (index >= trackInstances.Length)
                return;

            trackInstances[index]
                .imageOverride = sprite;
        }

        public Sprite GetImageOverride(
            string trackName)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return null;

            if (trackInstances == null)
                return null;

            if (index >= trackInstances.Length)
                return null;

            return trackInstances[index]
                .imageOverride;
        }

        public void ClearImageOverride(
            string trackName)
        {
            SetImageOverride(
                trackName,
                null
            );
        }

        public void SetTrackColor(
            string trackName,
            Color color)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return;

            if (trackInstances == null)
                return;

            if (index >= trackInstances.Length)
                return;

            trackInstances[index]
                .trackColor = color;
        }

        public Color GetTrackColor(
            string trackName)
        {
            int index =
                FindTrackIndex(
                    trackName
                );

            if (index < 0)
                return Color.white;

            if (trackInstances == null)
                return Color.white;

            if (index >= trackInstances.Length)
                return Color.white;

            return trackInstances[index]
                .trackColor;
        }

        public void ReanimationDie()
        {
            dead = true;
        }

        public void ReanimationRevive()
        {
            dead = false;
        }

        public void SetAnimationTime(
            float time)
        {
            float duration =
                GetDuration();

            if (duration <= 0f)
            {
                animTime = 0f;
                UpdateTracks();
                return;
            }

            animTime =
                Mathf.Clamp(
                    time,
                    0f,
                    duration
                );

            UpdateTracks();
        }

        public void SetAnimationFrame(
            int frame)
        {
            if (definition == null)
                return;

            if (definition.fps <= 0f)
                return;

            int maxFrame =
                Mathf.Max(
                    frameStart,
                    frameStart +
                    Mathf.Max(
                        1,
                        frameCount
                    ) -
                    1
                );

            frame =
                Mathf.Clamp(
                    frame,
                    frameStart,
                    maxFrame
                );

            animTime =
                (frame - frameStart) /
                definition.fps;

            UpdateTracks();
        }

        public int GetCurrentFrame()
        {
            if (definition == null)
                return frameStart;

            return GetFrameTime()
                .frameBefore;
        }

        public float GetDurationSeconds()
        {
            return GetDuration();
        }
    }
}