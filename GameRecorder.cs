using SmartBot.Plugins.API;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace SmartBot.Plugins
{
    public class ImageFormatItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            ItemCollection formats = new ItemCollection();
            var formatConverter = new ImageFormatConverter();
            foreach (var encoder in ImageCodecInfo.GetImageEncoders())
            {
                formats.Add(encoder.FormatDescription);
            }
            return formats;
        }
    }

    [Serializable]
    public class GameRecorderSettings : PluginDataContainer
    {
        [ItemsSource(typeof(ImageFormatItemsSource))]
        public string ImageFormat { get; set; }
        public int ImageQuality { get; set; }
        public bool ImageResizeEnabled { get; set; }
        public int ImageResizeHeight { get; set; }
        public int ImageResizeWidth { get; set; }

        public bool IncludeLogs { get; set; }
        public bool IncludeMulligan { get; set; }
        public bool IncludeSeeds { get; set; }

        public bool HidePersonalInfo { get; set; }

        public bool LogFriendRequests { get; set; }
        public bool LogWhispers { get; set; }

        public bool ScreenshotMulligan { get; set; }
        public bool ScreenshotBeginTurn { get; set; }
        public bool ScreenshotEndTurn { get; set; }
        public bool ScreenshotChoice { get; set; }
        public bool ScreenshotResimulate { get; set; }
        public bool ScreenshotConcede { get; set; }
        public bool ScreenshotLethal { get; set; }
        public bool ScreenshotVictory { get; set; }
        public bool ScreenshotDefeat { get; set; }

        public bool DeleteWins { get; set; }

        public GameRecorderSettings()
        {
            Name = "GameRecorder";
            this.ImageFormat = "Jpeg";
            ImageQuality = 50;
            ImageResizeEnabled = false;
            ImageResizeHeight = 600;
            ImageResizeWidth = 800;
            IncludeLogs = true;
            IncludeMulligan = true;
            IncludeSeeds = true;
            HidePersonalInfo = true;
            LogFriendRequests = true;
            LogWhispers = true;
            ScreenshotMulligan = true;
            ScreenshotBeginTurn = true;
            ScreenshotEndTurn = true;
            ScreenshotChoice = true;
            ScreenshotResimulate = true;
            ScreenshotConcede = true;
            ScreenshotLethal = true;
            ScreenshotVictory = true;
            ScreenshotDefeat = true;
            DeleteWins = false;
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

        private ImageFormatConverter imageFormatConverter = new ImageFormatConverter();

        ~GameRecorderPlugin()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (turnWriter != null)
            {
                turnWriter.Close();
                turnWriter = null;
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
            EndGame();
        }

        private void EndGame()
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
                if (settings.DeleteWins && wonLastGame)
                {
                    Directory.Delete(currentGameFolder, true);
                }
                else
                {
                    string suffix = wonLastGame ? " WIN" : " LOSS";
                    string newGameFolder = currentGameFolder + suffix;
                    Directory.Move(currentGameFolder, newGameFolder);
                }
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

        public override void OnHandleMulligan(List<Card.Cards> choices, Card.CClass enemyClass, Card.CClass friendClass)
        {
            // Check if OnGameEnd was never fired
            if (gameStarted)
            {
                EndGame();
            }

            CreateGameFolder(friendClass, enemyClass);
        }

        public override void OnMulliganCardsReplaced(List<Card.Cards> replacedCards)
        {
            if (settings.ScreenshotMulligan)
            {
                TakeScreenshot("Mulligan");
            }

            SaveMulligan();
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
                var board = API.Bot.CurrentBoard;                
                CreateGameFolder(board.FriendClass, board.EnemyClass);
            }

            SetupTurnLogger();

            if (settings.ScreenshotBeginTurn)
            {
                TakeScreenshot("BeginTurn");
            }
        }

        public override void OnTurnEnd()
        {
            if (settings.ScreenshotEndTurn)
            {
                TakeScreenshot("EndTurn");
                if (turnWriter != null)
                {
                    turnWriter.Flush();
                }
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
            if (settings.ScreenshotVictory)
            {
                TakeScreenshot("Victory");
            }

            wonLastGame = true;
        }

        public override void OnDefeat()
        {
            if (settings.ScreenshotDefeat)
            {
                TakeScreenshot("Defeat");
            }

            wonLastGame = false;
        }

        public override void OnConcede()
        {
            if (settings.ScreenshotConcede)
            {
                TakeScreenshot("Concede");
            }
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

        private void CreateGameFolder(Card.CClass friendClass, Card.CClass enemyClass)
        {
            if (!gameStarted)
            {
                // Create folder for the new game
                string dateTime = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                var friend = Capitalize(friendClass.ToString());
                var enemy = Capitalize(enemyClass.ToString());
                currentGameFolder = "RecordedGames\\" + dateTime + " " + friend + " vs. " + enemy;
                Directory.CreateDirectory(currentGameFolder);
                gameStarted = true;
            }
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

        private Bitmap ResizeImage(Bitmap original, int newWidth, int newHeight)
        {
            Bitmap scaledImage = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(scaledImage))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                // Draw the scaled image onto the canvas
                g.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            return scaledImage;
        }

        private void OnScreenshotReceived(Bitmap image)
        {
            if (!gameStarted)
            {
                return;
            }

            if (settings.ImageResizeEnabled &&
                (image.Height > settings.ImageResizeHeight || image.Width > settings.ImageResizeWidth))
            {
                // Compute new width and height
                int width = image.Width < settings.ImageResizeWidth ? image.Width : settings.ImageResizeWidth;
                int height = image.Height < settings.ImageResizeHeight ? image.Height : settings.ImageResizeHeight;

                // Resize the image
                image = ResizeImage(image, width, height);
            }

            using (Graphics g = Graphics.FromImage(image))
            {
                if (settings.HidePersonalInfo)
                {
                    SolidBrush grayBrush = new SolidBrush(Color.DimGray);

                    int rectangleWidth = (int)(0.2 * image.Width);
                    int rectangleHeight = (int)(0.12 * image.Height);

                    Rectangle[] grayBoxes = new Rectangle[] {
                        new Rectangle(0, 0, rectangleWidth, rectangleHeight),
                        new Rectangle(0, (int)(0.8 * image.Height), rectangleWidth, rectangleHeight),
                        new Rectangle(0, (int)(0.9 * image.Height), rectangleWidth, rectangleHeight)
                    };

                    g.FillRectangles(grayBrush, grayBoxes);
                }
            }

            // Build the encoder params
            var format = (ImageFormat)imageFormatConverter.ConvertFromString(settings.ImageFormat);
            var encoderParams = GetEncoderParams(settings.ImageQuality);

            // Build screenshot file name
            string turnAndAction = turnNum.ToString("D2") + "-" + actionNum.ToString("D2");
            string fileExtension = settings.ImageFormat.Equals("JPEG") ? "jpg" : settings.ImageFormat.ToLower();
            string fileName = turnAndAction + " " + screenshotAction + "." + fileExtension;

            // Save with given format and quality
            image.Save(currentGameFolder + "\\" + fileName, GetEncoder(format), encoderParams);
            Log("Saved screenshot: " + fileName);

            actionNum += 1;
        }

        private EncoderParameters GetEncoderParams(int quality)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            return encoderParams;
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void TakeScreenshot(String action)
        {
            if (gameStarted)
            {
                screenshotAction = action;
                GUI.RequestScreenshotToBitmap();
            }
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

            if (currentMulliganFile == null || DateTime.Now.AddMinutes(-1).CompareTo(mostRecentTime) > 0)
            {
                Log("Failed to find a current SmartMulligan file!");
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

            if (currentSeedDir == null || DateTime.Now.AddMinutes(-1).CompareTo(mostRecentTime) > 0)
            {
                Log("Failed to locate current seeds!");
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
