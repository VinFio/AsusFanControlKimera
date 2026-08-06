using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using AsusFanControlKimera.Localization;

namespace AsusFanControlKimera.Model
{
    internal static class FanCurve
    {
        internal const string DefaultText = "20,40-50,40-60,50-70,65-80,80-90,95-100,100";

        internal static List<Point> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new FormatException(Strings.Get("CurveEmpty"));

            var points = new List<Point>();
            foreach (string item in text.Split('-'))
            {
                string[] values = item.Trim().Split(',');
                int temperature;
                int speed;
                if (values.Length != 2 ||
                    !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out temperature) ||
                    !int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out speed))
                    throw new FormatException(Strings.Get("CurveFormat"));
                if (temperature < 20 || temperature > 105)
                    throw new FormatException(Strings.Get("CurveTemperatureRange"));
                if (speed < 1 || speed > 100)
                    throw new FormatException(Strings.Get("CurveSpeedRange"));
                points.Add(new Point(temperature, speed));
            }

            points = points.OrderBy(p => p.X).ToList();
            if (points.Count < 2)
                throw new FormatException(Strings.Get("CurveMinimumPoints"));
            if (points.Select(p => p.X).Distinct().Count() != points.Count)
                throw new FormatException(Strings.Get("CurveDuplicateTemperature"));
            for (int i = 1; i < points.Count; i++)
                if (points[i].Y < points[i - 1].Y)
                    throw new FormatException(Strings.Get("CurveDecreasingSpeed"));
            return points;
        }

        internal static string Serialize(IEnumerable<Point> points)
        {
            return string.Join("-", points.OrderBy(p => p.X)
                .Select(p => string.Format(CultureInfo.InvariantCulture, "{0},{1}", p.X, p.Y)));
        }

        internal static int Evaluate(IList<Point> points, int temperature)
        {
            if (temperature <= points[0].X)
                return points[0].Y;
            if (temperature >= points[points.Count - 1].X)
                return points[points.Count - 1].Y;

            for (int i = 1; i < points.Count; i++)
            {
                if (temperature <= points[i].X)
                {
                    Point lower = points[i - 1];
                    Point upper = points[i];
                    double ratio = (temperature - lower.X) / (double)(upper.X - lower.X);
                    return (int)Math.Round(lower.Y + ((upper.Y - lower.Y) * ratio));
                }
            }
            return points[points.Count - 1].Y;
        }
    }
}
