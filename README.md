# Game Recorder

Game Recorder is a plugin for [SmartBot](http://sb-forum.com/) (an intelligent Hearthstone bot) that records games by taking screenshots and saving relevant logs turn-by-turn. Misplay screenshots can be added at the press of a hotkey. Recorded games can also be analyzed post-mortem to see what went wrong and report misplays. See [configuration](#configuration) for complete list of features.

## Installation & Usage

1. Download the [GameRecorder.cs](https://github.com/levinson/GameRecorder/raw/master/GameRecorder.cs) file to your SmartBot ```Plugins``` directory.
2. Enable the GameRecorder plugin and set desired configurations (see next section).
3. Start the bot and watch the ```RecordedGames``` folder in its root directory.
  * A new folder is created for each game (e.g. ```2015-12-05 163737 Rogue vs. Shaman```)
  * Screenshots are captured on selected events and when the misplay hotkey is pressed.
  * Logs are saved for each turn (or when bot is stopped).
  * Folder name is appended with ```WIN``` or ```LOSS``` when the game ends.
  * Seeds are copied when the game ends (or when bot is stopped).
4. Review screenshots and log files to analyze what went wrong in games.

Note that the bot can be stopped and started without interfering with GameRecorder.

## Misplay Reporting

Review screenshots and log files in recorded game folders to discover misplays. If you pressed the misplay hotkey during a game, then search for screenshots with the word 'Misplay' in the name.

For misplays during mulligan create a post in the [SmartMulligan thread](http://sb-forum.com/index.php?/topic/6320-requestsreports-smartmulligan/). For all other misplays create a new thread in the [SmartCC AI Misplay forum](http://sb-forum.com/index.php?/forum/31-smartcc-ai-misplay/).

In your misplay report attach all the files for the turn on which the misplay occurred, and also include relevant details such as what the bot did, what you expected it to do, and why.

## Configuration

Here are the meanings behind the many configuration parameters:

Name|Description
---|---
DeleteGames|Select when to delete old recorded games.
DeleteWins|Enable this to delete recorded games that end in a win.
GameModes|Select the game modes to record.
HidePersonalInfo|Enable this to gray out personal info in screenshots, remove timestamps and auth messages from logs, and mask file modification times (highly recommended).
ImageFormat|Select the image format for saving screenshots.
ImageQuality|Enter the image quality for saving screenshots (1-100).
ImageResizeEnabled|Enable this to resize screenshots when saving.
ImageResizeHeight|Enter the maximum screenshot height (in pixels).
ImageResizeWidth|Enter the maximum screenshot width (in pixels).
IncludeLogs|Enable this to generate turn-by-turn log files.
IncludeMulligan|Enable this to copy mulligan output from [SmartMulligan](http://sb-forum.com/index.php?/topic/5930-download-smartmulligan/) at start of turn.
IncludeSeeds|Enable this to copy seed files at end of game (or when bot is stopped).
Locale|Set the locale of your game client to translate card names to English in log files.
LogFriendRequests|Enable this to log friend requests.
LogWhispers|Enable this to log received whispers.
MisplayHotkey|Select the hotkey for creating misplay screenshots. Changing this setting requires reload.
ScreenshotBeginTurn|Whether to capture screenshot at beginning of turn.
ScreenshotChoice|Whether to capture screenshot of choice events (e.g. Discover).
ScreenshotConcede|Whether to capture screeshot when conceding.
ScreenshotDefeat|Whether to capture screenshot on defeat.
ScreenshotEndTurn|Whether to capture screenshot at end of turn.
ScreenshotLegend|Whether to capture screenshot when legend rank is obtained.
ScreenshotLethal|Whether to capture screenshot when lethal is found.
ScreenshotMulligan|Whether to capture screenshot of mulligan selections.
ScreenshotResimulate|Whether to capture screenshot when bot resimulates.
ScreenshotVictory|Whether to capture screenshot on victory.
