#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenRA.FileSystem;

namespace OpenRA
{
	public class Stats
	{
		public string Name;
		public string Version;
		public string Author;
		public string Description;

		[FieldLoader.Ignore]
		public string Type;

		[FieldLoader.Ignore]
		public Dictionary<string, Dictionary<string, object>> Entries = new Dictionary<string, Dictionary<string, object>>();

		public MiniYaml Rules { get { return Generate("rules", HardcodedRules); } }
		public MiniYaml Weapons { get { return Generate("weapons", HardcodedWeapons); } }

		[FieldLoader.Ignore]
		public MiniYaml HardcodedRules;

		[FieldLoader.Ignore]
		public MiniYaml HardcodedWeapons;

		public static Stats Deserialize(string yaml, string type = null)
		{
			var data = MiniYaml.FromString(yaml).ToDictionary(node => node.Key);
			var stats = FieldLoader.Load<Stats>(data["Meta"].Value);
			stats.Type = type;

			data["Stats"].Value.Nodes.ForEach(actorNode =>
			{
				var values = new Dictionary<string, object>();
				actorNode.Value.Nodes.ForEach(statNode =>
				{
					if (statNode.Value.Nodes.Count > 0)
						values.Add(statNode.Key, statNode.Value.Nodes.ToDictionary(node => node.Key, node => node.Value.Value));
					else
						values.Add(statNode.Key, statNode.Value.Value);
				});
				stats.Entries.Add(actorNode.Key, values);
			});

			if (data.ContainsKey("HardcodedRules"))
				stats.HardcodedRules = data["HardcodedRules"].Value;

			if (data.ContainsKey("HardcodedWeapons"))
				stats.HardcodedWeapons = data["HardcodedWeapons"].Value;

			return stats;
		}

		public string Serialize()
		{
			var data = new List<MiniYamlNode>();
			data.Add(new MiniYamlNode("Meta", FieldSaver.Save(this)));

			var stats = new MiniYamlNode("Stats", new MiniYaml(null));
			data.Add(stats);

			foreach (var actorEntry in Entries)
			{
				var actorStats = new MiniYamlNode(actorEntry.Key, new MiniYaml(null));

				foreach (var statEntry in actorEntry.Value)
				{
					if (statEntry.Value is IDictionary)
					{
						var node = new MiniYamlNode(statEntry.Key, "");
						foreach (DictionaryEntry entry in (IDictionary)statEntry.Value)
							node.Value.Nodes.Add(new MiniYamlNode(entry.Key.ToString(), entry.Value.ToString()));
						actorStats.Value.Nodes.Add(node);
					}
					else
						actorStats.Value.Nodes.Add(new MiniYamlNode(statEntry.Key, statEntry.Value.ToString()));
				}

				stats.Value.Nodes.Add(actorStats);
			}

			if (HardcodedRules != null)
				data.Add(new MiniYamlNode("HardcodedRules", HardcodedRules));

			if (HardcodedWeapons != null)
				data.Add(new MiniYamlNode("HardcodedWeapons", HardcodedWeapons));

			return data.WriteToString();
		}

		MiniYaml Generate(string type, MiniYaml hardcodedYaml)
		{
			// TODO define strictly which stats.yaml!
			var mapping = MiniYaml.FromString(Game.ModData.DefaultFileSystem.Open("stats.yaml").ReadAllText()).ToDictionary(node => node.Key);

			var result = hardcodedYaml == null ? new MiniYaml(null) : hardcodedYaml.Clone();

			foreach (var actorEntry in Entries)
			{
				if (!mapping.ContainsKey(actorEntry.Key))
					continue;

				var actorMapping = mapping[actorEntry.Key].Value.Nodes.ToDictionary(node => node.Key);

				foreach (var statEntry in actorEntry.Value)
				{
					if (!actorMapping.ContainsKey(statEntry.Key))
						continue;

					var path = actorMapping[statEntry.Key].Value.Value.Replace(" ", "").Split(',').ToList();

					if (path[0] != type)
						continue;

					path.RemoveAt(0);

					SetProperty(result, path, statEntry.Value);
				}
			}

			return result;
		}

		void SetProperty(MiniYaml result, List<string> path, object value)
		{
			if (path.Count == 1)
			{
				if (value is IDictionary)
				{
					var node = new MiniYamlNode(path[0], "");
					foreach (DictionaryEntry entry in (IDictionary)value)
						node.Value.Nodes.Add(new MiniYamlNode(entry.Key.ToString(), entry.Value.ToString()));
					result.Nodes.Add(node);
				}
				else
					result.Nodes.Add(new MiniYamlNode(path[0], value.ToString()));
			}
			else
			{
				if (result.Nodes.All(node => node.Key != path[0]))
					result.Nodes.Add(new MiniYamlNode(path[0], new MiniYaml(null)));

				var child = result.Nodes.First(node => node.Key == path[0]).Value;
				path.RemoveAt(0);
				SetProperty(child, path, value);
			}
		}

		public object GetEntry(string actor, string property)
		{
			if (!Entries.ContainsKey(actor) || !Entries[actor].ContainsKey(property))
				return null;

			return Entries[actor][property];
		}

		string GetPath()
		{
			var path = Game.ModData.Manifest.StatsFolders.First(node => node.Value == "User").Key;
			if (path.StartsWith("~", StringComparison.Ordinal))
				path = path.Substring(1);
			return Path.Combine(path, Regex.Replace(Author + "_" + Name + "_" + Version, "[^\\w\\.\\-]", "") + ".yaml");
		}

		public void Save()
		{
			File.WriteAllText(Platform.ResolvePath(GetPath()), Serialize());
		}

		public void Delete()
		{
			File.Delete(Platform.ResolvePath(GetPath()));
		}

		public static IEnumerable<Stats> AvailableStats()
		{
			foreach (var kv in Game.ModData.Manifest.StatsFolders)
			{
				var name = kv.Key;

				IReadOnlyPackage package;
				var optional = name.StartsWith("~", StringComparison.Ordinal);
				if (optional)
					name = name.Substring(1);

				try
				{
					// HACK: If the path is inside the the support directory then we may need to create it
					if (Platform.IsPathRelativeToSupportDirectory(name))
					{
						// Assume that the path is a directory if there is not an existing file with the same name
						var resolved = Platform.ResolvePath(name);
						if (!File.Exists(resolved))
							Directory.CreateDirectory(resolved);
					}

					package = Game.ModData.ModFiles.OpenPackage(name);
				}
				catch
				{
					if (optional)
						continue;

					throw;
				}

				foreach (var stats in package.Contents.Where(file => file.EndsWith(".yaml")))
					yield return Deserialize(package.GetStream(stats).ReadAllText(), kv.Value);
			}
		}
	}
}
