#region Copyright & License Information

/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace OpenRA.Launcher
{
	using System.Globalization;
	using System.Net;
	using System.Threading;
	using OpenRA.Network;
	using OpenRA.Platforms.Default;
	using OpenRA.Server;
	using UtilityActions = Dictionary<string, KeyValuePair<Action<Utility, string[]>, Func<string[], bool>>>;

	[Serializable]
	public class NoSuchCommandException : Exception
	{
		public readonly string Command;
		public NoSuchCommandException(string command)
			: base($"No such command '{command}'")
		{
			Command = command;
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Command", Command);
		}
	}

	public static class DefaultProgram
	{
		public static void Main(string vendor, string product, string mod, string[] args)
		{
			if (args.Contains("--utility"))
				UtilityMain([mod, ..args.Where(args => args != "--utility")]);
			else if (args.Contains("--server"))
				ServerMain([$"Game.Vendor={vendor}", $"Game.Product={product}", $"Game.Mod={mod}", ..args]);
			else
				GameMain([$"Game.Vendor={vendor}", $"Game.Product={product}", $"Game.Mod={mod}", ..args]);
		}

		private static void GameMain(string[] args)
		{
			if (Debugger.IsAttached || args.Contains("--just-die"))
			{
				try
				{
					Game.InitializeAndRun(new DefaultPlatform(), args);
				}
				catch
				{
					// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
					// try-finally won't work - an unhandled exception kills our process without running the finally block!
					Log.Dispose();
					throw;
				}
				finally
				{
					Log.Dispose();
				}
			}

			AppDomain.CurrentDomain.UnhandledException += (_, e) => ExceptionHandler.HandleFatalError((Exception)e.ExceptionObject);

			try
			{
				Game.InitializeAndRun(new DefaultPlatform(), args);
			}
			catch (Exception e)
			{
				ExceptionHandler.HandleFatalError(e);
			}
			finally
			{
				// Flushing logs in finally block is okay here, as the catch block handles the exception.
				Log.Dispose();
			}
		}

		private static void ServerMain(string[] args)
		{
			try
			{
				var arguments = new Arguments(args);

				var engineDirArg = arguments.GetValue("Engine.EngineDir", null);
				if (!string.IsNullOrEmpty(engineDirArg))
					Platform.OverrideEngineDir(engineDirArg);

				var supportDirArg = arguments.GetValue("Engine.SupportDir", null);
				if (!string.IsNullOrEmpty(supportDirArg))
					Platform.OverrideSupportDir(supportDirArg);

				Log.AddChannel("debug", "dedicated-debug.log", true);
				Log.AddChannel("perf", "dedicated-perf.log", true);
				Log.AddChannel("server", "dedicated-server.log", true);
				Log.AddChannel("nat", "dedicated-nat.log", true);
				Log.AddChannel("geoip", "dedicated-geoip.log", true);

				// Special case handling of Game.Mod argument: if it matches a real filesystem path
				// then we use this to override the mod search path, and replace it with the mod id
				var modID = arguments.GetValue("Game.Mod", null);
				var explicitModPaths = Array.Empty<string>();
				if (modID != null && (File.Exists(modID) || Directory.Exists(modID)))
				{
					explicitModPaths = new[] { modID };
					modID = Path.GetFileNameWithoutExtension(modID);
				}

				if (modID == null)
					throw new InvalidOperationException("Game.Mod argument missing or mod could not be found.");

				// HACK: The engine code assumes that Game.Settings is set.
				// This isn't nearly as bad as ModData, but is still not very nice.
				Game.InitializeSettings(arguments);
				var settings = Game.Settings.Server;

				Nat.Initialize();

				var envModSearchPaths = Environment.GetEnvironmentVariable("MOD_SEARCH_PATHS");
				var modSearchPaths = !string.IsNullOrWhiteSpace(envModSearchPaths) ?
					FieldLoader.GetValue<string[]>("MOD_SEARCH_PATHS", envModSearchPaths) :
					new[] { Path.Combine(Platform.EngineDir, "mods") };

				var mods = new InstalledMods(modSearchPaths, explicitModPaths);

				WriteLineWithTimeStamp($"Starting dedicated server for mod: {modID}");
				while (true)
				{
					// HACK: The engine code *still* assumes that Game.ModData is set
					var modData = Game.ModData = new ModData(mods[modID], mods);
					modData.MapCache.LoadPreviewImages = false; // PERF: Server doesn't need previews, save memory by not loading them.
					modData.MapCache.LoadMaps();

					var endpoints = new List<IPEndPoint> { new(IPAddress.IPv6Any, settings.ListenPort), new(IPAddress.Any, settings.ListenPort) };
					var server = new Server(endpoints, settings, modData, ServerType.Dedicated);

					GC.Collect();
					while (true)
					{
						Thread.Sleep(1000);
						if (server.State == ServerState.GameStarted && server.Conns.Count < 1)
						{
							WriteLineWithTimeStamp("No one is playing, shutting down...");
							server.Shutdown();
							break;
						}
					}

					modData.Dispose();
					WriteLineWithTimeStamp("Starting a new server instance...");
				}
			}
			catch
			{
				// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
				// try-finally won't work - an unhandled exception kills our process without running the finally block!
				Log.Dispose();
				throw;
			}
			finally
			{
				Log.Dispose();
			}
		}

		static void WriteLineWithTimeStamp(string line)
		{
			Console.WriteLine($"[{DateTime.Now.ToString(Game.Settings.Server.TimestampFormat, CultureInfo.CurrentCulture)}] {line}");
		}

		private static void UtilityMain(string[] args)
		{
			try
			{
				var engineDir = Environment.GetEnvironmentVariable("ENGINE_DIR");
				if (!string.IsNullOrEmpty(engineDir))
					Platform.OverrideEngineDir(engineDir);

				Log.AddChannel("perf", null);
				Log.AddChannel("debug", null);

				Game.InitializeSettings(Arguments.Empty);

				var envModSearchPaths = Environment.GetEnvironmentVariable("MOD_SEARCH_PATHS");
				var modSearchPaths = !string.IsNullOrWhiteSpace(envModSearchPaths) ?
					FieldLoader.GetValue<string[]>("MOD_SEARCH_PATHS", envModSearchPaths) :
					new[] { Path.Combine(Platform.EngineDir, "mods") };

				if (args.Length == 0)
				{
					PrintUsage(new InstalledMods(modSearchPaths, Array.Empty<string>()), null);
					return;
				}

				var modId = args[0];
				var explicitModPaths = Array.Empty<string>();
				if (File.Exists(modId) || Directory.Exists(modId))
				{
					explicitModPaths = new[] { modId };
					modId = Path.GetFileNameWithoutExtension(modId);
				}

				var mods = new InstalledMods(modSearchPaths, explicitModPaths);
				if (!mods.Keys.Contains(modId))
				{
					PrintUsage(mods, null);
					return;
				}

				var modData = new ModData(mods[modId], mods);
				var utility = new Utility(modData, mods);
				args = args.Skip(1).ToArray();
				var actions = new UtilityActions();
				foreach (var commandType in modData.ObjectCreator.GetTypesImplementing<IUtilityCommand>())
				{
					var command = (IUtilityCommand)Activator.CreateInstance(commandType);
					var kvp = new KeyValuePair<Action<Utility, string[]>, Func<string[], bool>>(command.Run, command.ValidateArguments);
					actions.Add(command.Name, kvp);
				}

				if (args.Length == 0)
				{
					PrintUsage(mods, actions);
					return;
				}

				try
				{
					var command = args[0];
					if (!actions.TryGetValue(command, out var kvp))
						throw new NoSuchCommandException(command);

					var action = kvp.Key;
					var validateActionArgs = kvp.Value;

					if (validateActionArgs.Invoke(args))
					{
						action.Invoke(utility, args);
					}
					else
					{
						Console.WriteLine($"Invalid arguments for '{command}'");
						GetActionUsage(command, action);
						Environment.Exit(1);
					}
				}
				catch (Exception e)
				{
					Log.AddChannel("utility", "utility.log");
					Log.Write("utility", $"Received args: {args.JoinWith(" ")}");
					Log.Write("utility", e);

					if (e is NoSuchCommandException)
					{
						Console.WriteLine(e.Message);
						Log.Dispose(); // Flush logs before we terminate the process.
						Environment.Exit(1);
					}
					else
					{
						Console.WriteLine("Error: Utility application crashed. See utility.log for details");
						throw;
					}
				}
			}
			catch
			{
				// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
				// try-finally won't work - an unhandled exception kills our process without running the finally block!
				Log.Dispose();
				throw;
			}
			finally
			{
				Log.Dispose();
			}
		}

		static void PrintUsage(InstalledMods mods, UtilityActions actions)
		{
			Console.WriteLine("Run `OpenRA.Utility.exe [MOD]` to see a list of available commands.");
			Console.WriteLine("The available mods are: " + string.Join(", ", mods.Keys));
			Console.WriteLine();

			if (actions == null)
				return;

			var keys = actions.Keys.OrderBy(x => x);

			foreach (var key in keys)
			{
				GetActionUsage(key, actions[key].Key);
			}
		}

		static void GetActionUsage(string key, Action<Utility, string[]> action)
		{
			var descParts = Utility.GetCustomAttributes<DescAttribute>(action.Method, true)
				.SelectMany(d => d.Lines).ToArray();

			if (descParts.Length == 0)
				return;

			var args = descParts.Take(descParts.Length - 1).JoinWith(" ");
			var desc = descParts[^1];

			Console.WriteLine($"  {key} {args}{Environment.NewLine}  {desc}{Environment.NewLine}");
		}
	}
}
