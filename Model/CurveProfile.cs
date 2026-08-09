using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace AsusFanControlKimera.Model
{
    internal sealed class CurveProfile
    {
        internal string Name { get; set; }
        internal string Curve { get; set; }
        internal int Hysteresis { get; set; }
        internal int RefreshInterval { get; set; }
    }

    internal static class CurveProfileStore
    {
        internal const int MaximumNameLength = 40;

        internal static IList<CurveProfile> Deserialize(string text)
        {
            var profiles = new List<CurveProfile>();
            if (string.IsNullOrWhiteSpace(text))
                return profiles;

            try
            {
                var document = new XmlDocument { XmlResolver = null };
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using (var reader = XmlReader.Create(new StringReader(text), settings))
                    document.Load(reader);

                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "curveProfiles")
                    return profiles;

                foreach (XmlNode node in root.ChildNodes)
                {
                    var element = node as XmlElement;
                    if (element == null || element.Name != "profile")
                        continue;

                    CurveProfile profile;
                    if (!TryReadProfile(element, out profile) ||
                        profiles.Any(item => string.Equals(item.Name, profile.Name,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;
                    profiles.Add(profile);
                }
            }
            catch
            {
                // A damaged profile collection must not prevent Kimera from starting.
            }
            return profiles;
        }

        internal static string Serialize(IEnumerable<CurveProfile> profiles)
        {
            var output = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false
            };
            using (XmlWriter writer = XmlWriter.Create(output, settings))
            {
                writer.WriteStartElement("curveProfiles");
                writer.WriteAttributeString("version", "1");
                foreach (CurveProfile profile in profiles)
                {
                    writer.WriteStartElement("profile");
                    writer.WriteAttributeString("name", profile.Name);
                    writer.WriteAttributeString("curve", profile.Curve);
                    writer.WriteAttributeString("hysteresis",
                        profile.Hysteresis.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("interval",
                        profile.RefreshInterval.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            return output.ToString();
        }

        internal static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                name.Trim().Length <= MaximumNameLength &&
                !name.Trim().Any(char.IsControl);
        }

        private static bool TryReadProfile(XmlElement element, out CurveProfile profile)
        {
            profile = null;
            string name = element.GetAttribute("name").Trim();
            string curve = element.GetAttribute("curve");
            int hysteresis;
            int interval;
            if (!IsValidName(name) ||
                !int.TryParse(element.GetAttribute("hysteresis"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out hysteresis) ||
                !int.TryParse(element.GetAttribute("interval"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out interval) ||
                hysteresis < 0 || hysteresis > 15 ||
                interval < 500 || interval > 10000)
                return false;

            try
            {
                curve = FanCurve.Serialize(FanCurve.Parse(curve));
            }
            catch
            {
                return false;
            }

            profile = new CurveProfile
            {
                Name = name,
                Curve = curve,
                Hysteresis = hysteresis,
                RefreshInterval = interval
            };
            return true;
        }
    }
}
