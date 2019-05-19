using System;
using System.Net;

namespace AdaptiveProxyCore.Model
{
    public class ProxyInfo
    {
        public string Address { get; set; }
        public WebProxy Proxy { get; set; }

        public TimeSpan LastUsed { get; set; }
        public TimeSpan Delay { get; set; }
        public bool IsProxy { get => Proxy != null; }

        public ProxyInfo()
        {

        }
        public ProxyInfo(string address, bool isProxy)
        {
            Address = address;
            if (isProxy)
            {
                Proxy = new WebProxy(address);
            }
        }
    }
}
