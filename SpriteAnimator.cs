using UnityEngine;
using System.Collections;

namespace KonLab
{
    /// <summary>
    /// Handles sprite animation.
    /// Plays certain animation frames, and updates them based on time.
    /// Supports queues, custom sequences, and triggers on frames.
    /// </summary>
    /// <date>31-7-2014</date>
    /// <author>Unknown</author>
    /// <editor>Konstantinos Egkarchos</editor>
    public class SpriteAnimator : MonoBehaviour
    {
        /// <summary>
        /// Stores animation triggers.
        /// If target frame is reached, it dispatches a method name to call using SendMessageUpwards.
        /// </summary>
        [System.Serializable]
        public class AnimationTrigger
        {
            /// <summary>
            /// Name of method to call.
            /// </summary>
            public string name;
            public int frame;
        }

        [System.Serializable]
        public class Animation
        {
            public string name;
            public int fps;
            /// <summary>
            /// True if the animation will loop. False to stop at the last frame.
            /// </summary>
            public bool Loop;
            public Sprite[] frames;
            
            /// <summary>
            /// <para>
            /// Contains a string of sequence-formatted animations
            /// Must be in form [startFrame-endFrame(optional):duration] split by comma (,) for further animations.
            /// eg. <example>0-1:3,2-3:3,4-5:4,6-7:4,8:3,9:3</example>
            /// </para>
            /// <para>If empty animation is done by the frames referenced.</para>
            /// </summary>
            public string SequenceCode;
            
            /// <summary>
            /// Contains a queue string for a follow-up animation or empty for nothing.
            /// String must be in form of int1-int2:animName.
            /// This means minTime-MaxTime to wait for animation of the animName.
            /// </summary>
            public string Cue;

            /// <summary>
            /// Contains triggers for the animation, to dispatch.
            /// </summary>
            public AnimationTrigger[] Triggers;
        }

        [SerializeField]
        private SpriteRenderer spriteRenderer;
        public Animation[] animations;

        /// <summary>
        /// Indicates if there is an animation in progress. (eg. PlayAnimation coroutine is running)
        /// Completed animations are false.
        /// </summary>
        public bool playing { get; private set; }
        public Animation currentAnimation { get; private set; }
        public int currentFrame { get; private set; }
        /// <summary>
        /// Time that must pass for an animation to change to the next frame.
        /// Must be calculated when the animation changes.
        /// </summary>
        float frameDelay;
        /// <summary>
        /// Current animation loop.
        /// </summary>
        public bool loop { get; private set; }
        /// <summary>
        /// Sprite will play this animation upon Start.
        /// </summary>
        public string playAnimationOnStart;

        void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void CheckSpriteRenderer()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void Start()
        {
            if (playAnimationOnStart != "")
                Play(playAnimationOnStart);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
        }

        /// <summary>
        /// Sprite is not played anymore since disabled and all coroutines stop automatically.
        /// </summary>
        void OnDisable()
        {
            playing = false;
            /* If an animation is already playing we keep it in the variable.
             * Also if a new animation is set to play the coroutine doesn't start but the new animation is saved to play.
             * When enabled the new animation will start playing from the current frame set.
             */
        }

        /// <summary>
        /// Continue from where the animation (if existing) was left.
        /// </summary>
        void OnEnable()
        {
            if(currentAnimation != null)
                ForcePlay(currentAnimation, currentFrame);
        }

        /// <summary>
        /// Finds animation by given name and if it exists plays it instantly.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="startFrame"></param>
        public void Play(string name, int startFrame = 0)
        {
            Animation animation = GetAnimation(name);
            if (animation != null)
            {
                if (animation != currentAnimation)
                    ForcePlay(animation, startFrame);
            }
            else
                Debug.LogWarning("Could not find animation: " + name);
        }

        /// <summary>
        /// Instantly plays given animation.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="startFrame"></param>
        public void ForcePlay(string name, int startFrame = 0)
        {
            Animation animation = GetAnimation(name);
            if (animation != null)
                ForcePlay(animation, startFrame);
        }

        /// <summary>
        /// Instantly plays given animation.
        /// </summary>
        /// <param name="animation"></param>
        /// <param name="startFrame">Index of the frame the animation will start.</param>
        public void ForcePlay(Animation animation, int startFrame = 0)
        {
            this.loop = animation.Loop;
            currentAnimation = animation;
            playing = true;
            // Check if the supplied frame is inside range.
            currentFrame = startFrame >= animation.frames.Length ? 0 : startFrame;
            spriteRenderer.sprite = animation.frames[currentFrame];
            StopAllCoroutines();
            // Calculate frame delay for animation
            frameDelay = 1f / (float)animation.fps;

            // If game object is active. Else coroutine will fire an error.
            if (gameObject.activeSelf)
                StartCoroutine(PlayAnimation(currentAnimation));
        }

        public void SlipPlay(string name, int wantFrame, params string[] otherNames)
        {
            if (otherNames != null)
            {
                int length = otherNames.Length;
                for (int i = 0; i < length; i++)
                {
                    if (currentAnimation != null && currentAnimation.name == otherNames[i])
                    {
                        Play(name, currentFrame);
                        break;
                    }
                }
            }
            Play(name, wantFrame);
        }

        /// <summary>
        /// Checks if the given name is the animation currently playing.
        /// </summary>
        /// <param name="name">Animation name to check if is playing right now.</param>
        /// <returns>True if animation is currently playing. False if not.</returns>
        public bool IsPlaying(string name)
        {
            return (currentAnimation != null && currentAnimation.name == name);
        }

        public Animation GetAnimation(string name)
        {
            if (animations != null)
            {
                int length = animations.Length;
                for (int i = 0; i < length; i++)
                {
                    if (animations[i].name == name)
                        return animations[i];
                }
            }
            return null;
        }

        /// <summary>
        /// "Adds to queue" (waits for a random range of seconds) the given animation.
        /// </summary>
        /// <param name="animationName"></param>
        /// <param name="minTime">Minimum time to wait for the new animation to play.</param>
        /// <param name="maxTime">Maximum time to wait for the new animation to play.</param>
        /// <returns></returns>
        IEnumerator CueAnimation(string animationName, float minTime, float maxTime)
        {
            yield return new WaitForSeconds(Random.Range(minTime, maxTime));
            ForcePlay(animationName);
        }

        IEnumerator PlayAnimation(Animation animation)
        {
            playing = true;
            //Debug.Log("Playing animation: " + animation.name);

            float timer = 0f;
            //frameDelay = 1f / (float)animation.fps;
            string cueOnComplete = "";

            /* 
             * Checks if variable cue contains a string
             * If it does it must be in form of 0-1:animName.
             * This means minTime-MaxTime to wait for animation of the animName.
             */
            if (animation.Cue != null && animation.Cue != "")
            {
                if (animation.Cue.IndexOf(':') != -1)
                {
                    string[] dataBits = animation.Cue.Trim().Split(':');

                    string animationName = dataBits[1];
                    dataBits = dataBits[0].Split('-');

                    float minTime = float.Parse(dataBits[0], System.Globalization.CultureInfo.InvariantCulture);
                    float maxTime = minTime;

                    if (dataBits.Length > 1)
                        maxTime = float.Parse(dataBits[1], System.Globalization.CultureInfo.InvariantCulture);

                    StartCoroutine(CueAnimation(animationName, minTime, maxTime));

                    loop = true;
                }
                else
                    cueOnComplete = animation.Cue.Trim();
            }

            // If there is a sequence code play frames from this instead of the referenced frames.
            if (animation.SequenceCode != null && animation.SequenceCode != "")
            {
                while (true)
                {
                    string[] split = animation.SequenceCode.Split(',');
                    foreach (string data in split)
                    {
                        string[] dataBits = data.Trim().Split(':');
                        float duration = float.Parse(dataBits[1], System.Globalization.CultureInfo.InvariantCulture);
                        dataBits = dataBits[0].Split('-');

                        int startFrame = int.Parse(dataBits[0], System.Globalization.CultureInfo.InvariantCulture);
                        int endFrame = startFrame;

                        if (dataBits.Length > 1)
                            endFrame = int.Parse(dataBits[1], System.Globalization.CultureInfo.InvariantCulture);

                        currentFrame = startFrame;

                        Debug.Log("startFrame: " + startFrame + " endFrame: " + endFrame + " duration: " + duration);

                        while (duration > 0f)
                        {
                            while (timer < frameDelay)
                            {
                                duration -= Time.deltaTime;
                                timer += Time.deltaTime;
                                yield return null;
                            }
                            while (timer >= frameDelay)
                            {
                                timer -= frameDelay;
                                currentFrame++;
                                if (currentFrame > endFrame)
                                    currentFrame = startFrame;
                            }

                            spriteRenderer.sprite = animation.frames[currentFrame];
                        }
                    }
                    if (cueOnComplete != "")
                        ForcePlay(cueOnComplete);
                }
            }
            else
            {
                while (loop || currentFrame < animation.frames.Length - 1)
                {
                    while (timer < frameDelay)
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }

                    while (timer >= frameDelay)
                    {
                        timer -= frameDelay;
                        NextFrame(animation);
                    }

                    spriteRenderer.sprite = animation.frames[currentFrame];
                }
                if (cueOnComplete != "")
                    ForcePlay(cueOnComplete);
            }

            currentAnimation = null;
            playing = false;
        }

        /// <summary>
        /// Advances one frame and checks if the new frame has reached the animation end.
        /// Then if the animation loops starts from 0 again, or stops at the last one.
        /// </summary>
        /// <param name="animation"></param>
        void NextFrame(Animation animation)
        {
            currentFrame++;
            foreach (AnimationTrigger animationTrigger in animation.Triggers)
            {
                if (animationTrigger.frame == currentFrame)
                    gameObject.SendMessageUpwards(animationTrigger.name);
            }

            if (currentFrame >= animation.frames.Length)
            {
                if (loop)
                    currentFrame = 0;
                else
                    currentFrame = animation.frames.Length - 1;
            }
        }

        /// <summary>
        /// Checks if the sprite is looking left or right.
        /// </summary>
        /// <returns>1 if sprite looks at right. -1 if sprite looks at left.</returns>
        public int GetFacing()
        {
            return (int)Mathf.Sign(spriteRenderer.transform.localScale.x);
        }

        /// <summary>
        /// Flipts sprite horizontally to the given direction.
        /// </summary>
        /// <param name="dir">If negative the sprite looks at the left. If positive the sprite looks at the right.</param>
        public void FlipTo(float dir)
        {
            if (dir < 0f)
                spriteRenderer.transform.localScale = new Vector3(-1f, 1f, 1f);
            else
                spriteRenderer.transform.localScale = new Vector3(1f, 1f, 1f);
        }

        /// <summary>
        /// Checks sprite for flip looking at a position left or right.
        /// </summary>
        /// <param name="position"></param>
        public void FlipTo(Vector3 position)
        {
            float diff = position.x - transform.position.x;
            FlipTo(diff);
        }
    }
}
