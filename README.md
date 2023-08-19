[![Release](https://github.com/n-shay/BingBackground/actions/workflows/release.yml/badge.svg)](https://github.com/n-shay/BingBackground/actions/workflows/release.yml)

# BingBackground
BingBackground is a small lightweight Windows command-line tool that downloads Bing's most recent homepage image and sets it as your desktop background. Images are saved in your Pictures directory in a folder named "Bing Backgrounds".

## Usage

### Prerequisites
- .NET Desktop Runtime 6.0 ([Download](https://aka.ms/dotnet/6.0/dotnet-runtime-win-x64.exe) | [Documentation](https://dotnet.microsoft.com/en-us/download/dotnet/6.0))

### Install (via Task Scheduler)
1. Download and extract the latest [release](https://github.com/n-shay/BingBackground/releases/latest) to its permanent location.
2. Open CMD, Terminal or PowerShell as an admin and navigate to directory.
3. Run the following command:

    ```
    BingBackground.exe -i
    ```

4. Task "BingBackground Scheduled Task" was created in Task Scheduler and runs hourly or upon login.

### Run Once
If you'd like to execute the tool once, for testing or scheduling it on your own, simply execute `BingBackground.exe` as an admin (or "highest privileges").

#### Supported Command Arguments
| Command | Required? | Description |
|---------|-----------|-----------|
| `-i` `-install` `/i` `/install` | | Installs tool as background task in Task Scheduler |
| `-u` `-uninstall` `/u` `/uninstall` | | Uninstalls background task from Task Scheduler |

### Uninstall
1. Open CMD, Terminal or PowerShell as an admin and navigate to directory the tool was installed.
2. Run the following command:

    ```
    BingBackground.exe -u
    ```

3. Task "BingBackground Scheduled Task" was removed from Task Scheduler.


## Configuration
Coming soon...

## Troubleshooting
There are currently 2 methods to troubleshoot the tool behavior:

### Logs
Logs are automatically created in `%USERPROFILE%\AppData\Roaming` with a naming convention of `BingBackgroundYYYYMMDD.log`.

Log files are rolling daily (local time) with retention policy of 30 days.

### Task Scheduler
Task history is disabled by default, but can be enabled manually.
Regardless, it displays its "Last Run Result" which can be helpful at times.

## Future Features:
- Configuration:
  - resolution
  - update interval
- Installation
- Event Viewer (Logging)
- Remove prerequisite dependency (make portable).
- Add UI for offline, status and configs
- [dev] .editorconfig
- [dev] reduce package size

## Contribution
**Contributions are always welcome!**
Feel free to open pull requests for enhancements or bug fixes.

Clone the GitHub repo and use Visual Studio 2022 (or JetBrains Rider).

## Author

[Shay Nissel <img src="https://avatars.githubusercontent.com/u/5875440?v=4" width="15"/>](https://github.com/n-shay)

# Credits
This tool was originally developed by Josue Espinosa (josue.espinosa.live@gmail.com).

## License
[MIT](https://github.com/n-shay/BingBackground/blob/master/LICENSE)
