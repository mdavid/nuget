﻿using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Resources;

namespace NuGet
{
    public class NullSettings : ISettings
    {
        private static readonly NullSettings _settings = new NullSettings();

        public static NullSettings Instance
        {
            get { return _settings; }
        }

        public string GetValue(string section, string key)
        {
            return String.Empty;
        }

        public IList<KeyValuePair<string, string>> GetValues(string section)
        {
            return new List<KeyValuePair<string, string>>();
        }

        public void SetValue(string section, string key, string value)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidNullSettingsOperation, "SetValue"));
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidNullSettingsOperation, "SetValues"));
        }

        public bool DeleteValue(string section, string key)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidNullSettingsOperation, "DeleteValue"));
        }

        public bool DeleteSection(string section)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidNullSettingsOperation, "DeleteSection"));
        }
    }
}
