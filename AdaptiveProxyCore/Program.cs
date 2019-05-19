using System;
using System.IO;
using System.Net;

namespace AdaptiveProxyCore
{
    class Program
    {
        static void Main(string[] args)
        {
            AdaptiveProxy proxy = new AdaptiveProxy();
            proxy.Start();

            Console.ReadLine();

            try
            {
                WebClient client = new WebClient();
                client.Proxy = new WebProxy("127.0.0.1:5555");
                client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (iPhone; CPU iPhone OS 11_0 like Mac OS X) AppleWebKit/604.1.38 (KHTML, like Gecko) Version/11.0 Mobile/15A372 Safari/604.1");
                var s = client.DownloadString("http://whatismyip.host");
            }
            catch (WebException ex)
            {
                var x = ex.Response.GetResponseStream();
                StreamReader reader = new StreamReader(x);
                string f = reader.ReadToEnd();
            }
           

            Console.ReadLine();
        }
    }
}
