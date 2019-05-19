using System;
using System.Text.RegularExpressions;

namespace AdaptiveProxyCore.Model
{
    public class Rule
    {
        public string Parameter { get; set; }
        public string Action { get; set; }
        public Regex If { get; set; }
        public string Then { get; set; }

        public TimeSpan Delay { get; set; }
        public string Result { get; set; }
    }
}
