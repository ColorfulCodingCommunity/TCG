using UnityEngine;
using System.Collections.Generic;

namespace Photon.Voice.Unity
{
    // Plays back input audio via Unity AudioSource
    // May consume audio packets in thread other than Unity's main thread
    public class UnityAudioOut : IAudioOut<float>
    {
        private int frameSize;
        private int bufferSamples;

        private int clipWriteSamplePos;

        private readonly AudioSource source;
        private int channels;
        private bool started;

        private int maxDevPlayDelaySamples;
        private int targetPlayDelaySamples;

        private readonly ILogger logger;
        private readonly string logPrefix;
        private readonly bool debugInfo;

        public UnityAudioOut(AudioSource audioSource, ILogger logger, string logPrefix, bool debugInfo)
        {            
            this.source = audioSource;
            this.logger = logger;
            this.logPrefix = logPrefix;
            this.debugInfo = debugInfo;
        }
        public int Lag { get { return this.clipWriteSamplePos - playSamplePos; } }

        // non-wrapped play position
        private int playSamplePos
        {
            get { return this.started ? this.playLoopCount * this.bufferSamples + this.source.timeSamples : 0; }
            set
            {
                if (this.started)
                {
                    // if negative value is passed (possible when playback starts?), loop count is < 0 and sample position is positive
                    var pos = value % this.bufferSamples;
                    if (pos < 0)
                    {
                        pos += this.bufferSamples;
                    }
                    this.source.timeSamples = pos;
                    this.playLoopCount = value / this.bufferSamples;
                    this.sourceTimeSamplesPrev = this.source.timeSamples;
                }

            }
        }
        private int sourceTimeSamplesPrev;
        private int playLoopCount;

        public bool IsPlaying
        {
            get { return this.source.isPlaying; }
        }

        public void Start(int frequency, int channels, int frameSamples, int playDelayMs)
        {
            // frequency = (int)(frequency * 1.2); // underrun test
            // frequency = (int)(frequency / 1.2); // overrun test

            this.channels = channels;
            this.bufferSamples = 4*(playDelayMs * frequency / 1000 + frameSamples + frequency); // max delay + frame +  1 sec. just in case
            this.frameSize = frameSamples * channels;

            this.source.loop = true;
            // using streaming clip leads to too long delays
            this.source.clip = AudioClip.Create("UnityAudioOut", bufferSamples, channels, frequency, false);
            this.started = true;

            // add 1 frame samples to make sure that we have something to play when delay set to 0
            int playDelaySamples = playDelayMs * frequency / 1000 + frameSamples;
            this.maxDevPlayDelaySamples = playDelaySamples / 2;
            this.targetPlayDelaySamples = playDelaySamples + maxDevPlayDelaySamples;

            this.clipWriteSamplePos = this.targetPlayDelaySamples;
            this.playSamplePos = 0;

            if (this.framePool.Info != this.frameSize)
            {
                this.framePool.Init(this.frameSize);
            }

            this.source.Play();
        }

        Queue<float[]> frameQueue = new Queue<float[]>();
        public const int FRAME_POOL_CAPACITY = 50;
        PrimitiveArrayPool<float> framePool = new PrimitiveArrayPool<float>(FRAME_POOL_CAPACITY, "UnityAudioOut");

        // should be called in Update thread
        public void Service()
        {
            if (this.started)
            {
                lock (this.frameQueue)
                {
                    while (frameQueue.Count > 0)
                    {
                        var frame = frameQueue.Dequeue();
                        this.source.clip.SetData(frame, this.clipWriteSamplePos % this.bufferSamples);
                        this.clipWriteSamplePos += frame.Length / this.channels;
                        framePool.Release(frame);
                    }
                }
                // loop detection (pcmsetpositioncallback not called when clip loops)
                if (this.source.isPlaying)
                {
                    if (this.source.timeSamples < sourceTimeSamplesPrev)
                    {
                        playLoopCount++;
                    }
                    sourceTimeSamplesPrev = this.source.timeSamples;
                }

                var playSamplesPos = this.playSamplePos;
                var lagSamples = this.clipWriteSamplePos - playSamplesPos;
                if (lagSamples > targetPlayDelaySamples + maxDevPlayDelaySamples)
                {
                    this.source.UnPause();
                    this.playSamplePos = this.clipWriteSamplePos - targetPlayDelaySamples;
                    if (this.debugInfo)
                    {
                        this.logger.LogWarning("{0} UnityAudioOut overrun {1} {2} {3} {4} {5}", this.logPrefix, targetPlayDelaySamples - maxDevPlayDelaySamples, targetPlayDelaySamples + maxDevPlayDelaySamples, lagSamples, this.clipWriteSamplePos, playSamplesPos);
                    }
                }
                else if (lagSamples < targetPlayDelaySamples - maxDevPlayDelaySamples)
                {
                    //this.playSamplePos = this.clipWriteSamplePos - targetPlayDelaySamples;
                    if (this.source.isPlaying)
                    {
                        this.source.Pause();
                        if (this.debugInfo)
                        {
                            this.logger.LogWarning("{0} UnityAudioOut underrun {1} {2} {3} {4} {5}", this.logPrefix, targetPlayDelaySamples - maxDevPlayDelaySamples, targetPlayDelaySamples + maxDevPlayDelaySamples, lagSamples, this.clipWriteSamplePos, playSamplesPos);
                        }
                    }
                }
                else
                {
                    this.source.UnPause();
                }

            }
        }
        // may be called on any thread
        public void Push(float[] frame)
        {
            if (!this.started)
            {
                return;
            }

            if (frame.Length == 0)
            {
                return;
            }

            if (frame.Length != this.frameSize)
            {
                logger.LogError("{0} UnityAudioOut audio frames are not of  size: {1} != {2}", this.logPrefix, frame.Length, this.frameSize);
                return;
            }

            float[] b = framePool.AcquireOrCreate();
            System.Buffer.BlockCopy(frame, 0, b, 0, frame.Length * sizeof(float));
            lock (this.frameQueue)
            {
                this.frameQueue.Enqueue(b);
            }
        }

        public void Stop()
        {
            this.started = false;
            if (this.source != null)
            {
                this.source.clip = null;
            }
        }
    }
}