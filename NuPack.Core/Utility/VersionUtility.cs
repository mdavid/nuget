﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using NuGet.Resources;

namespace NuGet {
    public static class VersionUtility {
        private const string NetFrameworkIdentifier = ".NETFramework";

        private static readonly Dictionary<string, string> _knownIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "NET", NetFrameworkIdentifier },
            { ".NET", NetFrameworkIdentifier },
            { "NETFramework", NetFrameworkIdentifier },
            { ".NETFramework", NetFrameworkIdentifier },
            { "SL", "Silverlight" },
            { "Silverlight", "Silverlight" },
        };

        public static Version DefaultTargetFrameworkVersion {
            get {
                // We need to parse the version name out from the mscorlib's assembly name since
                // we can't call GetName() in medium trust
                return typeof(string).Assembly.GetNameSafe().Version;
            }
        }

        public static FrameworkName DefaultTargetFramework {
            get {
                return new FrameworkName(NetFrameworkIdentifier, DefaultTargetFrameworkVersion);
            }
        }

        public static Version ParseOptionalVersion(string version) {
            Version versionValue;
            if (!String.IsNullOrEmpty(version) && Version.TryParse(version, out versionValue)) {
                return versionValue;
            }
            return null;
        }

        /// <summary>
        /// This function tries to normalize a string that represents framework version names into
        /// something a framework name that the package manager understands.
        /// </summary>
        public static FrameworkName ParseFrameworkName(string frameworkName) {
            Debug.Assert(!String.IsNullOrEmpty(frameworkName));

            // {FrameworkName}{Version}
            var match = Regex.Match(frameworkName, @"\d+.*");
            int length = match.Success ? match.Index : frameworkName.Length;
            // Split the framework name into 2 parts, identifier and version
            string identifierPart = frameworkName.Substring(0, length);
            string versionPart = frameworkName.Substring(length);

            if (String.IsNullOrEmpty(identifierPart)) {
                // Use the default identifier (.NETFramework) if none specified
                identifierPart = NetFrameworkIdentifier;
            }
            else {
                // Try to nomalize the identifier to a known identifier
                string knownIdentifier;
                if (_knownIdentifiers.TryGetValue(identifierPart, out knownIdentifier)) {
                    identifierPart = knownIdentifier;
                }
            }

            Version version = null;
            // We support version formats that are integers (40 becomes 4.0)
            int versionNumber;
            if (Int32.TryParse(versionPart, out versionNumber)) {
                // Remove the extra numbers
                if (versionPart.Length > 4) {
                    versionPart = versionPart.Substring(0, 4);
                }

                // Make sure it has at least 2 digits so it parses as a valid version
                versionPart = versionPart.PadRight(2, '0');
                versionPart = String.Join(".", versionPart.Select(ch => ch.ToString()));
            }

            // If we can't parse the version then use the default
            if (!Version.TryParse(versionPart, out version)) {
                version = DefaultTargetFrameworkVersion;
            }

            return new FrameworkName(identifierPart, version);
        }


        /// <summary>
        /// The version string is either a simple version or an arithmetic range
        /// e.g.
        ///      1.0         --> 1.0 ≤ x
        ///      (,1.0]      --> x ≤ 1.0
        ///      (,1.0)      --> x &lt; 1.0
        ///      [1.0]       --> x == 1.0
        ///      (1.0,)      --> 1.0 &lt; x
        ///      (1.0, 2.0)   --> 1.0 &lt; x &lt; 2.0
        ///      [1.0, 2.0]   --> 1.0 ≤ x ≤ 2.0
        /// </summary>
        public static IVersionSpec ParseVersionSpec(string versionString) { 
            IVersionSpec versionInfo;
            if (!TryParseVersionSpec(versionString, out versionInfo)) {
                throw new ArgumentException(
                    String.Format(CultureInfo.CurrentCulture,
                     NuGetResources.InvalidVersionString, versionString));
            }

            return versionInfo;
        }

        public static bool TryParseVersionSpec(string versionString, out IVersionSpec iversionSpec) {
            if (versionString == null) {
                throw new ArgumentNullException("versionString");
            }

            var versionSpec = new VersionSpec();
            versionString = versionString.Trim();

            // First, try to parse it as a plain version string
            Version version;
            if (Version.TryParse(versionString, out version)) {
                // A plain version is treated as an inclusive minimum range
                iversionSpec = new VersionSpec {
                    MinVersion = version,
                    IsMinInclusive = true
                };

                return true;
            }

            // It's not a plain version, so it must be using the bracket arithmetic range syntax

            iversionSpec = null;

            // Fail early if the string is too short to be valid
            if (versionString.Length < 3) {
                return false;
            }

            // The first character must be [ ot (
            switch (versionString.First()) {
                case '[':
                    versionSpec.IsMinInclusive = true;
                    break;
                case '(':
                    versionSpec.IsMinInclusive = false;
                    break;
                default:
                    return false;
            }

            // The last character must be ] ot )
            switch (versionString.Last()) {
                case ']':
                    versionSpec.IsMaxInclusive = true;
                    break;
                case ')':
                    versionSpec.IsMaxInclusive = false;
                    break;
                default:
                    return false;
            }

            // Get rid of the two brackets
            versionString = versionString.Substring(1, versionString.Length - 2);

            // Split by comma, and make sure we don't get more than two pieces
            string[] parts = versionString.Split(',');
            if (parts.Length > 2) {
                return false;
            }

            // If there is only one piece, we use it for both min and max
            string minVersionString = parts[0];
            string maxVersionString = (parts.Length == 2) ? parts[1] : parts[0];

            // Only parse the min version if it's non-empty
            if (!String.IsNullOrWhiteSpace(minVersionString)) {
                if (!Version.TryParse(minVersionString, out version)) {
                    return false;
                }
                versionSpec.MinVersion = version;
            }

            // Same deal for max
            if (!String.IsNullOrWhiteSpace(maxVersionString)) {
                if (!Version.TryParse(maxVersionString, out version)) {
                    return false;
                }
                versionSpec.MaxVersion = version;
            }

            // Successful parse!
            iversionSpec = versionSpec;
            return true;
        }
    }
}
