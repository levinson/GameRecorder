using SmartBot.Plugins.API;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using SmartBot.Plugins.API.Actions;

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

    public class LocaleItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            ItemCollection locales = new ItemCollection();
            foreach (string lang in new string[] {
                "deDE", "enGB", "enUS", "esES", "esMX", "frFR", "itIT",
                "jaJP", "koKR", "plPL", "ptBR", "ruRU", "zhCN", "zhTW" })
            {
                locales.Add(lang);
            }
            return locales;
        }
    }

    public class GameModesSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            ItemCollection gameModes = new ItemCollection();
            gameModes.Add("All");
            gameModes.Add("Ranked");
            gameModes.Add("Unranked");
            gameModes.Add("Arena");
            gameModes.Add("Arena & Ranked");
            gameModes.Add("Arena & Unranked");
            gameModes.Add("Ranked & Unranked");
            return gameModes;
        }
    }

    public class DeleteGamesSource : IItemsSource
    {
        public const string NEVER = "Never";
        public const string ONE_DAY = "After one day";
        public const string TWO_DAYS = "After two days";
        public const string ONE_WEEK = "After one week";
        public const string TWO_WEEKS = "After two weeks";
        public const string ONE_MONTH = "After one month";

        public ItemCollection GetValues()
        {
            ItemCollection expireGamesAfter = new ItemCollection();
            expireGamesAfter.Add(NEVER);
            expireGamesAfter.Add(ONE_DAY);
            expireGamesAfter.Add(TWO_DAYS);
            expireGamesAfter.Add(ONE_WEEK);
            expireGamesAfter.Add(TWO_WEEKS);
            expireGamesAfter.Add(ONE_MONTH);
            return expireGamesAfter;
        }
    }

    public class MisplayHotkeySource : IItemsSource
    {
        public const string CONTROL_M = "Ctrl+M";
        public const string CONTROL_ALT_M = "Ctrl+Alt+M";
        public const string CONTROL_SHIFT_M = "Ctrl+Shift+M";
        public const string NONE = "None";
        public ItemCollection GetValues()
        {
            ItemCollection hotkeys = new ItemCollection();
            hotkeys.Add(CONTROL_M);
            hotkeys.Add(CONTROL_ALT_M);
            hotkeys.Add(CONTROL_SHIFT_M);
            hotkeys.Add(NONE);
            return hotkeys;
        }
    }

    [Serializable]
    public class GameRecorderSettings : PluginDataContainer
    {
        [ItemsSource(typeof(LocaleItemsSource))]
        public string Locale { get; set; }

        [ItemsSource(typeof(GameModesSource))]
        public string GameModes { get; set; }

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

        [ItemsSource(typeof(DeleteGamesSource))]
        public string DeleteGames { get; set; }

        [ItemsSource(typeof(MisplayHotkeySource))]
        public string MisplayHotkey { get; set; }

        public GameRecorderSettings()
        {
            Name = "GameRecorder";
            Locale = "enUS";
            GameModes = "All";
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
            DeleteGames = DeleteGamesSource.NEVER;
            MisplayHotkey = MisplayHotkeySource.CONTROL_M;
        }
    }

    public class GameRecorderPlugin : Plugin
    {
        // Top-level folder where all output is saved
        private const string baseFolder = "RecordedGames";

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
        private string turnWriterPath = null;

        // Log messages received before first turn
        private Queue<string> queuedLogMessages = new Queue<string>();

        // Writers for whispers and friend requests received
        private StreamWriter whisperWriter = null;
        private StreamWriter friendRequestWriter = null;

        // Stores action for next received screenshot
        private string screenshotAction = null;

        private ImageFormatConverter imageFormatConverter = new ImageFormatConverter();

        // Contains card name translations (for non en-US locales)
        private Dictionary<string, string> nameTranslations = null;
        private string nameTranslationLocale = null;

        // Keep track of enemy and friend classes
        private Card.CClass enemyClass = Card.CClass.NONE;
        private Card.CClass friendClass = Card.CClass.NONE;

        // Used for hotkeys
        private KeyboardHook hook = new KeyboardHook();

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
                HideFileAccessTimes(turnWriterPath);
            }

            if (whisperWriter != null)
            {
                whisperWriter.Close();
                whisperWriter = null;
            }

            if (friendRequestWriter != null)
            {
                friendRequestWriter.Close();
                friendRequestWriter = null;
            }

            // Unregister event handlers
            Debug.OnLogReceived -= OnLogReceived;
            GUI.OnScreenshotReceived -= OnScreenshotReceived;

            // Unregister hotkeys
            if (hook != null)
            {
                hook.Dispose();
                hook = null;
            }
        }

        public override void OnPluginCreated()
        {
            settings = (GameRecorderSettings)DataContainer;
            Debug.OnLogReceived += OnLogReceived;
            GUI.OnScreenshotReceived += OnScreenshotReceived;

            RegisterHotkeys();
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
            turnWriterPath = null;
            queuedLogMessages.Clear();
            screenshotAction = null;
            enemyClass = Card.CClass.NONE;
            friendClass = Card.CClass.NONE;
        }

        public override void OnGameEnd()
        {
            EndGame();
        }

        private void EndGame(bool previous = false)
        {
            if (turnWriter != null)
            {
                turnWriter.Close();
                turnWriter = null;
            }

            CopySeeds(previous);

            if (gameStarted)
            {
                if ((settings.DeleteWins && wonLastGame) || turnNum == 0)
                {
                    Directory.Delete(currentGameFolder, true);
                }
                else if (previous)
                {
                    Log("Results for the previous game are unknown");
                }
                else
                {
                    string suffix = wonLastGame ? " WIN" : " LOSS";
                    string newGameFolder = currentGameFolder + suffix;
                    Directory.Move(currentGameFolder, newGameFolder);
                }

                DeleteOldGames();
            }

            Reset();
        }

        private void DeleteOldGames()
        {
            DateTime cutoffDate = DateTime.Now;
            if (settings.DeleteGames.Equals(DeleteGamesSource.ONE_DAY))
            {
                cutoffDate = cutoffDate.AddDays(-1);
            }
            else if (settings.DeleteGames.Equals(DeleteGamesSource.TWO_DAYS))
            {
                cutoffDate = cutoffDate.AddDays(-2);
            }
            else if (settings.DeleteGames.Equals(DeleteGamesSource.ONE_WEEK))
            {
                cutoffDate = cutoffDate.AddDays(-7);
            }
            else if (settings.DeleteGames.Equals(DeleteGamesSource.TWO_WEEKS))
            {
                cutoffDate = cutoffDate.AddDays(-14);
            }
            else if (settings.DeleteGames.Equals(DeleteGamesSource.ONE_MONTH))
            {
                cutoffDate = cutoffDate.AddDays(-31);
            }
            else
            {
                return;
            }

            foreach (string path in Directory.GetDirectories(baseFolder))
            {
                var gameFolder = new DirectoryInfo(path);
                if (gameFolder.Name.Length > dateFormat.Length &&
                    gameFolder.LastWriteTime.CompareTo(cutoffDate) < 0)
                {
                    // Check that folder name starts with date format
                    DateTime result;
                    if (DateTime.TryParseExact(gameFolder.Name.Substring(0, dateFormat.Length),
                        dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                    {
                        try
                        {
                            Directory.Delete(path, true);
                            Log("Deleted old game: " + gameFolder.Name);
                        }
                        catch (Exception e)
                        {
                            Log("Failed to delete old game folder '" + gameFolder.Name + "' due to '" + e.Message);
                        }
                    }
                }
            }
        }

        public override void OnStopped()
        {
            if (turnWriter != null)
            {
                turnWriter.Flush();
            }

            // Overwritten when game ends
            CopySeeds();
        }

        public override void OnHandleMulligan(List<Card.Cards> choices, Card.CClass enemyClass, Card.CClass friendClass)
        {
            // Check if OnGameEnd was never fired
            if (gameStarted)
            {
                EndGame(true);
            }

            if (IsCurrentGameModeSelected())
            {
                StartGame(friendClass, enemyClass);
            }

            if (settings.ScreenshotMulligan)
            {
                TakeScreenshot("Mulligan");
            }
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

            if (!gameStarted && IsCurrentGameModeSelected())
            {
                var board = API.Bot.CurrentBoard;                
                StartGame(board.FriendClass, board.EnemyClass);
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
            }

            if (turnWriter != null)
            {
                turnWriter.Flush();
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
                if (whisperWriter == null)
                {
                    whisperWriter = new StreamWriter(baseFolder + "\\Whispers.txt", true);
                }
                whisperWriter.WriteLine(GetTimestampPrefix() + friend.GetName() + " says " + message);
                whisperWriter.Flush();
            }
        }

        public override void OnFriendRequestReceived(FriendRequest request)
        {
            if (settings.LogFriendRequests)
            {
                if (friendRequestWriter == null)
                {
                    friendRequestWriter = new StreamWriter(baseFolder + "\\FriendRequests.txt", true);
                }
                friendRequestWriter.WriteLine(GetTimestampPrefix() + request.GetPlayerName());
                friendRequestWriter.Flush();
            }
        }

        private void OnLogReceived(string message)
        {
            // TODO: Ask wirmate to add uncaught exception handling
            try
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
            catch(Exception e)
            {
                Log("Error while handling received log message: " + e.StackTrace);
            }
        }

        private void InitializeNameTranslations()
        {
            var englishCardsFile = new FileInfo("CardDatabase\\cardDB.enUS.xml");
            var nativeCardsFile = new FileInfo("CardDatabase\\cardDB." + settings.Locale + ".xml");

            if (!englishCardsFile.Exists || !nativeCardsFile.Exists)
            {
                Log("Failed to initialize name translations - no card database found!");
                return;
            }

            XDocument nativeContent = XDocument.Load(nativeCardsFile.FullName);
            XDocument englishContent = XDocument.Load(englishCardsFile.FullName);
            
            // Build lookup: Card ID -> English Name
            var englishIds = new Dictionary<string, string>();
            foreach (var elem in englishContent.Root.Elements("Card"))
            {
                string id = elem.Element("CardId").Value;
                string name = elem.Element("Name").Value;
                englishIds[id] = name;
            }

            // Build lookup: Native Name -> Card ID
            var nativeNames = new Dictionary<string, string>();
            foreach (var elem in nativeContent.Root.Elements("Card"))
            {
                string name = elem.Element("Name").Value;
                string id = elem.Element("CardId").Value;
                nativeNames[name] = id;
            }

            // Build lookup: Native Name -> English Name
            nameTranslations = new Dictionary<string, string>();

            foreach (var nativeName in nativeNames)
            {
                nameTranslations[nativeName.Key] = englishIds[nativeName.Value];
            }

            nameTranslationLocale = settings.Locale;

            Log("Initialized " + nameTranslations.Count + " card name translations.");
        }

        private static void Log(String message)
        {
            Bot.Log("[GameRecorder] " + message);
        }

        private bool IsCurrentGameModeSelected()
        {
            if (settings.GameModes.Equals("All"))
            {
                return true;
            }

            // Check game mode
            switch (Bot.CurrentMode())
            {
                case Bot.Mode.Arena:
                case Bot.Mode.ArenaAuto:
                    return settings.GameModes.Contains("Arena");
                case Bot.Mode.Ranked:
                    return settings.GameModes.Contains("Ranked");
                case  Bot.Mode.Unranked:
                    return settings.GameModes.Contains("Unranked");
                default:
                    return false;
            }
        }

        private static readonly string dateFormat = "yyyy-MM-dd HHmmss";

        private void StartGame(Card.CClass friendClass, Card.CClass enemyClass)
        {
            this.enemyClass = enemyClass;
            this.friendClass = friendClass;

            // Create folder for the new game
            string dateTime = DateTime.Now.ToString(dateFormat);
            var friend = friendClass.ToString().Capitalize();
            var enemy = enemyClass.ToString().Capitalize();
            currentGameFolder = baseFolder + "\\" + dateTime + " " + Bot.CurrentMode() + " " + friend + " vs. " + enemy;
            Directory.CreateDirectory(currentGameFolder);
            gameStarted = true;
        }

        private static Bitmap ResizeImage(Bitmap original, int newWidth, int newHeight)
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
            string turnAndAction = "Turn_" + turnNum + "_" + actionNum;
            string fileExtension = settings.ImageFormat.Equals("JPEG") ? "jpg" : settings.ImageFormat.ToLower();
            string fileName = turnAndAction + " " + screenshotAction + "." + fileExtension;

            // Save with given format and quality
            string filePath = currentGameFolder + "\\" + fileName;
            image.Save(filePath, GetEncoder(format), encoderParams);

            if (settings.HidePersonalInfo)
            {
                HideFileAccessTimes(filePath);
            }

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
            if (!settings.IncludeLogs || !gameStarted)
            {
                return;
            }

            if (turnWriter != null)
            {
                // Close the existing log file
                turnWriter.Close();

                HideFileAccessTimes(turnWriterPath);
            }
            else
            {
                // Write queued log messages now
                string filePath = currentGameFolder + "\\Turn_0_Logs.txt";
                using (var writer = new StreamWriter(filePath))
                {
                    foreach (string message in queuedLogMessages)
                    {
                        WriteLogMessage(writer, message);
                    }
                }

                HideFileAccessTimes(filePath);
            }

            // Setup new log file for this turn
            turnWriterPath = currentGameFolder + "\\" + "Turn_" + turnNum + "_Logs.txt";
            turnWriter = new StreamWriter(turnWriterPath);
        }

        private static DateTime year2000 = new DateTime(2000, 1, 1);

        private static void HideFileAccessTimes(string filePath)
        {
            File.SetCreationTime(filePath, year2000);
            File.SetLastAccessTime(filePath, year2000);
            File.SetLastWriteTime(filePath, year2000);
        }

        private string GetTimestampPrefix()
        {
            return "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
        }

        private string TranslateCardNames(string message)
        {
            // Check if name translations need to be initialized or locale changed
            if (nameTranslations == null || nameTranslationLocale != settings.Locale)
            {
                InitializeNameTranslations();
            }

            string addAction = "Play sequence : ";
            int addActionIndex = message.IndexOf(addAction);
            if (addActionIndex != -1)
            {
                int i = message.IndexOf(" ", addActionIndex + addAction.Length);
                if (i != -1)
                {
                    string action = message.Substring(i + 1);
                    foreach (string card in action.Split(new string[] { "->" }, StringSplitOptions.None))
                    {
                        int openBracket = card.IndexOf("[");
                        string nativeName = openBracket != -1 ? card.Substring(0, openBracket) : card;
                        if (nativeName.Length > 0)
                        {
                            string englishName;
                            if (nameTranslations.TryGetValue(nativeName, out englishName))
                            {
                                if (!nativeName.Equals(englishName))
                                {
                                    message = message.ReplaceFirst(nativeName, englishName);
                                }
                            }
                            else
                            {
                                Log("Failed to find name translation for: " + nativeName);
                            }
                        }
                    }
                }
            }

            return message;
        }

        private void WriteLogMessage(StreamWriter writer, string message)
        {
            if (settings.Locale != "enUS")
            {
                message = TranslateCardNames(message);
            }

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
            if (!settings.IncludeMulligan || !gameStarted)
            {
                return;
            }

            // Find the most recent SmartMulligan log file
            DateTime mostRecentTime = new DateTime(1900, 1, 1);
            string currentMulliganFile = null;
            string path = "MulliganProfiles\\MulliganArchives\\" + Bot.CurrentMode();
            foreach (string fileName in Directory.GetFiles(path))
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

            // Write last output from SmartMulligan to file
            string mulliganFilePath = currentGameFolder + "\\SmartMulligan.txt";
            using (StreamWriter writer = new StreamWriter(mulliganFilePath))
            {
                foreach (string line in mulliganLogs)
                {
                    writer.WriteLine(line);
                }
            }

            if (settings.HidePersonalInfo)
            {
                HideFileAccessTimes(mulliganFilePath);
            }

            Log("Copied SmartMulligan output");
        }

        private void CopySeeds(bool previous = false)
        {
            if (!settings.IncludeSeeds || !gameStarted)
            {
                return;
            }

            var dirs = Directory.GetDirectories("Seeds")
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(info => info.LastWriteTime);

            var dir = previous ? dirs.ElementAt(1) : dirs.First();

            string[] seedFiles = Directory.GetFiles(dir.FullName, "*.txt");

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

            // Copy the seeds to the recorded games directory
            foreach (string fileName in seedFiles)
            {
                FileInfo fileInfo = new FileInfo(fileName);
                string targetPath = currentGameFolder + "\\" + fileInfo.Name;
                File.Copy(fileInfo.FullName, targetPath, true);

                if (settings.HidePersonalInfo)
                {
                    HideFileAccessTimes(targetPath);
                }
            }

            Log("Copied " + seedFiles.Length + " seed files");
        }

        private void RegisterHotkeys()
        {
            if (!settings.MisplayHotkey.Equals(MisplayHotkeySource.NONE))
            {
                // register the event that is fired after the key press.
                hook.KeyPressed += new EventHandler<KeyPressedEventArgs>(OnHotkeyPressed);

                // register the control + alt + F12 combination as hot key.
                switch (settings.MisplayHotkey)
                {
                    case MisplayHotkeySource.CONTROL_M:
                        hook.RegisterHotKey(ModifierKeys.Control, Keys.M);
                        break;
                    case MisplayHotkeySource.CONTROL_ALT_M:
                        hook.RegisterHotKey(ModifierKeys.Control | ModifierKeys.Alt, Keys.M);
                        break;
                    case MisplayHotkeySource.CONTROL_SHIFT_M:
                        hook.RegisterHotKey(ModifierKeys.Control | ModifierKeys.Shift, Keys.M);
                        break;
                    default:
                        Log("Invalid misplay hotkey: " + settings.MisplayHotkey);
                        return;
                }

                Log("Registered misplay hotkey: " + settings.MisplayHotkey);
            }
        }

        private void OnHotkeyPressed(object sender, KeyPressedEventArgs e)
        {
            // Currently there is only one hotkey -- so assume event is for misplay

            if (!settings.Enabled)
            {
                return;
            }

            if (!gameStarted)
            {
                Log("Cannot record misplay before game started.");
                return;
            }

            TakeScreenshot("Misplay");
        }
    }

    public static class StringExtensionMethods
    {
        // Add extension method to string to replace only first occurrence
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public static string Capitalize(this string str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }
    }

    public sealed class KeyboardHook : IDisposable
    {
        // Registers a hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        // Unregisters the hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// Represents the window that is used internally to get the messages.
        /// </summary>
        private class Window : NativeWindow, IDisposable
        {
            private static int WM_HOTKEY = 0x0312;

            public Window()
            {
                // create the handle for the window.
                this.CreateHandle(new CreateParams());
            }

            /// <summary>
            /// Overridden to get the notifications.
            /// </summary>
            /// <param name="m"></param>
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                // check if we got a hot key pressed.
                if (m.Msg == WM_HOTKEY)
                {
                    // get the keys.
                    Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    ModifierKeys modifier = (ModifierKeys)((int)m.LParam & 0xFFFF);

                    // invoke the event to notify the parent.
                    if (KeyPressed != null)
                        KeyPressed(this, new KeyPressedEventArgs(modifier, key));
                }
            }

            public event EventHandler<KeyPressedEventArgs> KeyPressed;

            #region IDisposable Members

            public void Dispose()
            {
                this.DestroyHandle();
            }

            #endregion
        }

        private Window _window = new Window();
        private int _currentId;

        public KeyboardHook()
        {
            // register the event of the inner native window.
            _window.KeyPressed += delegate (object sender, KeyPressedEventArgs args)
            {
                if (KeyPressed != null)
                    KeyPressed(this, args);
            };
        }

        /// <summary>
        /// Registers a hot key in the system.
        /// </summary>
        /// <param name="modifier">The modifiers that are associated with the hot key.</param>
        /// <param name="key">The key itself that is associated with the hot key.</param>
        public void RegisterHotKey(ModifierKeys modifier, Keys key)
        {
            // increment the counter.
            _currentId = _currentId + 1;

            // register the hot key.
            if (!RegisterHotKey(_window.Handle, _currentId, (uint)modifier, (uint)key))
                throw new InvalidOperationException("Couldn’t register the hot key.");
        }

        /// <summary>
        /// A hot key has been pressed.
        /// </summary>
        public event EventHandler<KeyPressedEventArgs> KeyPressed;

        #region IDisposable Members

        public void Dispose()
        {
            // unregister all the registered hot keys.
            for (int i = _currentId; i > 0; i--)
            {
                UnregisterHotKey(_window.Handle, i);
            }

            // dispose the inner native window.
            _window.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Event Args for the event that is fired after the hot key has been pressed.
    /// </summary>
    public class KeyPressedEventArgs : EventArgs
    {
        private ModifierKeys _modifier;
        private Keys _key;

        internal KeyPressedEventArgs(ModifierKeys modifier, Keys key)
        {
            _modifier = modifier;
            _key = key;
        }

        public ModifierKeys Modifier
        {
            get { return _modifier; }
        }

        public Keys Key
        {
            get { return _key; }
        }
    }

    /// <summary>
    /// The enumeration of possible modifiers.
    /// </summary>
    [Flags]
    public enum ModifierKeys : uint
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }
}
