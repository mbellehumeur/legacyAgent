using System;
using System.Net;
using System.Net.WebSockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FHIRcastAgent
{
    class Program
    {
        private static object consoleLock = new object();
        private const int sendChunkSize = 256;
        private const int receiveChunkSize = 256;
        private const bool verbose = true;
        private static readonly TimeSpan delay = TimeSpan.FromMilliseconds(30000);
        

        static void Main(string[] args)
        {
            //String ssl = "";
            //String hub = "localhost:3000";

            String ssl = "s";
            String hub = "hub-fhircast.azurewebsites.net";

            Console.WriteLine("Starting subscription and event post.");
            String result=subscribe("http"+ssl+"://"+hub +"/api/hub/");
            Console.WriteLine(result);
            Thread.Sleep(1000);

            Connect("ws" + ssl +"://"+hub+"/bind/endpointUID").Wait();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static String subscribe(string url)
        {
            String hub_callback = "na";
            String hub_mode = "subscribe";
            String hub_events = "open-patient-chart";
            String hub_secret = "secret";
            String hub_topic = "DrXRay";
            String hub_lease = "999";
            String hub_channel_type = "websocket";
            String hub_channel_endpoint = "endpointUID";

            String result = "";
            String strPost    = "hub.callback=" + hub_callback + "&hub.mode=" + hub_mode + "&hub.events=" + hub_events;
            strPost = strPost + "&hub.secret=" + hub_secret + "&hub.topic=" + hub_topic + "&hub.lease=" + hub_lease;
            strPost = strPost + "&hub.channel.type=" + hub_channel_type + "&hub.channel.endpoint=" + hub_channel_endpoint;

            StreamWriter myWriter = null;

            HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
            objRequest.Method = "POST";
            objRequest.ContentLength = strPost.Length;
            objRequest.ContentType = "application/x-www-form-urlencoded";

            try
            {
                myWriter = new StreamWriter(objRequest.GetRequestStream());
                myWriter.Write(strPost);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                myWriter.Close();
            }

            HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
            using (StreamReader sr =
               new StreamReader(objResponse.GetResponseStream()))
            {
                result = sr.ReadToEnd();

                // Close and clean up the StreamReader
                sr.Close();
            }
            return result;
        }

        public static async Task Connect(string uri)
        {
            ClientWebSocket webSocket = null;

            try
            {
                webSocket = new ClientWebSocket();
                await webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                await Task.WhenAll(Receive(webSocket), Send(webSocket));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                if (webSocket != null)
                    webSocket.Dispose();
                Console.WriteLine();

                lock (consoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("WebSocket closed.");
                    Console.ResetColor();
                }
            }
        }
        static UTF8Encoding encoder = new UTF8Encoding();

        private static async Task Send(ClientWebSocket webSocket)
        {
 
            String EventArgs= "{\"timestamp\":\"2018 - 12 - 04T11: 22:53.601Z\",\"id\":\"event-id-igs1vu3ja6\",\"cast-session\":\"DrXRay\",\"event\":{\"hub.topic\":\"DrXRay\",\"hub.event\":\"open-patient-chart\",\"context\":{\"key\": \"patient\",\"resource\":{\"resourceType\": \"Patient\",\"id\": \"ewUbXT9RWEbSj5wPEdgRaBw3\",\"identifier\": [{\"system\": \"urn:oid:1.2.840.114350\",\"value\": \"185444\"},{\"system\": \"urn:oid:1.2.840.114350.1.13.861.1.7.5.737384.27000\",\"value\": \"2667\"}]}}}}";

            byte[] buffer = encoder.GetBytes(EventArgs);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

            while (webSocket.State == WebSocketState.Open)
            {
                LogStatus(false, buffer, buffer.Length);
                await Task.Delay(delay);
            }
        }

        private static async Task Receive(ClientWebSocket webSocket)
        {
            byte[] buffer = new byte[receiveChunkSize];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    LogStatus(true, buffer, result.Count);
                }
            }
        }

        private static void LogStatus(bool receiving, byte[] buffer, int length)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = receiving ? ConsoleColor.Green : ConsoleColor.Gray;
                //Console.WriteLine("{0} ", receiving ? "Received" : "Sent");

                if (verbose)
                    Console.WriteLine(encoder.GetString(buffer));

                Console.ResetColor();
            }
        }
    }
}

