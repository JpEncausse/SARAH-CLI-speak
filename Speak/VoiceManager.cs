using System;
using System.Globalization;
using System.IO;
using System.Speech.Synthesis;

namespace net.encausse.sarah {
    public class VoiceManager : IDisposable {

        private string language = "fr-FR";

        // -------------------------------------------
        //  SINGLETON
        // -------------------------------------------

        private static VoiceManager manager = null;
        private VoiceManager() { }

        public static VoiceManager GetInstance() {
            if (manager == null) {
                manager = new VoiceManager();
            }
            return manager;
        }

        public void Dispose() {
            if (synthesizer != null) {
                synthesizer.Dispose();
            }
        }

        // -------------------------------------------
        //  UTILITY
        // ------------------------------------------

        protected void Log(string msg) {
            Console.WriteLine(msg);
            // System.Diagnostics.Debug.WriteLine(msg);
        }

        // -------------------------------------------
        //  VoiceManager
        // -------------------------------------------

        private SpeechSynthesizer synthesizer;
        public void Init() {
            Init(null, language);
        }
        public void Init(string voice, string language) {
            this.language = language;

            synthesizer = new SpeechSynthesizer();
            synthesizer.SpeakCompleted += new EventHandler<SpeakCompletedEventArgs>(synthesizer_SpeakCompleted);

            // Select voice from properties
            var v = voice;
            if (!String.IsNullOrEmpty(v)) {
                synthesizer.SelectVoice(v);
                Log("Select voice: " + v);
            }
            Log("Voice: " + synthesizer.Voice.Name + " Rate: " + synthesizer.Rate);
        }

        // Synchronous
        protected void synthesizer_SpeakCompleted(object sender, SpeakCompletedEventArgs e) { }


        public void Speak(String tts, Action<MemoryStream> callback) {
            if (String.IsNullOrEmpty(tts)) { Log("No TTS"); return;  }
            Log("Speaking: " + tts);
            try {
                PromptBuilder builder = new PromptBuilder();
                builder.Culture = new CultureInfo(language);
                builder.AppendText(tts);

                using (var ms = new MemoryStream()) {
                    lock (synthesizer) {
                        synthesizer.SetOutputToWaveStream(ms);
                        synthesizer.Speak(builder);
                    }
                    ms.Position = 0;
                    callback(ms);
                }
            }
            catch (Exception ex) {
                Log(ex.Message);
            }
        }
    }
}
