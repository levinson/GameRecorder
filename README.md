# Game Recorder

Game Recorder is a plugin for [SmartBot](http://sb-forum.com/) (an intelligent Hearthstone bot) that records games by taking screenshots and organizing log files. It can be used to analyze games post-mortem to see what went wrong and even report AI misplays.

Screenshots are taken using native Windows API so that they cannot be masked by applications in the foreground.

## Installation & Usage

1. Download the [GameRecorder.cs](https://github.com/levinson/SmartBotPlugins/raw/master/GameRecorder.cs) file to your SmartBot ```Plugins``` directory.
2. Enable the GameRecorder plugin and set desired configurations (see next section).
3. Start the bot and watch the ```RecordedGames``` folder in its root directory.
  * A new folder is created for each game (e.g. ```2015-12-05 163737 Rogue vs. Shaman```)
  * Screenshots are captured live throughout the game.
  * Folder name is appended with ```WIN``` or ```LOSS``` (on victory or defeat).
  * Seeds are copied when the game is ended (or bot is stopped).
  * Logs are copied when next game begins (or bot is stopped).
4. Review screenshots and log files to analyze what went wrong in games.
5. Report any [AI misplays](http://sb-forum.com/index.php?/forum/31-smartcc-ai-misplay/) found by creating a new thread with relevant details.
  * Include misplay and media only for a single turn.
  * Explain what the bot did and what you expected it to do.
  * Justify why the bot should have made the alternate play.

Note that the bot can be stopped and started without interrupting GameRecorder.

## Configuration

Here are the meanings behind the many configuration parameters:

Name|Description
---|---
HidePersonalInfo|Enable this to gray out personal info in screenshots, and to remove timestamps and auth messages from logs. Highly recommended to keep this enabled.
IncludeLogs|Enable this to generate game start and turn-by-turn log files at start of next game or when bot is stopped.
IncludeSeeds|Enable this to copy seed files at end of game or when bot is stopped.
ScreenshotBeginTurn|Whether to capture screenshot at beginning of turn.
ScreenshotChoice|Whether to capture screenshot of choice events (e.g. Discover).
ScreenshotConcede|Whether to capture screeshot when conceding.
ScreenshotDefeat|Whether to capture screenshot on defeat.
ScreenshotEndTurn|Whether to capture screenshot at end of turn.
ScreenshotLethal|Whether to capture screenshot when lethal is found.
ScreenshotResimulate|Whether to capture screenshot when bot resimulates.
ScreenshotVictory|Whether to capture screenshot on victory.
WindowBottomMargin|Pixels to crop off bottom of screenshot.
WindowLeftMargin|Pixels to crop off left-side of screenshot.
WindowRightMargin|Pixels to crop off right-side of screenshot.
WindowTopMargin|Pixels to crop off top of screenshot.

### Window Margins
The window margin parameters were added to remove window borders and weird artifacts around border of the screenshots. These values were tuned for running on Windows 10 in windowed mode. You can size this properly for your platform by setting the values to zero, and then counting the number of pixels to remove on each border. I expect the default values to work fairly well for most people.
