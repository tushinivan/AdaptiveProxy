using Microsoft.VisualStudio.TestTools.UnitTesting;
using AdaptiveProxyCore;
using System.Net;

namespace AdaptiveProxyTest.Tests
{
    [TestClass]
    public class AdaptiveProxyTest
    {
        [TestMethod]
        public void SendQueryTest()
        {
            AdaptiveProxy proxy = new AdaptiveProxy();
            proxy.Start();

            WebClient client = new WebClient();
            client.Proxy = new WebProxy("127.0.0.1:5555");
            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (iPhone; CPU iPhone OS 11_0 like Mac OS X) AppleWebKit/604.1.38 (KHTML, like Gecko) Version/11.0 Mobile/15A372 Safari/604.1");
            var s = client.DownloadString("http://whatismyip.host");
        }
    }
}
