using JenkinsNET.Models;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;


using System.Threading;
using System.Threading.Tasks;


namespace JenkinsNET.Internal
{
    internal class JenkinsHttpCommand
    {
        public string Url {get; set;}
        public string UserName {get; set;}
        public string Password {get; set;}
        public JenkinsCrumb Crumb {get; set;}
        public Action<HttpWebRequest> OnWrite {get; set;}
        public Action<HttpWebResponse> OnRead {get; set;}

    
        public Func<HttpWebRequest, CancellationToken, Task> OnWriteAsync {get; set;}
        public Func<HttpWebResponse, CancellationToken, Task> OnReadAsync {get; set;}
    


        public void Run()
        {
            var request = CreateRequest();

            OnWrite?.Invoke(request);

            using (var response = (HttpWebResponse)request.GetResponse()) {
                OnRead?.Invoke(response);
            }
        }

        public async Task RunAsync(CancellationToken token = default)
        {
            var request = CreateRequest();

            if (OnWriteAsync != null) await OnWriteAsync.Invoke(request, token);

            using (token.Register(() => request.Abort(), false))
            using (var response = (HttpWebResponse)await request.GetResponseAsync()) {
                token.ThrowIfCancellationRequested();

                if (OnReadAsync != null) await OnReadAsync.Invoke(response, token);
            }
        }
    

        private HttpWebRequest CreateRequest()
        {
            var _url = Url;
            var hasUser = !string.IsNullOrEmpty(UserName);
            var hasPass = !string.IsNullOrEmpty(Password);

            var request = (HttpWebRequest)WebRequest.Create(_url);
            request.UserAgent = "Jenkins.NET Client";
            request.AllowAutoRedirect = true;
            request.KeepAlive = true;
            // 设置短一点超时时间,毫秒
            request.Timeout = 1500;
            if (Crumb != null)
                request.Headers.Add(Crumb.CrumbRequestField, Crumb.Crumb);

            if (hasUser && hasPass) {
                request.PreAuthenticate = true;
                request.UseDefaultCredentials = false;

                var data = Encoding.UTF8.GetBytes($"{UserName}:{Password}");
                var basicAuthToken = Convert.ToBase64String(data);
                request.Headers["Authorization"] = $"Basic {basicAuthToken}";
            }

            return request;
        }

        protected XDocument ReadXml(HttpWebResponse response)
        {
            using (var stream = response.GetResponseStream()) {
                if (stream == null) return null;

                using (var reader = new StreamReader(stream)) {
                    var xml = reader.ReadToEnd();
                    xml = RemoveXmlDeclaration(xml);
                    return XDocument.Parse(xml);
                }
            }
        }

        protected void WriteXml(HttpWebRequest request, XNode node)
        {
            var xmlSettings = new XmlWriterSettings {
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = false,
            };

            using (var stream = request.GetRequestStream())
            using (var writer = XmlWriter.Create(stream, xmlSettings)) {
                node.WriteTo(writer);
            }
        }

    
        protected async Task<XDocument> ReadXmlAsync(HttpWebResponse response)
        {
            using (var stream = response.GetResponseStream()) {
                if (stream == null) return null;

                using (var reader = new StreamReader(stream)) {
                    var xml = await reader.ReadToEndAsync();
                    xml = RemoveXmlDeclaration(xml);
                    return XDocument.Parse(xml);
                }
            }
        }

        protected async Task WriteXmlAsync(HttpWebRequest request, XNode node, CancellationToken token = default)
        {
            var xmlSettings = new XmlWriterSettings {
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = false,
            };

            using (token.Register(request.Abort, false))
            using (var stream = await request.GetRequestStreamAsync())
            using (var writer = XmlWriter.Create(stream, xmlSettings)) {
                token.ThrowIfCancellationRequested();

                node.WriteTo(writer);
            }
        }
    

        protected static Encoding TryGetEncoding(string name, Encoding fallback)
        {
            try {
                return Encoding.GetEncoding(name);
            }
            catch {
                return fallback;
            }
        }

        private static string RemoveXmlDeclaration(string xml)
        {
            const string pattern = @"<\?xml[^\>]*\?>";
            return Regex.Replace(xml, pattern, string.Empty);
        }
    }
}
