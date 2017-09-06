using System.ComponentModel;
using Raven.Server.Config.Attributes;
using Sparrow.Logging;

namespace Raven.Server.Config.Categories
{
    public class LogsConfiguration : ConfigurationCategory
    {
        [DefaultValue("Logs")]
        [ConfigurationEntry("Logs.Path", ConfigurationEntryScope.ServerWideOnly)]
        public string Path { get; set; }

        [DefaultValue(LogMode.Operations)]
        [ConfigurationEntry("Logs.Mode", ConfigurationEntryScope.ServerWideOnly)]
        public LogMode Mode { get; set; }
    }
}
