To run OpenRA, several files are needed from the original game disks.
A minimal asset pack can also be downloaded and installed by the game.

The following lists per-platform dependencies required to build from source.

Compiling OpenRA requires the following dependencies:
* [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or via Visual Studio)

To compile OpenRA, open the `OpenRA.sln` solution in the main folder or build it from the command-line with `dotnet build`.

Run the game using the the exe in `OpenRA.Game.<game>\bin\...` or from the command-line with `dotnet run --project OpenRA.Game.<game>`.

For available games, see the folders in this directory starting with `OpenRA.Game.`.
