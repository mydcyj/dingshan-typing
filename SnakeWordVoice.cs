using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class SnakeWordVoice : IDisposable
    {
        private readonly Action unavailable;
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        private SpeechSynthesizer synthesizer;
        private bool failed;

        internal SnakeWordVoice(Action unavailable)
        {
            this.unavailable = unavailable;
        }

        internal bool Enable()
        {
            if (synthesizer != null)
                return true;
            if (failed)
                return false;
            SpeechSynthesizer voice = null;
            try
            {
                voice = new SpeechSynthesizer();
                var selected = voice.GetInstalledVoices().FirstOrDefault(item => item.Enabled &&
                    item.VoiceInfo.Gender == VoiceGender.Female &&
                    item.VoiceInfo.Culture.TwoLetterISOLanguageName == "en");
                if (selected != null)
                {
                    voice.SelectVoice(selected.VoiceInfo.Name);
                    voice.Rate = 2;
                    voice.Volume = 100;
                    voice.SpeakCompleted += (sender, e) =>
                    {
                        if (e.Error != null)
                            dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (ReferenceEquals(synthesizer, sender))
                                    Fail();
                            }));
                    };
                    synthesizer = voice;
                    return true;
                }
            }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (NullReferenceException) { } // System.Speech can throw here if Windows has no default audio output.
            if (voice != null)
                voice.Dispose();
            failed = true;
            unavailable();
            return false;
        }

        internal void Speak(string word, int volume)
        {
            if (synthesizer == null)
                return;
            try
            {
                synthesizer.Volume = volume;
                var text = new PromptBuilder(synthesizer.Voice.Culture);
                text.StartStyle(new PromptStyle { Emphasis = PromptEmphasis.Strong });
                text.AppendText(SnakeWords.SpokenText(word));
                text.EndStyle();
                synthesizer.SpeakAsync(text);
            }
            catch (InvalidOperationException)
            {
                Fail();
            }
            catch (System.Runtime.InteropServices.COMException) { Fail(); }
            catch (NullReferenceException) { Fail(); } // System.Speech default device lookup.
        }

        private void Fail()
        {
            if (failed)
                return;
            failed = true;
            Dispose();
            unavailable();
        }

        internal void Stop()
        {
            if (synthesizer != null)
                try { synthesizer.SpeakAsyncCancelAll(); }
                catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            if (synthesizer == null)
                return;
            var current = synthesizer;
            synthesizer = null;
            current.Dispose();
        }
    }
}
