# Game Recorder

Game Recorder is a plugin for [SmartBot](http://sb-forum.com/) (an intelligent Hearthstone bot) that records games by taking screenshots and organizing log files. It can be used to analyze games post-mortem to see what went wrong and even report AI misplays.

## Installation & Usage

1. Download the [GameRecorder.cs](https://github.com/levinson/SmartBotPlugins/raw/master/GameRecorder.cs) file to your SmartBot ```Plugins``` directory.
2. Enable the GameRecorder plugin and set desired configurations (see next section).
3. Start the bot and watch the ```RecordedGames``` folder in its root directory.
  * A new folder is created for each game (e.g. ```2015-12-05 163737 Rogue vs. Shaman```)
  * Screenshots are captured live throughout the game.
  * Folder name is appended with ```WIN``` or ```LOSS``` (on victory or defeat).
  * Logs are saved for each turn (or when bot is stopped).
  * Seeds are copied when the game ends (or when bot is stopped).
4. Review screenshots and log files to analyze what went wrong in games.
5. Report any [AI misplays](http://sb-forum.com/index.php?/forum/31-smartcc-ai-misplay/) found by creating a new thread with relevant details.
  * Include misplay and media only for a single turn.
  * Explain what the bot did and what you expected it to do.
  * Justify why the bot should have made the alternate play.

Note that the bot can be stopped and started without interfering with GameRecorder.

## Configuration

Here are the meanings behind the many configuration parameters:

Name|Description
---|---
HidePersonalInfo|Enable this to gray out personal info in screenshots, and to remove timestamps and auth messages from logs (highly recommended).
IncludeLogs|Enable this to generate turn-by-turn log files.
IncludeMulligan|Enable this to copy mulligan output from [SmartMulligan](http://sb-forum.com/index.php?/topic/5930-requestfeedback-smartmulligan/) at start of turn.
IncludeSeeds|Enable this to copy seed files at end of game (or when bot is stopped).
LogFriendRequests|Enable this to log friend requests after each game (or when bot is stopped).
LogWhispers|Enable this to log received whispers after each game (or when bot is stopped).
ScreenshotBeginTurn|Whether to capture screenshot at beginning of turn.
ScreenshotChoice|Whether to capture screenshot of choice events (e.g. Discover).
ScreenshotConcede|Whether to capture screeshot when conceding.
ScreenshotDefeat|Whether to capture screenshot on defeat.
ScreenshotEndTurn|Whether to capture screenshot at end of turn.
ScreenshotLethal|Whether to capture screenshot when lethal is found.
ScreenshotResimulate|Whether to capture screenshot when bot resimulates.
ScreenshotVictory|Whether to capture screenshot on victory.
