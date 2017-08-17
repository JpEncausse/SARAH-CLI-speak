
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace net.encausse.sarah {
    public class SpeakerManager : IDisposable {

        private int    deviceId = -1;
        private float  speakerVol = 100; // 0-100
        private float  speakerDelay = 0; // in ms
        private double speakerTimeout = 60; 

        // -------------------------------------------
        //  SINGLETON
        // -------------------------------------------

        private static SpeakerManager manager = null;
        private SpeakerManager() { }

        public static SpeakerManager GetInstance() {
            if (manager == null) {
                manager = new SpeakerManager();
            }
            return manager;
        }

        protected WaveOut waveOut = null;
        public void Init() {
            Init(deviceId, speakerVol, speakerDelay, speakerTimeout);
        }
        public void Init(int deviceId, float speakerVol) {
            Init(deviceId, speakerVol, speakerDelay, speakerTimeout);
        }
        public void Init(int deviceId, float speakerVol, float speakerDelay, double speakerTimeout) {
            this.deviceId = deviceId;
            this.speakerVol = speakerVol;
            this.speakerDelay = speakerDelay;
            this.speakerTimeout = speakerTimeout;

            waveOut = new WaveOut(WaveCallbackInfo.FunctionCallback());
            waveOut.DeviceNumber = deviceId;
        }

        public void Dispose() {
            if (waveOut != null) {
                waveOut.Dispose();
            }
        }

        // -------------------------------------------
        //  UTILITY
        // -------------------------------------------

        protected void Log(string msg) {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        // -------------------------------------------
        //  SpeakerManager
        // -------------------------------------------

        private IDictionary<string, SpeakerState> SpeakerStates = new Dictionary<string, SpeakerState>();
        private static String SPEAKING_LOCK = "SPEAKING";
        public  static String SPEAKING_ID   = "SPEAKING_ID";

        /**
         * Returns true if Speaking State is Speaking
         */
        public bool IsSpeaking() {
            if (!SpeakerStates.ContainsKey(SPEAKING_ID)) { return false; }
            return SpeakerStates[SPEAKING_ID].playing;
        }

        /**
         * Stop named SpeakerState
         * @param name the SpeakerState name or true for SPEAKING_ID
         */
        public void Stop(String name, bool all) {
            if (name == null || name.ToLower().Equals("true")) {
                name = SPEAKING_ID;
            }
            if (!SpeakerStates.ContainsKey(name)) { return; }
            if (all) { StopTimeout = DateTime.Now; }
            SpeakerStates[name].timeout = TimeSpan.Zero;
        }

        /**
         * Play the given stream to Speaking state
         * @param stream the generated tts
         * @param async boolean true/false 
         */
        public void Speak(Stream stream, bool async) {
            try {
                if (stream.Length > 0) {
                    lock (SPEAKING_LOCK) { PlayWAV(SPEAKING_ID, stream, async); }
                }
            }
            finally { }
        }

        public static int FORMAT_WAV = 0;
        public static int FORMAT_MP3 = 1;


        /**
         * Play the given file path
         * @param path the file to play
         * @param async boolean true/false 
         */
        public void Play(String path, bool async) {
            var id = path;
            if (!File.Exists(path)) { return; }

            int format = path.ToLower().EndsWith(".wav") || path.ToLower().EndsWith(".wma") ? FORMAT_WAV : FORMAT_MP3;
            if (format == FORMAT_WAV) {
                using (var stream = new FileStream(path, FileMode.Open)) {
                    PlayWAV(id, stream, async);
                }
            } else {
                var stream = (WaveStream) new Mp3FileReader(path);
                SpeakerStates[id] = new SpeakerState(id, speakerTimeout);
                Play(stream, SpeakerStates[id]);
            }
        }

        /**
         * Play the given stream with given id
         * @param id of the stream
         * @param stream the generated tts
         * @param async boolean true/false 
         * @param format int FORMAT_WAV or FORMAT_MP3
         */
        public void PlayWAV(String id, Stream stream, bool async) {
            var state = new SpeakerState(id, speakerTimeout);
            SpeakerStates[id] = state;
            PlayWAV(stream, async, state);
        }

        private void PlayWAV(Stream stream, bool async, SpeakerState state) {
            var memory = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(memory);

            memory.Position = 0;
            var wav = WaveFormatConversionStream.CreatePcmStream(new WaveFileReader(memory));
            Play(wav, async, state);
        }

        private void Play(WaveStream stream, bool async, SpeakerState state) {
            if (async) {
                Task.Factory.StartNew(() => Play(stream, state));
                return;
            }
            Play(stream, state);
        }

        DateTime StopTimeout = DateTime.Now.AddMinutes(-1);
        private void Play(WaveStream stream, SpeakerState state) {

            // Stop all stream from playing for 1s
            if (DateTime.Now - StopTimeout < TimeSpan.FromMilliseconds(1000)) {
                Log("Stop next speech");
                state.playing = false;
                stream.Dispose();
                return;
            }

            state.start = DateTime.Now;
            using (WaveChannel32 volume = new WaveChannel32(stream)) {
                try {
                    volume.Volume = speakerVol / 100f;

                    // Blank dummy Audio
                    float delay = speakerDelay / 1000f;
                    if (delay > 0) {
                        OffsetSampleProvider osp = new OffsetSampleProvider(volume.ToSampleProvider());
                        osp.DelayBySamples = (int) (osp.WaveFormat.SampleRate * delay) * osp.WaveFormat.Channels;
                        waveOut.Init(osp);
                    } else {
                        waveOut.Init(volume);
                    }

                    waveOut.Play();
                    while (stream.CurrentTime < stream.TotalTime && (DateTime.Now - state.start) < state.timeout) { Thread.Sleep(500); }
                    waveOut.Stop();
                    state.playing = false;
                }
                catch (Exception ex) { Log("Error while using WaveOut: " + ex.Message); }
            }
            stream.Dispose();
        }

    }

    // -------------------------------------------
    //  SPEAKER STATE
    // ------------------------------------------

    public class SpeakerState {
        public String id;
        public DateTime start;
        public TimeSpan timeout;
        public Boolean playing;

        public SpeakerState(string identifier, double ttl) {
            id = identifier;
            start = DateTime.Now;
            timeout = TimeSpan.FromSeconds(ttl);
            playing = true;
        }
    }

}
