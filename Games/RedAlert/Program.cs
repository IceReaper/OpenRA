using System.IO;
using System.Linq;
using OpenRA;
using OpenRA.Launcher;
using OpenRA.Server;

args = new[] { "Game.Mod=ra", $"Engine.ModSearchPaths={Directory.GetCurrentDirectory()}" }.Concat(args).ToArray();

if (args.Contains("--server"))
	ServerProgram.ServerMain(args.Where(arg => arg != "--server").ToArray());
else if (args.Contains("--utility"))
	UtilityProgram.UtilityMain(args.Where(arg => arg != "--utility").ToArray());
else
	GameProgram.GameMain(args);
