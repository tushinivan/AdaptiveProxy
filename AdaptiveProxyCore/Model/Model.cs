using System;
using System.Collections.Generic;

namespace AdaptiveProxyCore.Model
{
    public class ProxyModel
    {
        public string Name { get; set; }
        public string Host { get; set; }
        public TimeSpan TimeOut { get; set; }
        public List<Rule> Rules { get; set; } = new List<Rule>();
        public List<ProxyInfo> ProxyCollection { get; set; } = new List<ProxyInfo>();
    }
}
