using System;
using System.IO;
using System.Text;

namespace EnglishTypingPractice
{
    internal static class ThemedTypingAudio
    {
        private const int SampleRate = 22050;

        internal static MemoryStream CreateSound(UiTheme theme, int level, double volume = 1)
        {
            if (theme == UiTheme.Modern)
                return TypingEffects.CreateSound(level, volume);

            int count = SampleRate * (level == 0 ? 75 : level == 1 ? 195 : 460) / 1000;
            var stream = new MemoryStream(44 + count * 2);
            var noise = new Random(913 + (int)theme);
            double previousNoise = 0;
            double gain = Math.Max(0, Math.Min(1, volume));
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(0x46464952); // RIFF
                writer.Write(36 + count * 2);
                writer.Write(0x45564157); // WAVE
                writer.Write(0x20746D66); // fmt
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(0x61746164); // data
                writer.Write(count * 2);

                for (int i = 0; i < count; i++)
                {
                    double time = (double)i / SampleRate;
                    double white = noise.NextDouble() * 2 - 1;
                    double airyNoise = (white - previousNoise) * .5;
                    double sample = Sample(theme, level, time, airyNoise);
                    double end = Math.Min(1, (count - 1 - i) / (SampleRate * .016));
                    writer.Write((short)(Math.Max(-.96, Math.Min(.96, sample)) * gain * end * short.MaxValue));
                    previousNoise = white;
                }
            }
            stream.Position = 0;
            return stream;
        }

        private static double Sample(UiTheme theme, int level, double t, double noise)
        {
            bool word = level >= 1;
            bool complete = level == 2;
            double sample;
            switch (theme)
            {
                case UiTheme.Steampunk:
                    // Short metallic teeth followed by escaping steam and a closing valve.
                    double tooth = t % .022;
                    sample = (t < .066 ? Tone(tooth, 1230, .0035) * .24 +
                        Tone(tooth, 415, .006) * .18 : 0) + noise * Envelope(t - .008, .023) * .23;
                    if (word)
                        sample += noise * Envelope(t - .032, .061) * .41 + Tone(t - .093, 289, .024) * .23;
                    if (complete)
                        sample += Tone(t - .137, 194, .079) * .32 + Tone(t - .137, 670, .025) * .19 +
                            noise * Envelope(t - .146, .094) * .42 + Tone(t - .270, 368, .063) * .18;
                    return sample;
                case UiTheme.Cyberpunk:
                    sample = Sweep(t, 980, -8000, .020) * .33 + noise * Envelope(t, .006) * .17;
                    if (word)
                        sample += Sweep(t - .030, 460, 9200, .037) * .28 + Tone(t - .064, 1180, .027) * .23;
                    if (complete)
                        sample += Sweep(t - .106, 430, 5500, .085) * .29 +
                            Tone(t - .137, 1479, .090) * .21 + Tone(t - .183, 1760, .083) * .18;
                    return sample;
                case UiTheme.Minimal:
                    sample = Tone(t, 705, .012) * .32;
                    if (word)
                        sample += Tone(t - .035, 940, .045) * .31;
                    if (complete)
                        sample += Tone(t - .102, 1175, .064) * .25 + Tone(t - .184, 1410, .092) * .20;
                    return sample;
                case UiTheme.Vaporwave:
                    sample = Bell(t, 440, .028) * .35 + Bell(t, 443, .023) * .12;
                    if (word)
                        sample += Bell(t - .031, 659, .065) * .23 + Bell(t - .060, 880, .061) * .20;
                    if (complete)
                        sample += Bell(t - .110, 554, .115) * .22 + Bell(t - .151, 698, .112) * .19 +
                            Bell(t - .193, 831, .108) * .15;
                    return sample;
                case UiTheme.Constructivist:
                    sample = Sweep(t, 184, -1060, .024) * .29 + noise * Envelope(t, .012) * .39 +
                        Tone(t, 414, .013) * .22;
                    if (word)
                        sample += Sweep(t - .042, 150, -670, .044) * .34 +
                            noise * Envelope(t - .042, .012) * .39;
                    if (complete)
                        sample += Sweep(t - .115, 128, -260, .085) * .49 +
                            Tone(t - .115, 324, .071) * .20 + Tone(t - .218, 228, .065) * .25;
                    return sample;
                case UiTheme.Persona5:
                    sample = Funk(t, 130.8, .028) * .48 + noise * Envelope(t, .008) * .26;
                    if (word)
                        sample += Funk(t - .033, 196.0, .042) * .38 + Funk(t - .071, 233.1, .054) * .33;
                    if (complete)
                        sample += Funk(t - .115, 261.6, .088) * .23 + Funk(t - .117, 311.1, .081) * .19 +
                            Funk(t - .120, 440.0, .096) * .21 + noise * Envelope(t - .142, .032) * .30;
                    return sample;
                case UiTheme.ChineseStyle:
                    sample = Pluck(t, 392) * .48 + noise * Envelope(t, .005) * .08;
                    if (word)
                        sample += Pluck(t - .041, 587.3) * .32 + Pluck(t - .075, 659.3) * .30;
                    if (complete)
                        sample += Tone(t - .112, 156, .164) * .22 + Tone(t - .112, 351, .097) * .15 +
                            Pluck(t - .120, 784) * .25 + Pluck(t - .176, 587.3) * .22 +
                            Pluck(t - .240, 392) * .22;
                    return sample;
                case UiTheme.Windows98:
                    sample = DesktopPulse(t, 920, .015) * .25 + Tone(t, 235, .008) * .10;
                    if (word)
                        sample += DesktopPulse(t - .033, 1046.5, .030) * .26 + DesktopPulse(t - .068, 830.6, .035) * .20;
                    if (complete)
                        sample += DesktopPulse(t - .126, 698.5, .067) * .24 + DesktopPulse(t - .195, 932.3, .073) * .25 +
                            DesktopPulse(t - .264, 1174.7, .075) * .20;
                    return sample;
                case UiTheme.CassetteFuturism:
                    sample = TapeRelay(t, noise) * .64 + TapeRelay(t - .015, -noise) * .32;
                    if (word)
                        sample += TapeRelay(t - .049, noise) * .62 + Tone(t - .056, 185, .038) * .18;
                    if (complete)
                        sample += TapeRelay(t - .126, noise) * .63 + Sweep(t - .151, 310, -631, .086) * .30 +
                            noise * Envelope(t - .155, .101) * .095 + TapeRelay(t - .311, -noise) * .42;
                    return sample;
                case UiTheme.Magritte:
                    sample = FeltKey(t, 349.2) * .29;
                    if (word)
                        sample += FeltKey(t - .052, 523.25) * .23 + FeltKey(t - .096, 466.16) * .16;
                    if (complete)
                        sample += FeltKey(t - .137, 293.66) * .20 + FeltKey(t - .195, 440) * .18 +
                            FeltKey(t - .206, 587.33) * .15;
                    return sample;
                case UiTheme.Atompunk:
                    sample = OrbitTone(t, 440, 2400, .024) * .31;
                    if (word)
                        sample += OrbitTone(t - .038, 660, 940, .048) * .27 + Tone(t - .091, 1320, .025) * .13;
                    if (complete)
                        sample += OrbitTone(t - .125, 280, 3300, .098) * .30 + OrbitTone(t - .229, 925, -1090, .070) * .25 +
                            Tone(t - .285, 1602, .054) * .14;
                    return sample;
                default:
                    return 0;
            }
        }

        private static double Envelope(double t, double decay)
        {
            return t < 0 ? 0 : Math.Min(1, t * 850) * Math.Exp(-t / decay);
        }

        private static double Tone(double t, double frequency, double decay)
        {
            return Sweep(t, frequency, 0, decay);
        }

        private static double Sweep(double t, double frequency, double rate, double decay)
        {
            return t < 0 ? 0 : Math.Sin(2 * Math.PI * t * (frequency + rate * t * .5)) * Envelope(t, decay);
        }

        private static double Bell(double t, double frequency, double decay)
        {
            if (t < 0)
                return 0;
            double phase = 2 * Math.PI * t * frequency;
            return Math.Sin(phase + .65 * Math.Sin(phase * 2.01)) * Envelope(t, decay);
        }

        private static double Funk(double t, double frequency, double decay)
        {
            if (t < 0)
                return 0;
            double phase = 2 * Math.PI * t * frequency;
            return (Math.Sin(phase) + .32 * Math.Sin(3 * phase) + .15 * Math.Sin(5 * phase)) *
                Envelope(t, decay);
        }

        private static double Pluck(double t, double frequency)
        {
            if (t < 0)
                return 0;
            double phase = 2 * Math.PI * frequency * t;
            return Math.Sin(phase) * Envelope(t, .059) +
                .32 * Math.Sin(phase * 2.01) * Envelope(t, .025) +
                .13 * Math.Sin(phase * 3.03) * Envelope(t, .011);
        }

        private static double DesktopPulse(double t, double frequency, double decay)
        {
            if (t < 0)
                return 0;
            double phase = 2 * Math.PI * frequency * t;
            return (Math.Sin(phase) + .21 * Math.Sin(phase * 3) + .075 * Math.Sin(phase * 5)) * Envelope(t, decay);
        }

        private static double TapeRelay(double t, double noise)
        {
            return Tone(t, 176, .009) * .33 + Tone(t, 836, .004) * .25 + noise * Envelope(t, .009) * .48;
        }

        private static double FeltKey(double t, double frequency)
        {
            return Tone(t, frequency, .105) + .21 * Tone(t, frequency * 2.003, .039) +
                .08 * Tone(t, frequency * 3.014, .018);
        }

        private static double OrbitTone(double t, double frequency, double glide, double decay)
        {
            if (t < 0)
                return 0;
            double phase = 2 * Math.PI * t * (frequency + glide * t * .5);
            return (Math.Sin(phase + .36 * Math.Sin(2 * Math.PI * 11 * t)) + .10 * Math.Sin(2 * phase)) * Envelope(t, decay);
        }
    }
}
