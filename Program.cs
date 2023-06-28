using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SteamInviteSpammer
{
    class Program
    {
        static async Task Main()
        {
            // Creates credentials file if there isn't any
            bool isCredentialsFile = false;
            string[] filesInDir = Directory.GetFiles(Directory.GetCurrentDirectory());
            for (int i = 0; i < filesInDir.Length; i++)
            {
                if (filesInDir[i].Split(GameInviteSpammer.credentialsFileName).Length >= 2)
                {
                    isCredentialsFile = true;
                    break;
                }
            }
            if (!isCredentialsFile)
            {
                File.WriteAllText(GameInviteSpammer.credentialsFileName, "{}");
            }

            // App introduction
            Console.WriteLine("Welcome to Steam Lobby Invite Spammer!");
            Console.WriteLine("Use at your own risk!!!");
            Console.WriteLine("Low chance of community ban at max, but even if it happens it will be most likely temporary.");
            Console.WriteLine("Just don't be a jerk and spam everybody in your friends list non-stop and you will be fine.");
            Console.WriteLine("Also note the credentials in the file are not encrypted for now, delete the file if you care about this.");
            Console.WriteLine("Have fun!");

            Console.WriteLine("\n"); // 2 New lines to space out the app

            // Credentials setup
            bool isCredentials;
            Dictionary<string, string> credentials = GameInviteSpammer.LoadCredentials();
            if (credentials.Count == 0)
            {
                Console.WriteLine("No credentials stored, please enter them.");
                isCredentials = false;
            }
            else
            {
                isCredentials = true;
            }

            while (!isCredentials)
            {
                bool result = GameInviteSpammer.ChangeCredentials();
                if (result)
                {
                    Console.WriteLine("\n"); // 2 New lines to space out the app
                    break;
                }
            }



            // Loads the credentials
            credentials = GameInviteSpammer.LoadCredentials();
            GameInviteSpammer gms = new(credentials["steam_username"], credentials["steam_password"], credentials["web_api_key"]);

            // Login to steam dialog
            while (!gms.isRunning || gms.isSteamGuard)
            {
                Console.WriteLine("1 = Login to Steam | 2 = Change Credentials | 0 = Exit");
                string userInput = Console.ReadLine();

                if (userInput.Equals("1"))
                {
                    gms.SetupSteamConnection();
                    break;
                }
                else if (userInput.Equals("2"))
                {
                    GameInviteSpammer.ChangeCredentials();
                    credentials = GameInviteSpammer.LoadCredentials();
                    gms = new(credentials["steam_username"], credentials["steam_password"], credentials["web_api_key"]);
                }
                else if (userInput.Equals("0"))
                {
                    Environment.Exit(0);
                }
            }

            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                if (!gms.isSteamGuard)
                {
                    Console.WriteLine("Waiting 15 seconds for everything to be ready!");
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                    break;
                }
                else if (gms.isLoggedOn)
                {
                    Console.WriteLine("Waiting 10 seconds for everything to be ready!");
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    break;
                }
            }


            Console.WriteLine("\n"); // 2 New lines to space out the app
            Console.WriteLine("Successfully connected and authenticated to the steam network."); // Let's user know everything is ready

            ulong friendID = 0;
            // Main menu of the app
            while (true)
            {
                Console.WriteLine("1 = Send One Invite | 2 = Send Multiple Invites | 3 = Send Invites On Enter");
                Console.WriteLine("4 = Choose a friend ID as global | 0 = Exit");
                string userInput = Console.ReadLine();
                if (userInput.Equals("1"))
                {
                    if (friendID != 0)
                    {
                        await gms.SendOneLobbyInvite(friendID);
                    }
                    else
                    {
                        bool conversionSuccess = false;
                        while (true)
                        {
                            Console.WriteLine("Steam id 64 of your friend: ");
                            userInput = Console.ReadLine();
                            ulong tempFriendID;
                            conversionSuccess = ulong.TryParse(userInput, out tempFriendID);
                            if (conversionSuccess)
                            {
                                await gms.SendOneLobbyInvite(tempFriendID);
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Invalid input string!");
                            }
                        }
                    }
                }
                else if (userInput.Equals("2"))
                {
                    if (friendID != 0)
                    {
                        int invitesNumber;
                        bool conversionSuccess;
                        while (true)
                        {
                            Console.WriteLine("Number of Invites: ");
                            userInput = Console.ReadLine();
                            conversionSuccess = int.TryParse(userInput, out invitesNumber);
                            if (conversionSuccess)
                            {
                                await gms.SendLobbyInvites(friendID, invitesNumber);
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Invalid input string!");
                            }
                        }
                    }
                    else
                    {
                        ulong tempFriendID;
                        while (true)
                        {
                            bool conversionSuccess = false;
                            Console.WriteLine("Steam id 64 of your friend: ");
                            userInput = Console.ReadLine();
                            conversionSuccess = ulong.TryParse(userInput, out tempFriendID);
                            if (conversionSuccess)
                            {
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Invalid input string!");
                            }
                        }
                        while (true)
                        {
                            int invitesNumber;
                            bool conversionSuccess = false;
                            Console.WriteLine("Number of Invites: ");
                            userInput = Console.ReadLine();
                            conversionSuccess = int.TryParse(userInput, out invitesNumber);
                            if (conversionSuccess)
                            {
                                await gms.SendLobbyInvites(tempFriendID, invitesNumber);
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Invalid input string!");
                            }
                        }
                    }
                }
                else if (userInput.Equals("3"))
                {
                    uint gameID = await gms.GetCurrentGameID();
                    ulong lobbyID = await gms.GetCurrentSteamLobbyID();
                    Console.WriteLine("Game id: " + gameID);
                    Console.WriteLine("Lobby id: " + lobbyID);

                    if (friendID != 0)
                    {
                        while (true)
                        {
                            ConsoleKeyInfo key = Console.ReadKey();
                            if (key.Key.Equals(ConsoleKey.D0))
                            {
                                break;
                            }
                            gms.SendOneLobbyInvite(friendID, gameID, lobbyID);
                        }
                    }
                    else
                    {
                        bool conversionSuccess = false;
                        while (true)
                        {
                            Console.WriteLine("Steam id 64 of your friend: ");
                            userInput = Console.ReadLine();
                            ulong tempFriendID;
                            conversionSuccess = ulong.TryParse(userInput, out tempFriendID);
                            if (conversionSuccess)
                            {
                                while (true)
                                {
                                    ConsoleKeyInfo key = Console.ReadKey();
                                    if (key.Key.Equals(ConsoleKey.D0))
                                    {
                                        break;
                                    }
                                    gms.SendOneLobbyInvite(tempFriendID, gameID, lobbyID);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid input string!");
                            }
                        }
                    }
                }
                else if (userInput.Equals("4"))
                {
                    ulong tempFriendID;
                    while (true)
                    {
                        bool conversionSuccess = false;
                        Console.WriteLine("Steam id 64 of your friend: ");
                        userInput = Console.ReadLine();
                        conversionSuccess = ulong.TryParse(userInput, out tempFriendID);
                        if (conversionSuccess)
                        {
                            friendID = tempFriendID;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Invalid input string!");
                        }
                    }
                }
                else if (userInput.Equals("0"))
                {
                    gms.SteamDisconnect();
                    Environment.Exit(0);
                }
            }
        }
    }
}


// TODO: add more info (like press 0 to exit)
// TODO: add more checks like if user is in lobby
// TODO: check if friend id is valid
// TODO: Implement more go back
// TODO: Tell user on invite the game and lobby id.
// TODO: Allow user to put shortcut on multiple people