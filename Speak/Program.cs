using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Mono.Options;

namespace net.encausse.sarah {
    class Program {

        static void Main(string[] args) {

            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");

            bool help = false;
            string tts = null;
            int deviceId = -1;
            float speakerVol = 100;
            string voice = null;
            string language = "fr-FR";

            var p = new OptionSet() {
                { "tts=", "the text to speech", v => tts = v },
                { "speaker=", "the speaker id (default to -1)", v => deviceId = int.Parse(v, culture) },
                { "volume=", "the speaker volume (default to 100)", v => speakerVol = float.Parse(v, culture) },
                { "voice=", "the voice id", v => voice = v },
                { "l|language=", "the voice language (default to fr-FR)", v => language = v },
                { "h|help",  "show this message and exit", v => help = v != null },
            };

            List<string> extra;
            try { extra = p.Parse(args); }
            catch (OptionException e) {
                Console.Write("Speak: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `Speak --help' for more information.");
                return;
            }

            if (help) {
                ShowHelp(p);
                return;
            }

            // Init Managers
            VoiceManager.GetInstance().Init(voice, language);
            SpeakerManager.GetInstance().Init(deviceId, speakerVol);
            
            // Speak something
            VoiceManager.GetInstance().Speak(tts, delegate(MemoryStream ms) {
                SpeakerManager.GetInstance().Speak(ms, false);
            });
        }

        static void ShowHelp(OptionSet p) {
            Console.WriteLine("Usage: Speak [OPTIONS]+ message");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
