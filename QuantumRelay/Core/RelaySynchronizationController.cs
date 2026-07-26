using System;

namespace QuantumRelay
{
    internal sealed class RelaySynchronizationController
    {
        private const double CompletionEpsilon = 0.0001;

        public double Progress { get; private set; }
        public bool IsSynchronized { get; private set; }

        public void Restore(double progress, bool synchronized)
        {
            Progress = Clamp01(progress);
            IsSynchronized = synchronized || Progress >= 1.0 - CompletionEpsilon;

            if (IsSynchronized)
                Progress = 1.0;
        }

        public void Reset()
        {
            Progress = 0.0;
            IsSynchronized = false;
        }

        public void Tick(double durationSeconds, double deltaTime)
        {
            if (IsSynchronized)
                return;

            if (durationSeconds <= CompletionEpsilon)
            {
                Progress = 1.0;
                IsSynchronized = true;
                return;
            }

            Progress = Clamp01(
                Progress + Math.Max(0.0, deltaTime) / durationSeconds);

            if (Progress >= 1.0 - CompletionEpsilon)
            {
                Progress = 1.0;
                IsSynchronized = true;
            }
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
                return 0.0;
            if (value > 1.0)
                return 1.0;
            return value;
        }
    }
}
