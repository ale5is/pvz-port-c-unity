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
         * �ltimo transform v�lido de cada track.
         *
         * Esto es importante para reproducir el comportamiento
         * de Reanimator:
         *
         * Si una animaci�n no modifica una pieza, la pieza
         * anterior NO desaparece.
         */
        private PvZReanimTransform[] lastValidTransforms;

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

                lastValidTransforms[i] =
                    null;
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

            /*
             * NO limpiamos lastValidTransforms.
             *
             * Al cambiar de animaci�n, las piezas que no
             * est�n animadas deben conservar su pose.
             */
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
            // FRAMES
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
                    "No se encontr� el rango de animaci�n: " +
                    trackName,
                    this
                );

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
            // REINICIAR TIEMPO
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

            /*
             * MUY IMPORTANTE:
             *
             * No hacemos:
             *
             * lastValidTransforms = null
             *
             * porque las piezas est�ticas de PeaShooter
             * deben permanecer.
             */

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

            /*
             * El nombre del track funciona como marcador
             * de la seccion de frames.
             *
             * En el reanim original, cada track marcador
             * tiene su propio campo "frame" (-1 = fuera de
             * esta sub-animacion, >= 0 = dentro de ella).
             * Hay que escanear ese campo para encontrar el
             * rango real, igual que hace
             * Reanimation::GetFramesForLayer en el motor
             * original (Reanimator.cpp). Si no lo hacemos,
             * siempre devolvemos la linea de tiempo COMPLETA
             * (todas las sub-animaciones concatenadas), que
             * es lo que causa que aparezcan piezas de otras
             * animaciones (p.ej. del Repetidor) antes de que
             * arranque la correcta.
             */

            resultFrameStart = 0;

            resultFrameCount = 1;

            bool foundStart = false;

            for (int i = 0;
                 i < animationTrack.TransformCount;
                 i++)
            {
                PvZReanimTransform t =
                    animationTrack.transforms[i];

                if (t != null &&
                    t.HasFrame &&
                    t.GetFrame() >= 0f)
                {
                    resultFrameStart = i;
                    foundStart = true;
                    break;
                }
            }

            if (!foundStart)
            {
                // Sin marcadores validos: comportamiento
                // igual al original (frameStart 0, count 1).
                return true;
            }

            for (int j = resultFrameStart;
                 j < animationTrack.TransformCount;
                 j++)
            {
                PvZReanimTransform t =
                    animationTrack.transforms[j];

                if (t != null &&
                    t.HasFrame &&
                    t.GetFrame() >= 0f)
                {
                    resultFrameCount =
                        j - resultFrameStart + 1;
                }
            }

            return resultFrameCount > 0;
        }

        // =========================================================
        // TIME
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

            int maxFrame =
                Mathf.Max(
                    0,
                    definition.GetMaxFrameCount() - 1
                );

            int last =
                start +
                count -
                1;

            last =
                Mathf.Min(
                    last,
                    maxFrame
                );

            float normalized =
                Mathf.Clamp01(
                    animTime
                );

            float frame =
                start +
                (last - start) *
                normalized;

            int before =
                Mathf.FloorToInt(
                    frame
                );

            float fraction =
                frame -
                before;

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

                /*
                 * =================================================
                 * TRACK SIN DATOS EN ESTA ANIMACI�N
                 * =================================================
                 *
                 * Esto NO significa que la pieza desaparezca.
                 *
                 * Conservamos la �ltima pose v�lida.
                 */
                if (current == null)
                {
                    if (lastValidTransforms != null &&
                        i < lastValidTransforms.Length &&
                        lastValidTransforms[i] != null)
                    {
                        renderer.Apply(
                            lastValidTransforms[i],
                            instance
                        );
                    }

                    continue;
                }

                /*
                 * =================================================
                 * FRAME NEGATIVO EXPL�CITO
                 * =================================================
                 *
                 * Esto s� significa que PvZ quiere ocultar
                 * esta pieza.
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

                /*
                 * Guardamos una copia independiente.
                 *
                 * No guardamos "current" directamente porque
                 * el interpolador puede devolver objetos nuevos
                 * y queremos que el estado anterior sea estable.
                 */
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
            if (definition == null)
                return null;

            if (trackIndex < 0 ||
                trackIndex >= definition.TrackCount)
            {
                return null;
            }

            PvZReanimTrack track =
                definition.GetTrack(
                    trackIndex
                );

            if (track == null ||
                track.TransformCount == 0)
            {
                return null;
            }

            /*
             * Cada track puede tener una cantidad diferente
             * de transforms.
             *
             * Nunca debemos devolver null simplemente porque
             * el frame global sea mayor que la cantidad del
             * track.
             *
             * El �ltimo transform conocido permanece.
             */

            int before =
                frameTime.frameBefore;

            int after =
                frameTime.frameAfter;

            if (before < 0)
                before = 0;

            if (after < 0)
                after = 0;

            /*
             * Si el frame global est� fuera del track,
             * utilizamos el �ltimo transform del track.
             */
            if (before >= track.TransformCount)
            {
                before =
                    track.TransformCount - 1;
            }

            if (after >= track.TransformCount)
            {
                after =
                    track.TransformCount - 1;
            }

            PvZReanimTransform a =
                track.GetTransform(
                    before
                );

            PvZReanimTransform b =
                track.GetTransform(
                    after
                );

            /*
             * Si el parser dej� un hueco real, intentamos
             * recuperar el �ltimo transform v�lido del track.
             */
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

            if (a == null &&
                b == null)
            {
                return null;
            }

            PvZReanimTrackInstance instance =
                trackInstances != null &&
                trackIndex >= 0 &&
                trackIndex < trackInstances.Length
                    ? trackInstances[trackIndex]
                    : null;

            /*
             * Si el track termina justo cuando empieza
             * una desaparici�n expl�cita, respetamos
             * truncateDisappearingFrames.
             */
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

        private PvZReanimTransform FindPreviousValidTransform(
            PvZReanimTrack track,
            int startIndex)
        {
            if (track == null ||
                track.transforms == null)
            {
                return null;
            }

            int index =
                Mathf.Min(
                    startIndex,
                    track.transforms.Count - 1
                );

            for (int i = index;
                 i >= 0;
                 i--)
            {
                PvZReanimTransform transform =
                    track.transforms[i];

                if (transform != null)
                    return transform;
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

                /*
                 * Si el frame actual no tiene datos,
                 * usamos la �ltima pose v�lida.
                 */
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

                /*
                 * Igual que Resodded:
                 * el blend interpola transformaciones,
                 * no cambia imagen ni texto.
                 */
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

            PvZReanimTransform current =
                GetCurrentTransform(
                    index
                );

            if (current == null &&
                lastValidTransforms != null &&
                index < lastValidTransforms.Length)
            {
                current =
                    lastValidTransforms[index];
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

            if (transform == null &&
                lastValidTransforms != null &&
                index < lastValidTransforms.Length)
            {
                transform =
                    lastValidTransforms[index];
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

            if (lastValidTransforms != null)
            {
                for (int i = 0;
                     i < lastValidTransforms.Length;
                     i++)
                {
                    lastValidTransforms[i] =
                        null;
                }
            }

            UpdateTracks();
        }

        // =========================================================
        // STOP / DIE
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

                trackRenderers[i]
                    .ResetRenderer();
            }
        }
    }
}