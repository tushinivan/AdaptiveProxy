using AdaptiveProxyCore.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace AdaptiveProxyCore
{
    public class AdaptiveProxy
    {
        private readonly HttpListener listener = new HttpListener();
        private Dictionary<string, ProxyModel> models = new Dictionary<string, ProxyModel>();

        private Stopwatch timer = new Stopwatch();
        private Random random = new Random();

        public AdaptiveProxy()
        {
            models.Add("default", new ProxyModel()
            {
                Name = "default",
                TimeOut = new TimeSpan(0, 3, 0),
                ProxyCollection = new List<ProxyInfo>()
                {
                    new ProxyInfo()
                }
            });
        }

        public void AddModel(ProxyModel model)
        {
            if (model != null)
            {
                throw new NullReferenceException();
            }

            if (!models.TryAdd(model.Name, model))
            {
                throw new ArgumentException("The model has already been added");
            }
        }

        public void Start()
        {
            Start("127.0.0.1", 5555);
        }
        public async void Start(string host, int port)
        {
            string prefix = $"http://{host}:{port}/";

            try
            {
                listener.Prefixes.Add(prefix);
                listener.Start();
                timer.Start();

                Console.WriteLine($"Proxy: Start on {prefix}");

                while (listener.IsListening)
                {
                    var x = await listener.GetContextAsync();
                    if (x != null)
                    {
                        QueryContext context = new QueryContext()
                        {
                            Input = x.Request,
                            Output = x.Response
                        };

                        //добавляем модель
                        string modelHeader = context.Input.Headers["APX-Model"] ?? "default";
                        if (models.TryGetValue(modelHeader, out ProxyModel model))
                        {
                            context.Model = model;
                        }

                        //таймаут
                        string timeoutHeader = context.Input.Headers["APX-Timeout"];
                        if (timeoutHeader != null)
                        {
                            if (int.TryParse(timeoutHeader, out int value))
                            {
                                context.TimeOut = TimeSpan.FromMilliseconds(value);
                            }
                        }

                        //выполняем запрос
                        Execute(context, timer);

                        //закрываем соединение и отправляем ответ
                        context.Output.Close();
                    }
                }

                timer.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Proxy: Started failed {ex.Message}");
            }
            
        }
        public void Stop()
        {
            listener.Stop();
        }

        private void Execute(QueryContext context, Stopwatch timer)
        {
            while (true)
            {
                try
                {
                    //выбираем прокси
                    context.Proxy = GetProxy(context.Model);

                    //отправляем запрос к ресурсу
                    SendRequest(context);

                    //проверка модели
                    var checkResult = CheckRules(context);
                    if (checkResult)
                    {
                        //возвращаем результат
                        SendResponse(context);
                        break;
                    }
                    else if (timer.Elapsed - context.TimeBegin > context.TimeOut)
                    {
                        //возвращаем ошибку по таймауту
                        context.Output.ContentLength64 = 0;
                        context.Output.StatusDescription = "Adaptive Proxy Timeout";
                        context.Output.StatusCode = 999;
                        break;
                    }
                }
                catch (Exception)
                {
                    //возвращаем ошибку
                    context.Output.ContentLength64 = 0;
                    context.Output.StatusDescription = "Adaptive Proxy Fatal Error";
                    context.Output.StatusCode = 998;
                    break;
                }
            }
        }

        private ProxyInfo GetProxy(ProxyModel model)
        {
            while (true)
            {
                if (model != null)
                {
                    var items = (from t in model.ProxyCollection where timer.Elapsed - t.LastUsed >= t.Delay select t).ToArray();
                    if (items.Length > 0)
                    {
                        return items[random.Next(0, items.Count())];
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                else
                {
                    throw new ArgumentNullException();
                }
            }
        }
        private void SendRequest(QueryContext context)
        {
            context.Request = WebRequest.CreateHttp(context.Input.RawUrl);

            if (!context.Proxy.IsProxy && context.Model.ProxyCollection.Count > 1)
            {
                //если локальных ip более 1, выбираем случайым образом один из них
                //TODO: Переделать
                context.Request.ServicePoint.BindIPEndPointDelegate = delegate (ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
                {
                    IPAddress ip = IPAddress.Parse("127.0.0.1");
                    int port = 0; //This in most cases should stay 0. This when 0 will bind to any port available.
                    return new IPEndPoint(ip, port);
                };
            }
            else
            {
                context.Request.Proxy = context.Proxy.Proxy;
            }

            //копируем заголовки
            context.Request.Method = context.Input.HttpMethod;
            context.Request.Headers.Add(context.Request.Headers);

            //сохраняем тело запроса для отправки
            if (context.RequestBody == null && context.Input.ContentLength64 > 0)
            {
                context.RequestBody = new MemoryStream((int)context.Input.ContentLength64);
                using (var stream = context.Request.GetRequestStream())
                {
                    stream.CopyTo(context.RequestBody);
                }
            }

            //копируем тело запроса
            if (context.Request.Method != "GET" && context.RequestBody != null)
            {
                context.Request.ContentLength = context.RequestBody.Length;
                using (var stream = context.Request.GetRequestStream())
                {
                    context.RequestBody.Position = 0;
                    context.RequestBody.CopyTo(stream);
                }
            }

            //выполняем запрос
            try
            {
                context.Response = context.Request.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                context.Response = ex.Response as HttpWebResponse;
            }
        }
        private void SendResponse(QueryContext context)
        {
            //копируем тело ответа
            if (context.Response != null)
            {
                context.Output.Headers.Add(context.Response.Headers);
                context.Output.StatusCode = (int)context.Response.StatusCode;
                context.Output.StatusDescription = context.Response.StatusDescription;
                context.Output.ContentLength64 = context.Response.ContentLength;

                if (context.ResponseBody != null)
                {
                    context.ResponseBody.Position = 0;
                    context.ResponseBody.CopyTo(context.Output.OutputStream);
                }
                else
                {
                    using (var stream = context.Response.GetResponseStream())
                    {
                        stream.CopyTo(context.Output.OutputStream);
                    }
                }
            }
            else
            {
                context.Output.StatusCode = 997;
            }
        }
        private bool CheckRules(QueryContext context)
        {
            foreach (var rule in context.Model.Rules)
            {
                switch (rule.Parameter)
                {
                    case "status":

                        //Обработка статуса запроса
                        if (rule.If.IsMatch(context.Response.StatusDescription))
                        {
                            switch (rule.Action)
                            {
                                case "delay":
                                    context.Proxy.Delay += rule.Delay;
                                    return false;
                                case "status":
                                    context.Output.StatusCode = int.Parse(rule.Then);
                                    return true;
                                case "return":
                                    using (StreamWriter writer = new StreamWriter(context.Output.OutputStream))
                                    {
                                        writer.Write(rule.Then);
                                    }
                                    return true;
                                default:
                                    break;
                            }
                        }
                        break;
                    case "body":

                        //Обработка тела ответа
                        if (context.ResponseBody == null)
                        {
                            using (var stream = context.Response.GetResponseStream())
                            {
                                context.ResponseBody = new MemoryStream();
                                stream.CopyTo(context.ResponseBody);
                            }
                        }

                        string body = null;
                        using (StreamReader reader = new StreamReader(context.ResponseBody))
                        {
                            context.ResponseBody.Position = 0;
                            body = reader.ReadToEnd();
                        }

                        if (body != null && rule.If.IsMatch(body))
                        {
                            switch (rule.Action)
                            {
                                case "delay":
                                    context.Proxy.Delay += rule.Delay;
                                    return false;
                                case "status":
                                    context.Output.StatusCode = int.Parse(rule.Then);
                                    return true;
                                case "return":
                                    using (StreamWriter writer = new StreamWriter(context.Output.OutputStream))
                                    {
                                        writer.Write(rule.Then);
                                    }
                                    return true;
                                default:
                                    break;
                            }
                        }
                        break;
                    default:
                        {
                            string header = context.Response.Headers[rule.Parameter];
                            if (header != null && rule.If.IsMatch(header))
                            {
                                switch (rule.Action)
                                {
                                    case "delay":
                                        context.Proxy.Delay += rule.Delay;
                                        return false;
                                    case "status":
                                        context.Output.StatusCode = int.Parse(rule.Then);
                                        return true;
                                    case "return":
                                        using (StreamWriter writer = new StreamWriter(context.Output.OutputStream))
                                        {
                                            writer.Write(rule.Then);
                                        }
                                        return true;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;
                }
            }

            return true;
        }

        private class QueryContext
        {
            /// <summary>
            /// Модель.
            /// </summary>
            public ProxyModel Model { get; set; }

            /// <summary>
            /// Выбранный прокси сервер.
            /// </summary>
            public ProxyInfo Proxy { get; set; }


            /// <summary>
            /// Запрос сформированный для отправки через прокси сервер.
            /// </summary>
            public HttpWebRequest Request { get; set; }

            /// <summary>
            /// Тело запроса.
            /// </summary>
            public MemoryStream RequestBody { get; set; }

            /// <summary>
            /// Ответ от прокси сервера.
            /// </summary>
            public HttpWebResponse Response { get; set; }

            /// <summary>
            /// Тело ответа от ресурса.
            /// </summary>
            public MemoryStream ResponseBody { get; set; }

            /// <summary>
            /// Входящий запрос, который нужно проксировать.
            /// </summary>
            public HttpListenerRequest Input { get; set; }

            /// <summary>
            /// Ответ от сервиса.
            /// </summary>
            public HttpListenerResponse Output { get; set; }

            /// <summary>
            /// Время начала операции.
            /// </summary>
            public TimeSpan TimeBegin { get; set; }

            /// <summary>
            /// Установленный таймаут операции.
            /// </summary>
            public TimeSpan TimeOut { get; set; }

            public QueryContext()
            {

            }
            public QueryContext(HttpListenerContext listenerContext)
            {
                Input = listenerContext.Request;
                Output = listenerContext.Response;


            }
        }
    }
}
