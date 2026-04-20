using System;

namespace CKAN.App.Models
{
    public static class UiScaleSettings
    {
        public const int DefaultPercent = 100;
        public const int MinPercent     = 80;
        public const int MaxPercent     = 120;

        public static int NormalizePercent(int percent)
        {
            int normalized = percent <= 0 ? DefaultPercent : percent;
            if (normalized < MinPercent)
            {
                return MinPercent;
            }
            if (normalized > MaxPercent)
            {
                return MaxPercent;
            }
            return normalized;
        }

        public static double ToFactor(int percent)
            => NormalizePercent(percent) / 100D;
    }
}
