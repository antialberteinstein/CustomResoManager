using System;
using System.Collections.Generic;
using System.Linq;
using CustomResoManager.Models;

namespace CustomResoManager.Core
{
    public static class AspectRatioCalculator
    {
        /// <summary>
        /// Calculates the aspect ratio of a given width and height.
        /// </summary>
        public static string CalculateAspectRatio(int width, int height)
        {
            int gcd = GetGreatestCommonDivisor(width, height);
            int aspectWidth = width / gcd;
            int aspectHeight = height / gcd;

            // Handle common approximation mapping e.g., 16:10 or 16:9 
            if (aspectWidth == 8 && aspectHeight == 5) return "16:10";
            if (Math.Abs((double)width / height - (16.0 / 9.0)) < 0.05) return "16:9";

            return $"{aspectWidth}:{aspectHeight}";
        }

        /// <summary>
        /// Filters a list of resolutions and returns only those matching the target aspect ratio.
        /// </summary>
        public static List<ResolutionModel> FilterResolutionsByRatio(List<ResolutionModel> allResolutions, string targetRatio)
        {
            return allResolutions
                .Where(r => CalculateAspectRatio(r.Width, r.Height) == targetRatio)
                .ToList();
        }

        /// <summary>
        /// Computes the Greatest Common Divisor using the Euclidean algorithm.
        /// </summary>
        private static int GetGreatestCommonDivisor(int a, int b)
        {
            return b == 0 ? a : GetGreatestCommonDivisor(b, a % b);
        }
    }
}