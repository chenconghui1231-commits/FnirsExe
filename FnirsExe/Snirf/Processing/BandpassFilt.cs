using System;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// Homer3-aligned BandpassFilt:
    ///   y2 = filtfilt(lowpass butter order=3) then filtfilt(highpass butter order=5)
    /// Behavior aligned with hmrR_BandpassFilt:
    ///   - lpf==0 => skip lowpass
    ///   - hpf==0 => skip highpass
    ///   - cutoff must be < Nyquist
    ///   - per-column processing
    ///   - zero-phase via filtfilt with odd extension + padlen = 3*order
    /// Filter design:
    ///   - Butterworth via analog prototype poles + prewarped bilinear transform
    ///   - ZPK -> SOS pairing
    /// </summary>
    public static class BandpassFilt
    {
        public static void ApplyInPlace(double[,] x, double fs, double hpf, double lpf)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (fs <= 0) throw new ArgumentOutOfRangeException(nameof(fs));

            int T = x.GetLength(0);
            int M = x.GetLength(1);
            if (T < 4 || M < 1) return;

            // Check NaN / Inf (Homer3 warns+returns; here we throw)
            for (int t = 0; t < T; t++)
                for (int m = 0; m < M; m++)
                {
                    double v = x[t, m];
                    if (double.IsNaN(v))
                        throw new ArgumentException("Input to BandpassFilt contains NaN values (Homer3 suggests adding preprocess NAN step).");
                    if (double.IsInfinity(v))
                        throw new ArgumentException("Input to BandpassFilt must be finite.");
                }

            double nyq = fs * 0.5;
            if (lpf < 0 || hpf < 0) throw new ArgumentException("hpf/lpf must be >= 0");
            if (lpf > 0 && lpf >= nyq) throw new ArgumentException("lpf must be < Nyquist");
            if (hpf > 0 && hpf >= nyq) throw new ArgumentException("hpf must be < Nyquist");

            // Homer3 doesn't force hpf<lpf if one side is 0, but for real bandpass we sanity-check:
            if (hpf > 0 && lpf > 0 && hpf >= lpf)
                throw new ArgumentException("Require hpf < lpf when both are > 0 (bandpass).");

            // Design filters (SOS + gain baked into SOS[0] b-coefs)
            SosFilter lp = default;
            SosFilter hp = default;
            bool doLp = (lpf > 0);
            bool doHp = (hpf > 0);

            if (doLp)
                lp = ButterworthDesign.DesignSos(fs, lpf, order: 3, type: ButterworthDesign.FilterType.Lowpass);

            if (doHp)
                hp = ButterworthDesign.DesignSos(fs, hpf, order: 5, type: ButterworthDesign.FilterType.Highpass);

            // Process per column
            var col = new double[T];
            for (int m = 0; m < M; m++)
            {
                for (int t = 0; t < T; t++) col[t] = x[t, m];

                // Homer3 order: lowpass then highpass
                if (doLp) col = FiltFiltSOS(col, lp);
                if (doHp) col = FiltFiltSOS(col, hp);

                for (int t = 0; t < T; t++) x[t, m] = col[t];
            }
        }

        // ==========================================================
        // filtfilt with SOS (odd extension + padlen + zi)
        // ==========================================================
        private static double[] FiltFiltSOS(double[] x, SosFilter sos)
        {
            int n = x.Length;
            int order = sos.Order;
            int padlen = 3 * order; // MATLAB/Scipy style

            if (n <= padlen + 1)
            {
                // If too short, reduce padlen; this keeps behavior stable
                padlen = Math.Max(0, n - 2);
            }

            // Odd extension (like scipy.signal.filtfilt)
            double[] ext = OddExtend(x, padlen);

            // Forward
            double[] y = SosFilt(ext, sos, useInitialConditions: true);

            // Reverse
            Array.Reverse(y);
            y = SosFilt(y, sos, useInitialConditions: true);
            Array.Reverse(y);

            // Remove padding
            var outy = new double[n];
            Array.Copy(y, padlen, outy, 0, n);
            return outy;
        }

        private static double[] OddExtend(double[] x, int padlen)
        {
            int n = x.Length;
            if (padlen <= 0) return (double[])x.Clone();

            int N = n + 2 * padlen;
            var ext = new double[N];

            double x0 = x[0];
            double xN = x[n - 1];

            // left: 2*x0 - x[1:padlen+1] reversed
            for (int i = 0; i < padlen; i++)
            {
                int src = 1 + i; // x[1], x[2], ...
                if (src >= n) src = n - 1; // clamp
                ext[padlen - 1 - i] = 2 * x0 - x[src];
            }

            // center
            Array.Copy(x, 0, ext, padlen, n);

            // right: 2*xN - x[n-2 : n-padlen-2] reversed
            for (int i = 0; i < padlen; i++)
            {
                int src = (n - 2) - i; // x[n-2], x[n-3], ...
                if (src < 0) src = 0; // clamp
                ext[padlen + n + i] = 2 * xN - x[src];
            }

            return ext;
        }

        private static double[] SosFilt(double[] x, SosFilter sos, bool useInitialConditions)
        {
            // Cascade SOS sections (DF2T)
            double[] y = (double[])x.Clone();

            for (int s = 0; s < sos.Sections.Length; s++)
            {
                var sec = sos.Sections[s];

                double z1, z2;
                if (useInitialConditions)
                {
                    // lfilter_zi for DF2T, scaled by first sample
                    var zi = DF2T_Zi(sec.b0, sec.b1, sec.b2, sec.a1, sec.a2);
                    z1 = zi.z1 * y[0];
                    z2 = zi.z2 * y[0];
                }
                else
                {
                    z1 = 0; z2 = 0;
                }

                for (int i = 0; i < y.Length; i++)
                {
                    double x0 = y[i];
                    double out0 = sec.b0 * x0 + z1;
                    z1 = sec.b1 * x0 - sec.a1 * out0 + z2;
                    z2 = sec.b2 * x0 - sec.a2 * out0;
                    y[i] = out0;
                }
            }

            return y;
        }

        /// <summary>
        /// Equivalent of scipy.signal.lfilter_zi for a 2nd-order IIR in DF2T form.
        /// </summary>
        private static (double z1, double z2) DF2T_Zi(double b0, double b1, double b2, double a1, double a2)
        {
            // For DF2T:
            //   A = [ -a1  1
            //         -a2  0 ]
            //   B = [ b1 - a1*b0
            //         b2 - a2*b0 ]
            // zi = inv(I - A) * B
            double B0 = b1 - a1 * b0;
            double B1 = b2 - a2 * b0;

            double m00 = 1 + a1;
            double m01 = -1;
            double m10 = a2;
            double m11 = 1;

            double det = m00 * m11 - m01 * m10;
            if (Math.Abs(det) < 1e-14)
                return (0, 0);

            double inv00 = m11 / det;
            double inv01 = -m01 / det;
            double inv10 = -m10 / det;
            double inv11 = m00 / det;

            double z1 = inv00 * B0 + inv01 * B1;
            double z2 = inv10 * B0 + inv11 * B1;
            return (z1, z2);
        }

        // ==========================================================
        // Butterworth design: analog prototype -> prewarp -> bilinear
        // ZPK -> SOS, normalize gain at DC (LP) or Nyquist (HP)
        // ==========================================================
        private static class ButterworthDesign
        {
            public enum FilterType { Lowpass, Highpass }

            public static SosFilter DesignSos(double fs, double fc, int order, FilterType type)
            {
                if (order < 1) throw new ArgumentOutOfRangeException(nameof(order));
                if (fc <= 0 || fc >= fs * 0.5) throw new ArgumentOutOfRangeException(nameof(fc));

                // Normalize Wn like MATLAB: Wn = fc / (fs/2)
                double wn = fc / (fs * 0.5);

                // Prewarp for bilinear transform (analog cutoff rad/s)
                // Ωc = 2*fs*tan(pi*wn/2)
                double Oc = 2.0 * fs * Math.Tan(Math.PI * wn / 2.0);

                // Analog prototype poles (Butterworth lowpass at 1 rad/s)
                Complex[] poles = new Complex[order];
                for (int k = 0; k < order; k++)
                {
                    double theta = Math.PI * (2.0 * k + order - 1) / (2.0 * order);
                    poles[k] = new Complex(Math.Cos(theta), Math.Sin(theta));
                }

                // Scale to cutoff
                for (int i = 0; i < poles.Length; i++)
                    poles[i] = poles[i] * Oc;

                Complex[] zeros;
                if (type == FilterType.Lowpass)
                {
                    // Analog zeros at infinity
                    zeros = Array.Empty<Complex>();
                }
                else
                {
                    // Highpass analog transform: s -> Oc/s
                    for (int i = 0; i < poles.Length; i++)
                        poles[i] = (Oc / poles[i]);

                    // Zeros at s=0 repeated order
                    zeros = new Complex[order];
                    for (int i = 0; i < order; i++)
                        zeros[i] = new Complex(0, 0);
                }

                // Bilinear transform: z = (2fs + s) / (2fs - s)
                double T = 2.0 * fs;

                // Digital poles
                Complex[] zp = new Complex[poles.Length];
                for (int i = 0; i < poles.Length; i++)
                    zp[i] = (new Complex(T, 0) + poles[i]) / (new Complex(T, 0) - poles[i]);

                // Digital zeros
                Complex[] zz;
                if (type == FilterType.Lowpass)
                {
                    // zeros at infinity -> z = -1, repeated order
                    zz = new Complex[order];
                    for (int i = 0; i < order; i++) zz[i] = new Complex(-1, 0);
                }
                else
                {
                    // zeros at s=0 -> z = 1, repeated order
                    zz = new Complex[order];
                    for (int i = 0; i < order; i++) zz[i] = new Complex(1, 0);
                }

                // Gain normalization:
                // For lowpass, normalize |H(z=1)| = 1 (DC)
                // For highpass, normalize |H(z=-1)| = 1 (Nyquist)
                Complex zref = (type == FilterType.Lowpass) ? new Complex(1, 0) : new Complex(-1, 0);

                // -------- FIX: rename k -> gain to avoid CS0136 variable shadowing --------
                double gain = 1.0;
                double mag = EvaluateZpkMagnitude(zz, zp, gain, zref);
                if (mag == 0) mag = 1e-30;
                gain = 1.0 / mag;

                // ZPK -> SOS
                var sos = ZpkToSos(zz, zp, gain);

                return new SosFilter(order, sos);
            }

            private static double EvaluateZpkMagnitude(Complex[] z, Complex[] p, double gain, Complex zpoint)
            {
                Complex num = new Complex(gain, 0);
                for (int i = 0; i < z.Length; i++) num *= (zpoint - z[i]);

                Complex den = new Complex(1, 0);
                for (int i = 0; i < p.Length; i++) den *= (zpoint - p[i]);

                Complex h = num / den;
                return h.Abs();
            }

            private static SosSection[] ZpkToSos(Complex[] z, Complex[] p, double gain)
            {
                var zList = new System.Collections.Generic.List<Complex>(z);
                var pList = new System.Collections.Generic.List<Complex>(p);

                // Sort by imag magnitude to pair conjugates
                zList.Sort((a, b) => Math.Abs(b.Im).CompareTo(Math.Abs(a.Im)));
                pList.Sort((a, b) => Math.Abs(b.Im).CompareTo(Math.Abs(a.Im)));

                int sections = (int)Math.Ceiling(Math.Max(zList.Count, pList.Count) / 2.0);
                var sos = new SosSection[sections];

                for (int s = 0; s < sections; s++)
                {
                    Complex z0 = PopBestPairFirst(zList, out Complex z1);
                    Complex p0 = PopBestPairFirst(pList, out Complex p1);

                    var b = PolyFromRootsInZinv(z0, z1);
                    var a = PolyFromRootsInZinv(p0, p1);

                    // normalize a0 -> 1
                    double a0 = a[0];
                    b[0] /= a0; b[1] /= a0; b[2] /= a0;
                    a[1] /= a0; a[2] /= a0;

                    sos[s] = new SosSection
                    {
                        b0 = b[0],
                        b1 = b[1],
                        b2 = b[2],
                        a1 = a[1],
                        a2 = a[2]
                    };
                }

                // Fold overall gain into first SOS section (equivalent to MATLAB's g)
                sos[0].b0 *= gain;
                sos[0].b1 *= gain;
                sos[0].b2 *= gain;

                return sos;
            }

            private static double[] PolyFromRootsInZinv(Complex r0, Complex r1)
            {
                bool has0 = !r0.IsNaN;
                bool has1 = !r1.IsNaN;

                Complex s = new Complex(0, 0);
                Complex prod = new Complex(0, 0);

                if (has0 && has1)
                {
                    s = r0 + r1;
                    prod = r0 * r1;
                }
                else if (has0)
                {
                    s = r0;
                    prod = new Complex(0, 0);
                }
                else if (has1)
                {
                    s = r1;
                    prod = new Complex(0, 0);
                }
                else
                {
                    return new double[] { 1, 0, 0 };
                }

                double c0 = 1.0;
                double c1 = -s.Re;
                double c2 = prod.Re;

                return new double[] { c0, c1, c2 };
            }

            private static Complex PopBestPairFirst(System.Collections.Generic.List<Complex> list, out Complex r1)
            {
                r1 = Complex.NaN;
                if (list.Count == 0) return Complex.NaN;

                Complex r0 = list[0];
                list.RemoveAt(0);

                if (Math.Abs(r0.Im) > 1e-12)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if ((list[i] - r0.Conj()).Abs() < 1e-8)
                        {
                            r1 = list[i];
                            list.RemoveAt(i);
                            return r0;
                        }
                    }

                    if (list.Count > 0)
                    {
                        r1 = list[0];
                        list.RemoveAt(0);
                    }
                    return r0;
                }
                else
                {
                    if (list.Count > 0)
                    {
                        r1 = list[0];
                        list.RemoveAt(0);
                    }
                    return r0;
                }
            }
        }

        // ==========================================================
        // SOS container + minimal Complex
        // ==========================================================
        private readonly struct SosFilter
        {
            public readonly int Order;
            public readonly SosSection[] Sections;

            public SosFilter(int order, SosSection[] sections)
            {
                Order = order;
                Sections = sections ?? throw new ArgumentNullException(nameof(sections));
            }
        }

        private struct SosSection
        {
            public double b0, b1, b2;
            public double a1, a2;
        }

        private readonly struct Complex
        {
            public readonly double Re;
            public readonly double Im;

            public Complex(double re, double im) { Re = re; Im = im; }

            public static Complex NaN => new Complex(double.NaN, double.NaN);

            public bool IsNaN => double.IsNaN(Re) || double.IsNaN(Im);

            public Complex Conj() => new Complex(Re, -Im);

            public double Abs() => Math.Sqrt(Re * Re + Im * Im);

            public static Complex operator +(Complex a, Complex b) => new Complex(a.Re + b.Re, a.Im + b.Im);
            public static Complex operator -(Complex a, Complex b) => new Complex(a.Re - b.Re, a.Im - b.Im);
            public static Complex operator *(Complex a, Complex b) => new Complex(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
            public static Complex operator /(Complex a, Complex b)
            {
                double den = b.Re * b.Re + b.Im * b.Im;
                return new Complex((a.Re * b.Re + a.Im * b.Im) / den, (a.Im * b.Re - a.Re * b.Im) / den);
            }

            public static Complex operator *(Complex a, double s) => new Complex(a.Re * s, a.Im * s);
            public static Complex operator /(double s, Complex a) => new Complex(s, 0) / a;
        }
    }
}
