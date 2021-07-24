using Fiddler;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace ConsoleApp5
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "Kebs's Instant Gifting";
            // Detect when the app is closed so we can delete the proxy
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            // Find League process
            Process[] processesByName = Process.GetProcessesByName("LeagueClientUx");
            while (processesByName.Length == 0)
            {
                processesByName = Process.GetProcessesByName("LeagueClientUx");
                Console.WriteLine("Looking for league...");
                System.Threading.Thread.Sleep(10000);
                Console.Clear();
            }
            Console.WriteLine("League found!\nChecking certificates...");

            // Get port and auth token from command line
            string cmdLine;
            using (FileStream fileStream = File.Open(Path.Combine(Path.GetDirectoryName(processesByName[0].MainModule.FileName), "lockfile"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                cmdLine = new StreamReader((Stream)fileStream).ReadToEnd();
            string[] strArray = cmdLine.Split(new string[1] { ":" }, StringSplitOptions.None);
            int port = Convert.ToInt32(strArray[2]);
            string leagueToken = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("riot:" + strArray[3]));

            // Send a request to get all friends
            string url = "https://127.0.0.1:" + port;
            url += "/lol-chat/v1/friends";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("Authorization", "Basic " + leagueToken);
            request.Accept = "application/json";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) LeagueOfLegendsClient/11.13.382.1241 (CEF 74) Safari/537.36";
            request.Method = "GET";
            request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();
            string responseString = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();

            // Build a body for store's friend list with changed friend's date time
            List<FriendList> friends = JsonConvert.DeserializeObject<List<FriendList>>(responseString);
            StoreFriends storeFriends = new StoreFriends();
            var rand = new Random();
            friends.ForEach(delegate (FriendList friend)
            {
                Friend templateFriend = new Friend();
                templateFriend.friendsSince = "2019-" + rand.Next(1, 13) + "-" + rand.Next(1, 28) + " " + rand.Next(1, 24) + ":" + rand.Next(1, 60) + ":" + rand.Next(1, 60);
                templateFriend.oldFriends = true;
                templateFriend.nick = friend.gameName;
                templateFriend.summonerId = friend.summonerId;
                storeFriends.friends.Add(templateFriend);
            });

            // Check for Fiddler certificates to decrypt https
            if (!Fiddler.CertMaker.rootCertExists())
            {
                if (!Fiddler.CertMaker.createRootCert())
                {
                    throw new Exception("Unable to create cert for FiddlerCore.");
                }
            }
            if (!Fiddler.CertMaker.rootCertIsTrusted())
            {
                if (!Fiddler.CertMaker.trustRootCert())
                {
                    throw new Exception("Unable to install FiddlerCore's cert.");
                }
            }

            // Ignore invalid certs, that league client sends
            FiddlerApplication.OnValidateServerCertificate += (sender, ea) =>
            {
                if (ea.CertificatePolicyErrors != System.Net.Security.SslPolicyErrors.None)
                {
                    ea.ValidityState = CertificateValidity.ForceValid;
                }
            };

            // For decrypting https
            FiddlerApplication.BeforeRequest += session =>
            {
                session["x-OverrideSslProtocols"] = "ssl3;tls1.0;tls1.1;tls1.2";
            };

            // For changing response body
            FiddlerApplication.ResponseHeadersAvailable += session =>
            {
                if (session.url.Contains("/storefront/v3/gift/friends"))
                {
                    session.bBufferResponse = true;
                }
            };

            // Change response body to ours
            FiddlerApplication.BeforeResponse += session =>
            {
                if (session.url.Contains("/storefront/v3/gift/friends"))
                {
                    session.bBufferResponse = true;
                    session.utilDecodeResponse();
                    //string response = session.GetResponseBodyAsString();
                    //response = response.Replace("\"oldFriends\":false", "\"oldFriends\":true");
                    //session.utilSetResponseBody(response);
                    session.utilSetResponseBody(JsonConvert.SerializeObject(storeFriends));
                }
            };

            FiddlerApplication.AfterSessionComplete += session =>
            {
                //Console.WriteLine(session.fullUrl);
            };

            // Start proxy server
            FiddlerApplication.Startup(8888, true, true);

            Console.Clear();
            Console.WriteLine("Everything is ready. Open Store -> Gifting Center");

            // Wait for input
            Console.ReadLine();

            // Cleanup
            if (FiddlerApplication.IsStarted())
            {
                if (CertMaker.rootCertExists())
                {
                    CertMaker.removeFiddlerGeneratedCerts(true);
                }
                FiddlerApplication.Shutdown();
            }
        }

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);

        private static EventHandler _handler;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            Console.Clear();
            Console.WriteLine("Exiting...\nIf you didn't accept the popup in time, launch and exit the app again.");
            Console.WriteLine("If nothing happens and you have internet problem, go into Window's settings and disable proxy server");
            // Cleanup
            //if (CertMaker.rootCertExists())
            // {
            CertMaker.removeFiddlerGeneratedCerts(true);
            // }

            // if (FiddlerApplication.IsStarted())
            //  {
            FiddlerApplication.Shutdown();
            // }

            System.Threading.Thread.Sleep(8000);
            Environment.Exit(-1);

            return true;
        }
    }

    public class FriendList
    {
        public string gameName { get; set; }
        public object summonerId { get; set; }
    }

    public class Config
    {
        public int giftingHextechMaxDailyGiftsSend = 10;
        public int giftingHextecMaxDailyGiftsReceive = 10;
        public int giftingItemMaxDailyGiftsReceive = 10;
        public int giftingItemMaxDailyGiftsSend = 10;
        public int giftingItemMinLevelSend = 10;
        public int giftingRestrictionFlagRioter = 1000000;
        public int giftingRpMaxDailyGiftsReceive = 5;
        public int giftingRpMaxDailyGiftsSend = 5;
        public int giftingRpMinLevelSend = 15;
        public int recipientLevelLimitItem = 1;
        public int recipientLevelLimitRp = 1;
        public bool requiresIdentityVerification = false;
    }

    public class Friend
    {
        public string friendsSince { get; set; }
        public string nick { get; set; }
        public bool oldFriends { get; set; }
        public object summonerId { get; set; }
    }

    public class StoreFriends
    {
        public Config config = new Config();
        public List<Friend> friends = new List<Friend>();
    }
}