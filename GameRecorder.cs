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
        public bool IncludeSeeds { get; set; }

        public bool HidePersonalInfo { get; set; }

        public bool AlternateScreenshot { get; set; }

        public bool ScreenshotBeginTurn { get; set; }
        public bool ScreenshotEndTurn { get; set; }
        public bool ScreenshotChoice { get; set; }
        public bool ScreenshotResimulate { get; set; }
        public bool ScreenshotConcede { get; set; }
        public bool ScreenshotLethal { get; set; }
        public bool ScreenshotVictory { get; set; }
        public bool ScreenshotDefeat { get; set; }

        public int WindowLeftMargin { get; set; }
        public int WindowRightMargin { get; set; }
        public int WindowTopMargin { get; set; }
        public int WindowBottomMargin { get; set; }

        public GameRecorderSettings()
        {
            Name = "GameRecorder";
            IncludeLogs = true;
            IncludeSeeds = true;
            HidePersonalInfo = true;
            AlternateScreenshot = false;
            ScreenshotBeginTurn = true;
            ScreenshotEndTurn = true;
            ScreenshotChoice = true;
            ScreenshotResimulate = true;
            ScreenshotConcede = true;
            ScreenshotLethal = true;
            ScreenshotVictory = true;
            ScreenshotDefeat = true;
            WindowLeftMargin = 8;
            WindowRightMargin = 8;
            WindowTopMargin = 31;
            WindowBottomMargin = 8;
        }
    }

    public class GameRecorderPlugin : Plugin
    {
        // Folder to save screenshots for current game
        private string currentGameFolder = null;

        // Keep track of the turn and action number
        private int turnNum = 0;
        private int actionNum = 0;

        private bool wonLastGame = false;

        private GameRecorderSettings settings = null;

        // Writer for current turn logging
        private StreamWriter turnWriter = null;

        // Log messages received before first turn
        private Queue<String> queuedLogMessages = new Queue<String>();

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
        }

        public override void OnGameBegin()
        {
            // Reset private variables
            currentGameFolder = null;
            turnNum = 0;
            actionNum = 0;
            wonLastGame = false;
            turnWriter = null;
            queuedLogMessages.Clear();
        }

        public override void OnGameEnd()
        {
            if (turnWriter != null)
            {
                turnWriter.Close();
                turnWriter = null;
            }

            CopySeeds();

            if (wonLastGame)
            {
                AddSuffixToGameFolder(" WIN");
            }
            else
            {
                AddSuffixToGameFolder(" LOSS");
            }
            
        }

        public override void OnStopped()
        {
            if (turnWriter != null)
            {
                // Flush all output to current log file
                turnWriter.Flush();
            }

            // Copy seeds - overwritten when game ends
            CopySeeds();
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

            if (currentGameFolder == null)
            {
                // Create folder for the new game
                string dateTime = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                var board = API.Bot.CurrentBoard;
                var friend = Capitalize(board.FriendClass.ToString());
                var enemy = Capitalize(board.EnemyClass.ToString());

                currentGameFolder = "RecordedGames\\" + dateTime + " " + friend + " vs. " + enemy;
                Directory.CreateDirectory(currentGameFolder);
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

        private void AddSuffixToGameFolder(string suffix)
        {
            if (currentGameFolder != null)
            {
                string newGameFolder = currentGameFolder + suffix;
                Directory.Move(currentGameFolder, newGameFolder);
                currentGameFolder = newGameFolder;
            }
        }

        private string Capitalize(String str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        private void TakeScreenshot(String action)
        {
            IntPtr handle = WindowUtils.FindWindow("Hearthstone");
            if (handle == IntPtr.Zero)
            {
                Log("Failed to find Hearthstone window!");
                return;
            }

            // Capture game window
            Image original = settings.AlternateScreenshot ?
                WindowUtils.CaptureWindowAlternate(handle) :
                WindowUtils.CaptureWindow(handle);

            // Crop image due to weird shit 
            Bitmap image = new Bitmap(original.Width - (settings.WindowLeftMargin + settings.WindowRightMargin),
                original.Height - (settings.WindowTopMargin + settings.WindowBottomMargin));

            using (Graphics g = Graphics.FromImage(image))
            {
                g.DrawImage(original, new Point(-settings.WindowLeftMargin, -settings.WindowTopMargin));

                if (settings.HidePersonalInfo)
                {
                    SolidBrush grayBrush = new SolidBrush(Color.DimGray);

                    int rectangleWidth = image.Width / 8;
                    int rectangleHeight = (int)(0.12 * image.Height);

                    Rectangle[] grayBoxes = new Rectangle[] {
                        new Rectangle(1, 1, rectangleWidth, rectangleHeight),
                        new Rectangle(1, (int)(0.80 * image.Height), rectangleWidth, rectangleHeight),
                        new Rectangle(1, (int)(0.90 * image.Height), (int)(1.5 * rectangleWidth), rectangleHeight)
                    };

                    g.FillRectangles(grayBrush, grayBoxes);
                }
            }

            // Save the modified image
            string turnAndActionNum = turnNum.ToString("D2") + "-" + actionNum.ToString("D2");
            string fileName = turnAndActionNum + " " + action + ".jpg";
            image.Save(currentGameFolder + "\\" + fileName, ImageFormat.Jpeg);
            Log("Captured screenshot: " + fileName);

            actionNum += 1;
        }

        private void SetupTurnLogger()
        {
            if (!settings.IncludeLogs)
            {
                return;
            }

            if (currentGameFolder == null)
            {
                Log("Failed to setup output logging since game folder is not defined!");
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
                writer.WriteLine(message);
            }
        }

        private void CopySeeds()
        {
            if (!settings.IncludeSeeds)
            {
                return;
            }

            if (currentGameFolder == null)
            {
                // No turns yet
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

    public class WindowUtils
    {
        public static IntPtr FindWindow(String windowName)
        {
            return User32.FindWindow(null, windowName);
        }

        public static Bitmap CaptureWindowAlternate(IntPtr handle)
        {
            var rect = new User32.Rect();
            User32.GetWindowRect(handle, ref rect);
            
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            return bmp;
        }

        public static Image CaptureWindow(IntPtr handle)
        {
            // get the hDC of the target window
            IntPtr hdcSrc = User32.GetWindowDC(handle);
            // get the size
            User32.Rect rect = new User32.Rect();
            User32.GetWindowRect(handle, ref rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            // create a device context we can copy to
            IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
            // create a bitmap we can copy it to,
            // using GetDeviceCaps to get the width/height
            IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, width, height);
            // select the bitmap object
            IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
            // bitblt over
            GDI32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, GDI32.SRCCOPY);
            // restore selection
            GDI32.SelectObject(hdcDest, hOld);
            // clean up 
            GDI32.DeleteDC(hdcDest);
            User32.ReleaseDC(handle, hdcSrc);
            // get a .NET image object for it
            Image image = Image.FromHbitmap(hBitmap);
            // free up the Bitmap object
            GDI32.DeleteObject(hBitmap);
            return image;
        }

        private class GDI32
        {
            public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
                int nWidth, int nHeight, IntPtr hObjectSource,
                int nXSrc, int nYSrc, int dwRop);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
                int nHeight);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        }

        private class User32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct Rect
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        }
    }
}
