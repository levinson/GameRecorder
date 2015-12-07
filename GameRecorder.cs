using SmartBot.Plugins.API;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SmartBot.Plugins
{
    [Serializable]
    public class GameRecorderSettings : PluginDataContainer
    {
        public bool IncludeLogs { get; set; }
        public bool IncludeMulligan { get; set; }
        public bool IncludeSeeds { get; set; }

        public bool HidePersonalInfo { get; set; }

        public bool LogFriendRequests { get; set; }
        public bool LogWhispers { get; set; }

        public bool ScreenshotBeginTurn { get; set; }
        public bool ScreenshotEndTurn { get; set; }
        public bool ScreenshotChoice { get; set; }
        public bool ScreenshotResimulate { get; set; }
        public bool ScreenshotConcede { get; set; }
        public bool ScreenshotLethal { get; set; }
        public bool ScreenshotVictory { get; set; }
        public bool ScreenshotDefeat { get; set; }

        public GameRecorderSettings()
        {
            Name = "GameRecorder";
            IncludeLogs = true;
            IncludeMulligan = true;
            IncludeSeeds = true;
            HidePersonalInfo = true;
            LogFriendRequests = true;
            LogWhispers = true;
            ScreenshotBeginTurn = true;
            ScreenshotEndTurn = true;
            ScreenshotChoice = true;
            ScreenshotResimulate = true;
            ScreenshotConcede = true;
            ScreenshotLethal = true;
            ScreenshotVictory = true;
            ScreenshotDefeat = true;
        }
    }

    public class GameRecorderPlugin : Plugin
    {
        // Folder to save screenshots for current game
        private string currentGameFolder = null;

        // Keep track of the turn and action number
        private bool gameStarted = false;
        private int turnNum = 0;
        private int actionNum = 0;

        private bool wonLastGame = false;

        private GameRecorderSettings settings = null;

        // Writer for current turn logging
        private StreamWriter turnWriter = null;

        // Log messages received before first turn
        private Queue<String> queuedLogMessages = new Queue<String>();

        // Whispers received
        private Queue<String> whispers = new Queue<String>();

        // Friend requests received
        private Queue<String> friendRequests = new Queue<String>();

        // Stores action for next received screenshot
        private string screenshotAction = null;

        ~GameRecorderPlugin()
        {
            if (turnWriter != null)
            {
                turnWriter.Close();
            }
        }

        public override void OnPluginCreated()
        {
            settings = (GameRecorderSettings)DataContainer;
            Debug.OnLogReceived += OnLogReceived;
            GUI.OnScreenshotReceived += OnScreenshotReceived;
        }

        private void Reset()
        {
            // Reset private variables
            currentGameFolder = null;
            gameStarted = false;
            turnNum = 0;
            actionNum = 0;
            wonLastGame = false;
            turnWriter = null;
            queuedLogMessages.Clear();
            whispers.Clear();
            friendRequests.Clear();
            screenshotAction = null;
        }

        public override void OnGameEnd()
        {
            if (turnWriter != null)
            {
                turnWriter.Close();
                turnWriter = null;
            }

            CopySeeds();
            SaveFriendRequests();
            SaveWhispers();

            if (gameStarted)
            {
                string suffix = wonLastGame ? " WIN" : " LOSS";
                string newGameFolder = currentGameFolder + suffix;
                Directory.Move(currentGameFolder, newGameFolder);
            }

            Reset();
        }

        public override void OnStopped()
        {
            if (turnWriter != null)
            {
                turnWriter.Flush();
            }

            // Overwritten when game ends
            CopySeeds();

            // Appended at end of game
            SaveFriendRequests();
            SaveWhispers();
        }

        public override void OnActionExecute(API.Actions.Action action)
        {
            if (action is API.Actions.ChoiceAction && settings.ScreenshotChoice)
            {
                TakeScreenshot("Choice");
            }
            else if (action is API.Actions.ConcedeAction && settings.ScreenshotConcede)
            {
                TakeScreenshot("Concede");
            }
            else if (action is API.Actions.EndTurnAction && settings.ScreenshotEndTurn)
            {
                TakeScreenshot("EndTurn");
                if (turnWriter != null)
                {
                    turnWriter.Flush();
                }
            }
            else if (action is API.Actions.ResimulateAction && settings.ScreenshotResimulate)
            {
                TakeScreenshot("Resimulate");
            }
        }

        public override void OnTurnBegin()
        {
            turnNum += 1;
            actionNum = 0;

            if (!gameStarted)
            {
                // Create folder for the new game
                string dateTime = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                var board = API.Bot.CurrentBoard;
                var friend = Capitalize(board.FriendClass.ToString());
                var enemy = Capitalize(board.EnemyClass.ToString());
                currentGameFolder = "RecordedGames\\" + dateTime + " " + friend + " vs. " + enemy;
                Directory.CreateDirectory(currentGameFolder);
                gameStarted = true;

                SaveMulligan();
            }

            SetupTurnLogger();

            if (settings.ScreenshotBeginTurn)
            {
                TakeScreenshot("BeginTurn");
            }
        }

        public override void OnLethal()
        {
            if (settings.ScreenshotLethal)
            {
                TakeScreenshot("Lethal");
            }
        }

        public override void OnVictory()
        {
            if (settings.ScreenshotVictory && gameStarted)
            {
                TakeScreenshot("Victory");
            }

            wonLastGame = true;
        }

        public override void OnDefeat()
        {
            if (settings.ScreenshotDefeat && gameStarted)
            {
                TakeScreenshot("Defeat");
            }

            wonLastGame = false;
        }

        public override void OnWhisperReceived(Friend friend, string message)
        {
            if (settings.LogWhispers)
            {
                whispers.Enqueue(GetTimestampPrefix() + friend.GetName() + " says " + message);
            }
        }

        public override void OnFriendRequestReceived(FriendRequest request)
        {
            if (settings.LogFriendRequests)
            {
                friendRequests.Enqueue(GetTimestampPrefix() + request.GetPlayerName());
            }
        }

        private void OnLogReceived(string message)
        {
            if (!settings.IncludeLogs)
            {
                return;
            }

            if (turnWriter != null)
            {
                WriteLogMessage(turnWriter, message);
            }
            else
            {
                queuedLogMessages.Enqueue(message);
            }
        }

        private static void Log(String message)
        {
            Bot.Log("[GameRecorder] " + message);
        }

        private void SaveFriendRequests()
        {
            if (gameStarted && friendRequests.Count > 0)
            {
                using (var writer = new StreamWriter(currentGameFolder + "\\FriendRequests.txt", true))
                {
                    foreach (string request in friendRequests)
                    {
                        writer.WriteLine(request);
                    }
                }

                friendRequests.Clear();
            }
        }

        private void SaveWhispers()
        {
            if (gameStarted && whispers.Count > 0)
            {
                using (var writer = new StreamWriter(currentGameFolder + "\\Whispers.txt", true))
                {
                    foreach (string whisper in whispers)
                    {
                        writer.WriteLine(whisper);
                    }
                }

                whispers.Clear();
            }
        }

        private string Capitalize(String str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        private void OnScreenshotReceived(Bitmap image)
        {
            using (Graphics g = Graphics.FromImage(image))
            {
                if (settings.HidePersonalInfo)
                {
                    SolidBrush grayBrush = new SolidBrush(Color.DimGray);

                    int rectangleWidth = (int)(0.2 * image.Width);
                    int rectangleHeight = (int)(0.12 * image.Height);

                    Rectangle[] grayBoxes = new Rectangle[] {
                        new Rectangle(1, 1, rectangleWidth, rectangleHeight),
                        new Rectangle(1, (int)(0.8 * image.Height), rectangleWidth, rectangleHeight),
                        new Rectangle(1, (int)(0.9 * image.Height), rectangleWidth, rectangleHeight)
                    };

                    g.FillRectangles(grayBrush, grayBoxes);
                }
            }

            // Save the modified image
            string turnAndActionNum = turnNum.ToString("D2") + "-" + actionNum.ToString("D2");
            string fileName = turnAndActionNum + " " + screenshotAction + ".jpg";
            image.Save(currentGameFolder + "\\" + fileName, ImageFormat.Jpeg);
            Log("Saved screenshot: " + fileName);

            actionNum += 1;
        }

        private void TakeScreenshot(String action)
        {
            screenshotAction = action;
            GUI.RequestScreenshotToBitmap();
        }

        private void SetupTurnLogger()
        {
            if (!settings.IncludeLogs)
            {
                return;
            }

            if (turnWriter != null)
            {
                // Close the existing log file
                turnWriter.Close();
            }
            else
            {
                // Write queued log messages now
                using (var writer = new StreamWriter(currentGameFolder + "\\BeginGame_Logs.txt"))
                {
                    foreach (string message in queuedLogMessages)
                    {
                        WriteLogMessage(writer, message);
                    }
                }
            }

            // Setup new log file for this turn
            string outputPath = currentGameFolder + "\\" + "Turn_" + turnNum + "_Logs.txt";
            turnWriter = new StreamWriter(outputPath);
        }

        private string GetTimestampPrefix()
        {
            return "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
        }

        private void WriteLogMessage(StreamWriter writer, string message)
        {
            if (settings.HidePersonalInfo)
            {
                // Ignore auth logs
                if (!message.Contains("[DEBUG][AUTH]"))
                {
                    writer.WriteLine(message);
                }
            }
            else
            {
                writer.WriteLine(GetTimestampPrefix() + message);
            }
        }

        private void SaveMulligan()
        {
            if (!settings.IncludeMulligan)
            {
                return;
            }

            // Find the most recent SmartMulligan log file
            DateTime mostRecentTime = new DateTime(1900, 1, 1);
            string currentMulliganFile = null;
            foreach (string fileName in Directory.GetFiles("MulliganProfiles\\MulliganArchives"))
            {
                FileInfo fileInfo = new FileInfo(fileName);
                DateTime modified = fileInfo.LastWriteTime;

                if (modified > mostRecentTime)
                {
                    mostRecentTime = modified;
                    currentMulliganFile = fileName;
                }
            }

            if (currentMulliganFile == null)
            {
                Log("Failed to find any SmartMulligan files!");
                return;
            }

            // Build mulligan log entries for this game
            var mulliganLogs = new List<String>();
            using (FileStream fs = File.OpenRead(currentMulliganFile))
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("=============================================="))
                    {
                        mulliganLogs.Clear();
                    }
                    else
                    {
                        mulliganLogs.Add(line);
                    }
                }
            }

            using (StreamWriter writer = new StreamWriter(currentGameFolder + "\\SmartMulligan.txt"))
            {
                foreach (string line in mulliganLogs)
                {
                    writer.WriteLine(line);
                }
            }

            Log("Copied smart mulligan output");
        }

        private void CopySeeds()
        {
            if (!settings.IncludeSeeds || !gameStarted)
            {
                return;
            }

            // Find the most recent seed sub-directory
            DateTime mostRecentTime = new DateTime(1900, 1, 1);
            string currentSeedDir = null;

            foreach (string seedDir in Directory.GetDirectories("Seeds"))
            {
                DirectoryInfo info = new DirectoryInfo(seedDir);
                DateTime createdTime = info.LastWriteTime;

                if (createdTime > mostRecentTime)
                {
                    currentSeedDir = seedDir;
                    mostRecentTime = createdTime;
                }
            }

            if (currentSeedDir == null)
            {
                Log("Failed to locate any seeds!");
                return;
            }

            string[] seedFiles = Directory.GetFiles(currentSeedDir, "*.txt");

            // Build set of turns from seed directory
            var turns = new HashSet<int>();
            foreach (string fileName in seedFiles)
            {
                int turnIndex = fileName.IndexOf("Turn_");
                if (turnIndex != -1)
                {
                    try
                    {
                        int turnNum = Int32.Parse(Regex.Match(fileName.Substring(turnIndex), @"\d+").Value);
                        turns.Add(turnNum);
                    }
                    catch
                    {
                        Log("Failed to parse turn number from seed file: " + fileName);
                    }
                }
            }

            // Check if number of turns found is reasonable
            if (turns.Count > this.turnNum)
            {
                Log("No seeds yet for this game");
                return;
            }

            // Copy the seeds to the recorded games directory
            foreach (string fileName in seedFiles)
            {
                FileInfo fileInfo = new FileInfo(fileName);
                File.Copy(fileInfo.FullName, currentGameFolder + "\\" + fileInfo.Name, true);
            }

            Log("Copied " + seedFiles.Length + " seed files");
        }
    }
}
