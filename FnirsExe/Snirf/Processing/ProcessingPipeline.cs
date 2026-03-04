using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public class ProcessingPipeline
    {
        public IntensityToOd.Options OptInt2Od { get; set; } = new IntensityToOd.Options();
        public MotionArtifactByChannel.Options OptMotion { get; set; } = new MotionArtifactByChannel.Options();
        public PruneChannels.Options OptPrune { get; set; } = new PruneChannels.Options();

        public MotionCorrectWavelet.Options OptWavelet { get; set; } =
            new MotionCorrectWavelet.Options { TurnOn = true, IQR = 1.5 };

        public bool EnableCbsi { get; set; } = false;
        public double BandpassLpf { get; set; } = 0.1;
        public double BandpassHpf { get; set; } = 0.01;

        public double[] Ppf { get; set; } = { 6.0, 6.0 };
        public double BlockAvgTPre { get; set; } = -5.0;
        public double BlockAvgTPost { get; set; } = 25.0;

        public List<MotionArtifactByChannel.Result> MotionResults { get; private set; }
        public bool[] ActiveChannels { get; private set; }
        public Od2Conc.ConcResult ConcData { get; private set; }
        public BlockAvgHomer3.BlockAvgResult BlockAvgResults { get; private set; }

        public ProcessingPipeline()
        {
            MotionResults = new List<MotionArtifactByChannel.Result>();
        }

        public void Run(SnirfFile snirf)
        {
            if (snirf == null || snirf.Data == null || snirf.Data.Count == 0) return;

            // 0) PruneChannels on raw intensity
            ActiveChannels = PruneChannels.ComputeActiveChannels(snirf, 0, null, OptPrune);

            // 1) Intensity -> OD
            IntensityToOd.ApplyInPlace(snirf, OptInt2Od);

            // 2) MotionArtifactByChannel
            MotionResults.Clear();
            for (int i = 0; i < snirf.Data.Count; i++)
            {
                var res = MotionArtifactByChannel.Compute(snirf, i, null, ActiveChannels, null, OptMotion);
                MotionResults.Add(res);
            }

            // 3) Wavelet correction
            if (OptWavelet.TurnOn)
                MotionCorrectWavelet.Apply(snirf, OptWavelet);

            // 4) Bandpass on OD
            foreach (var block in snirf.Data)
            {
                double dt = block.Time[1] - block.Time[0];
                double fs = 1.0 / dt;
                BandpassFilt.ApplyInPlace(block.DataTimeSeries, fs, BandpassHpf, BandpassLpf);
            }

            // 5) OD -> Conc
            ConcData = Od2Conc.ComputeStrictHomer3(snirf, 0, Ppf);

            // 6) CBSI (Homer3截图没开，默认关)
            if (EnableCbsi && ConcData != null)
                MotionCorrectCbsi.ApplyInPlace(ConcData);

            // 7) BlockAvg on conc
            if (ConcData != null)
                BlockAvgResults = BlockAvgHomer3.Compute(ConcData, snirf, BlockAvgTPre, BlockAvgTPost);
        }
    }
}
