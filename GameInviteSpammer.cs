using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.Http;
using SteamKit2;

namespace SteamInviteSpammer
{
    public class GameInviteSpammer
    {
        private SteamClient steamClient;
        private CallbackManager manager;
        private SteamUser steamUser;
        private SteamFriends steamFriends;
        private SteamMatchmaking steamMatchmaking;
        public static string credentialsFileName = "credentials.json";
        public bool isRunning;
        private string username;
        private string password;
        private string webApiKey;
        public bool isSteamGuard = true;
        public bool isLoggedOn = false;
        private string authCode;
        private string twoFactorAuth;
        private const string lobbyIdRequestUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=[api_key]&format=json&steamids=[steam_id]";

        public GameInviteSpammer(string username, string password, string webApiKey)
        {
            this.username = username;
            this.password = password;
            this.webApiKey = webApiKey;
        }

        public static Dictionary<string, string> LoadCredentials()
        {
            string credentialsString = File.ReadAllText(credentialsFileName);
            Dictionary<string, string> credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(credentialsString);
            return credentials;
        }
        public static bool ChangeCredentials()
        {
            Console.WriteLine("Input '0' at any time in order to return back and not save any changes!");
            Console.WriteLine("Steam account username: ");
            string steamUsername = Console.ReadLine();
            if (steamUsername.Equals("0"))
            {
                return false;
            }

            Console.WriteLine("\nSteam account password: ");
            string steamPassword = Console.ReadLine();
            if (steamPassword.Equals("0"))
            {
                return false;
            }

            Console.WriteLine("\nYour web api key is also needed for this app to work, more specifically" +
                        " we need it for getting the steamID of the lobby.");
            Console.WriteLine("In order to get yours you need to go to https://steamcommunity.com/dev/apikey" +
                        " put a random string for your domain and then copy that key and paste it here.");
            string steamWebApiKey = Console.ReadLine();
            if (steamWebApiKey.Equals("0"))
            {
                return false;
            }

            Console.WriteLine("\nHere is your input: ");
            Console.WriteLine($"Steam username: {steamUsername}");
            Console.WriteLine($"Steam password: {steamPassword}");
            Console.WriteLine($"Steam web api key: {steamWebApiKey}");
            Console.WriteLine("Are you sure you want to save this information? (y/n)");
            string userConfirmation = Console.ReadLine();
            if (!userConfirmation.ToLower().Equals("y"))
            {
                return false;
            }

            Dictionary<string, string> credentials = new()
            {
                { "steam_username", steamUsername },
                { "steam_password", steamPassword },
                { "web_api_key", steamWebApiKey }
            };

            JsonSerializerOptions options = new();
            options.WriteIndented = true;
            string credentialsString = JsonSerializer.Serialize(credentials, typeof(Dictionary<string, string>), options);
            File.WriteAllText(credentialsFileName, credentialsString);

            File.Delete("sentry.bin"); // deletes old 2fa data

            return true;
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine($"Connected to Steam! Logging in '{username}'...");

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                // if we have a saved sentry file, read and sha-1 hash it
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                Password = password,
                // this 3 values are null by default for the first login attempt
                AuthCode = authCode,
                TwoFactorCode = twoFactorAuth,
                SentryFileHash = sentryHash,
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {

            // after receiving an AccountLogonDenied, we'll be disconnected from steam
            // so after we read an authcode from the user, we need to reconnect to begin the logon flow again

            if (isRunning)
            {
                Console.WriteLine("Disconnected from Steam!");
            }
            else
            {
                Console.WriteLine("Disconnected from Steam, reconnecting in 5...");
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));
            steamClient.Connect();
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuardEmail = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuardEmail || is2FA)
            {
                Console.WriteLine("This account is SteamGuard protected!");
                isSteamGuard = true;

                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticator app:");
                    twoFactorAuth = Console.ReadLine();
                }
                else
                {
                    Console.Write($"Please enter the auth code sent to the email at {callback.EmailDomain}: ");
                    authCode = Console.ReadLine();
                }

                isSteamGuard = false;
                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");

                isRunning = false;
                return;
            }

            Console.WriteLine("Successfully logged on!");
            isLoggedOn = true;
            // at this point, we should be able to perform actions on Steam
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine($"Logged off of Steam: {callback.Result}");
        }

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            // before being able to interact with friends, you must wait for the account info callback
            // this callback is posted shortly after a successful logon
            // at this point, we can go online on friends, so lets do that
            steamFriends.SetPersonaState(EPersonaState.Online);
        }

        private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");

            // write out our sentry file
            // ideally we'd want to write to the filename specified in the callback
            // but then this would require more code to find the correct sentry file to read during logon
            // for the sake of simplicity, I'll just use "sentry.bin"

            int fileSize;
            byte[] sentryHash;
            using (FileStream fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = SHA1.Create())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });

            Console.WriteLine("Done!");
        }

        private void CallBackHandler()
        {
            while (isRunning)
            {
                // in order for the callbacks to get routed, they need to be handled by the manager
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        public void SetupSteamConnection()
        {
            // gets steam client instance
            SteamConfiguration configuration = SteamConfiguration.Create(b => b.WithProtocolTypes(ProtocolTypes.Tcp));
            steamClient = new(configuration);

            //create the callback manager
            manager = new CallbackManager(steamClient);

            // gets SteamUser, SteamFriends and SteamMatchmaking handler
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();
            steamMatchmaking = steamClient.GetHandler<SteamMatchmaking>();

            // registers callbacks
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            // this callback is triggered when the steam servers wish for the client to store the sentry file
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);

            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            //manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);

            isRunning = true; // at this point it should be fully running

            Console.WriteLine("Connecting to Steam...");

            // initiate the connection
            steamClient.Connect();


            Task.Run(() => CallBackHandler());
            //Thread.Sleep(5); // Waiting 3 seconds in order to get response on steam guard.

            /*
            if (!isSteamGuard)
            {
                Console.WriteLine("Waiting 10 seconds for everything to be ready...");
                Thread.Sleep(TimeSpan.FromSeconds(10));
                Console.WriteLine("Done, fully connected to the steam network!");
            }*/
        }

        public void SteamDisconnect()
        {
            steamClient.Disconnect();
        }

        public async Task<ulong> GetCurrentSteamLobbyID()
        {
            string requestUrl = lobbyIdRequestUrl.Replace("[api_key]", webApiKey).Replace("[steam_id]", steamClient.SteamID.ConvertToUInt64().ToString());

            string responseContent;
            using (HttpClient client = new())
            {
                HttpResponseMessage response = await client.GetAsync(requestUrl);
                responseContent = await response.Content.ReadAsStringAsync();
            }

            if (responseContent.Split("\"lobbysteamid\":\"").Length >= 2)
            {
                string lobbyIdString = responseContent.Split("\"lobbysteamid\":\"")[1].Split("\",")[0];
                ulong lobbyId = ulong.Parse(lobbyIdString);
                return lobbyId;
            }
            else
            {
                return 0;
            }
        }

        public async Task<uint> GetCurrentGameID()
        {
            string requestUrl = lobbyIdRequestUrl.Replace("[api_key]", webApiKey).Replace("[steam_id]", steamClient.SteamID.ConvertToUInt64().ToString());

            string responseContent;
            using (HttpClient client = new())
            {
                HttpResponseMessage response = await client.GetAsync(requestUrl);
                responseContent = await response.Content.ReadAsStringAsync();
            }

            if (responseContent.Split("\"gameid\":\"").Length >= 2)
            {
                string gameIdString = responseContent.Split("\"gameid\":\"")[1].Split("\",")[0];
                uint gameID = uint.Parse(gameIdString);
                return gameID;
            }
            else
            {
                return 0;
            }

        }

        public async Task SendOneLobbyInvite(ulong friendID)
        {
            uint gameID = await GetCurrentGameID();
            ulong lobbyID = await GetCurrentSteamLobbyID();

            steamMatchmaking.InviteToLobby(gameID, lobbyID, friendID);
            Console.WriteLine("Invite sent!");
        }

        public void SendOneLobbyInvite(ulong friendID, uint gameID, ulong lobbyID)
        {
            steamMatchmaking.InviteToLobby(gameID, lobbyID, friendID);
            Console.WriteLine("Invite sent!");
        }

        public async Task SendLobbyInvites(ulong friendID, int invitesNumber)
        {
            uint gameID = await GetCurrentGameID();
            ulong lobbyID = await GetCurrentSteamLobbyID();

            for (int i = 0; i < invitesNumber; i++)
            {
                steamMatchmaking.InviteToLobby(gameID, lobbyID, friendID);
                Console.WriteLine("Invite sent!");
                Thread.Sleep(500);
            }
        }

        public void SendLobbyInvites(ulong friendID, int invitesNumber, uint gameID, ulong lobbyID)
        {
            for (int i = 0; i < invitesNumber; i++)
            {
                steamMatchmaking.InviteToLobby(gameID, lobbyID, friendID);
                Console.WriteLine("Invite sent!");
                Thread.Sleep(500);
            }
        }
    }
}