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
using System.Linq;
using System.Reflection;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
    class StringProperty
    {
        public TextFieldWidget Widget;
        public string DefaultValue;
        public string LoadedValue;
        public bool IsChanged { get { return Value != LoadedValue; } }
        public string Value { get { return Widget.Text; } set { Widget.Text = value; } }
    }

    class DictionaryProperty
    {
        public ContainerWidget Widget;
        public Dictionary<string, string> DefaultValue;
        public Dictionary<string, string> LoadedValue;
        public bool IsChanged
        {
            get
            {
                var value = Value;
                if (value.Count != LoadedValue.Count)
                    return true;

                foreach (var entry in value)
                    if (!LoadedValue.ContainsKey(entry.Key) || LoadedValue[entry.Key] != entry.Value)
                        return true;

                return false;
            }
        }

        public bool IsDefault
        {
            get
            {
                var value = Value;
                if (value.Count != DefaultValue.Count)
                    return false;

                foreach (var entry in value)
                    if (!DefaultValue.ContainsKey(entry.Key) || DefaultValue[entry.Key] != entry.Value)
                        return false;

                return true;
            }
        }

        public Dictionary<string, string> Value
        {
            get
            {
                var result = new Dictionary<string, string>();

                foreach (var child in Widget.Children.Where(child => child.IsVisible() && child is ContainerWidget))
                {
                    var key = child.Get<TextFieldWidget>("KEY").Text;
                    if (key == "" || result.ContainsKey(key))
                        continue;

                    var value = child.Get<TextFieldWidget>("VALUE").Text;

                    if (value == "")
                    {
                        if (DefaultValue.ContainsKey(key))
                            value = DefaultValue[key];
                        else
                            continue;
                    }

                    result.Add(key, value);
                }

                return result;
            }

            set
            {
                var dictionaryItemTemplate = Widget.Get<ContainerWidget>("PROPERTIES_DICTIONARY_ITEM_TEMPLATE");

                var children = Widget.Children.Where(child => child is ContainerWidget && child != dictionaryItemTemplate).ToArray();
                foreach (var child in children)
                    Widget.RemoveChild(child);

                var values = value.Keys;
                if (values.Count == 0)
                    values = DefaultValue.Keys;

                foreach (var key in values)
                {
                    var dictionaryItem = (ContainerWidget)dictionaryItemTemplate.Clone();
                    dictionaryItem.IsVisible = () => true;

                    var keyField = dictionaryItem.Get<TextFieldWidget>("KEY");
                    var valueField = dictionaryItem.Get<TextFieldWidget>("VALUE");

                    keyField.OnTextEdited = () => valueField.Placeholder = DefaultValue.ContainsKey(keyField.Text) ? DefaultValue[keyField.Text] : "";
                    keyField.Text = key;
                    keyField.OnTextEdited();

                    valueField.Text = value.ContainsKey(key) && (!DefaultValue.ContainsKey(key) || value[key] != DefaultValue[key]) ? value[key] : "";

                    var removeButton = dictionaryItem.Get<ButtonWidget>("REMOVE");
                    removeButton.OnClick = () =>
                    {
                        Widget.RemoveChild(dictionaryItem);
                        UpdateBounds();
                    };

                    Widget.AddChild(dictionaryItem);
                }

                UpdateBounds();
            }
        }

        private void UpdateBounds()
        {
            Widget.Bounds.Height = 30;

            foreach (var child in Widget.Children.Where(child => child.IsVisible() && child is ContainerWidget))
            {
                child.Bounds.Y = Widget.Bounds.Height - 30;
                Widget.Bounds.Height += 30;
            }

            foreach (var child in Widget.Children.Where(child => child is ButtonWidget))
                child.Bounds.Y = Widget.Bounds.Height - 25;

            ((ScrollPanelWidget)Widget.Parent).Layout.AdjustChildren();
        }
    }

    class PropertyInfo
    {
        public object Value;
        public Type Type = typeof(string);
    }

    public class StatsEditorLogic : ChromeLogic
    {
        Dictionary<string, MiniYamlNode> statsYaml;

        ContainerWidget overview;
        ContainerWidget editor;
        bool inEditor;

        ScrollPanelWidget statsList;
        ScrollItemWidget statsItemTemplate;

        ScrollPanelWidget actorsList;
        ScrollPanelWidget propertiesList;
        LabelWidget actorsGroupTemplate;
        ScrollItemWidget actorsItemTemplate;
        LabelWidget propertiesGroupTemplate;
        ContainerWidget propertiesItemTemplate;
        ContainerWidget dictionaryTemplate;

        Dictionary<Stats, ScrollItemWidget> availableStats = new Dictionary<Stats, ScrollItemWidget>();

        Stats currentStats;
        string currentActor;

        Dictionary<string, Dictionary<string, object>> properties = new Dictionary<string, Dictionary<string, object>>();

        [ObjectCreator.UseCtor]
        public StatsEditorLogic(Widget widget, Action onExit)
        {
            // TODO define strictly which stats.yaml!
            statsYaml = MiniYaml.FromString(Game.ModData.DefaultFileSystem.Open("stats.yaml").ReadAllText()).ToDictionary(node => node.Key);

            overview = widget.Get<ContainerWidget>("OVERVIEW");
            overview.IsVisible = () => !inEditor;
            editor = widget.Get<ContainerWidget>("EDITOR");
            editor.IsVisible = () => inEditor;

            statsList = overview.Get<ScrollPanelWidget>("STATS_LIST");
            statsItemTemplate = statsList.Get<ScrollItemWidget>("STATS_ITEM_TEMPLATE");

            actorsList = editor.Get<ScrollPanelWidget>("ACTORS_LIST");
            propertiesList = editor.Get<ScrollPanelWidget>("PROPERTIES_LIST");
            actorsGroupTemplate = actorsList.Get<LabelWidget>("ACTORS_GROUP_TEMPLATE");
            actorsItemTemplate = actorsList.Get<ScrollItemWidget>("ACTORS_ITEM_TEMPLATE");
            propertiesGroupTemplate = propertiesList.Get<LabelWidget>("PROPERTIES_GROUP_TEMPLATE");
            propertiesItemTemplate = propertiesList.Get<ContainerWidget>("PROPERTIES_ITEM_TEMPLATE");
            dictionaryTemplate = propertiesList.Get<ContainerWidget>("PROPERTIES_DICTIONARY_TEMPLATE");

            SetupOverview(onExit);
            SetupEditor();
            PrepareEditorWidgets();

            foreach (var stats in Stats.AvailableStats())
                AddStats(stats);
        }

        private void SetupOverview(Action onExit)
        {
            var closeButton = overview.Get<ButtonWidget>("EXIT_BUTTON");
            closeButton.OnClick = () =>
            {
                Ui.CloseWindow();
                onExit();
            };

            var newButton = overview.Get<ButtonWidget>("NEW");
            newButton.OnClick = () =>
            {
                currentStats = new Stats();
                UpdateEditor();
            };

            var editButton = overview.Get<ButtonWidget>("EDIT");
            editButton.IsVisible = () => currentStats != null;
            editButton.OnClick = () =>
            {
                if (currentStats.Type == "System")
                    currentStats = Stats.Deserialize(currentStats.Serialize());

                UpdateEditor();
            };

            var deleteButton = overview.Get<ButtonWidget>("DELETE");
            deleteButton.IsVisible = () => currentStats != null;
            deleteButton.IsDisabled = () => currentStats.Type == "System";
            deleteButton.OnClick = () =>
            {
                currentStats.Delete();
                statsList.RemoveChild(availableStats[currentStats]);
                availableStats.Remove(currentStats);
                currentStats = null;
            };
        }

        private void AddStats(Stats stats)
        {
            var statsItem = (ScrollItemWidget)statsItemTemplate.Clone();
            statsItem.IsSelected = () => stats == currentStats;
            statsItem.IsVisible = () => true;
            statsItem.OnClick = () => currentStats = stats == currentStats ? null : stats;
            statsItem.Get<LabelWidget>("LABEL").GetText = () => stats.Name + " by " + stats.Author + " " + stats.Version;
            statsList.AddChild(statsItem);
            availableStats.Add(stats, statsItem);
        }

        private void SetupEditor()
        {
            var saveButton = editor.Get<ButtonWidget>("SAVE");
            saveButton.OnClick = () =>
            {
                if (editor.Get<TextFieldWidget>("NAME_FIELD").Text.Trim() == "")
                    return;
                if (editor.Get<TextFieldWidget>("VERSION_FIELD").Text.Trim() == "")
                    return;
                if (editor.Get<TextFieldWidget>("AUTHOR_FIELD").Text.Trim() == "")
                    return;

                UpdateStats();

                inEditor = false;

                if (!availableStats.ContainsKey(currentStats))
                    AddStats(currentStats);

                inEditor = true;
            };

            var backButton = editor.Get<ButtonWidget>("BACK");
            backButton.OnClick = () =>
            {
                // TODO show "Are you sure..?"
                if (!availableStats.ContainsKey(currentStats))
                    currentStats = null;

                inEditor = false;
            };
        }

        private void PrepareEditorWidgets()
        {
            foreach (var actor in statsYaml)
            {
                var localActor = actor;
                if (actor.Key.StartsWith("Group@"))
                {
                    var actorsGroup = (LabelWidget)actorsGroupTemplate.Clone();
                    actorsGroup.IsVisible = () => true;
                    actorsGroup.GetText = () => localActor.Key.Split('@')[1];
                    actorsList.AddChild(actorsGroup);
                }
                else
                {
                    var actorsItem = (ScrollItemWidget)actorsItemTemplate.Clone();
                    actorsItem.IsVisible = () => true;
                    actorsItem.Get<LabelWidget>("LABEL").GetText = () => localActor.Key;
                    actorsItem.OnClick = () =>
                    {
                        currentActor = currentActor == localActor.Key ? null : localActor.Key;
                        propertiesList.Layout.AdjustChildren();
                    };
                    actorsItem.IsSelected = () => currentActor == localActor.Key;
                    actorsList.AddChild(actorsItem);

                    var actorProperties = new Dictionary<string, object>();
                    properties.Add(localActor.Key, actorProperties);

                    actor.Value.Value.Nodes.ForEach(node =>
                    {
                        if (node.Key.StartsWith("Group@"))
                        {
                            var propertiesGroup = (LabelWidget)propertiesGroupTemplate.Clone();
                            propertiesGroup.IsVisible = () => currentActor == localActor.Key;
                            propertiesGroup.GetText = () => node.Key.Split('@')[1];
                            propertiesList.AddChild(propertiesGroup);
                        }
                        else
                        {
                            var valueInfo = GetPropertyInfo(node.Value.Value.Replace(" ", "").Split(',').ToList());

                            if (valueInfo.Type.IsGenericType && valueInfo.Type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                            {
                                var dictionary = (ContainerWidget)dictionaryTemplate.Clone();
                                dictionary.IsVisible = () => currentActor == localActor.Key;
                                dictionary.Get<LabelWidget>("LABEL").GetText = () => node.Key;
                                propertiesList.AddChild(dictionary);

                                var property = new DictionaryProperty();
                                property.DefaultValue = new Dictionary<string, string>();

                                foreach (DictionaryEntry entry in (IDictionary)valueInfo.Value)
                                    property.DefaultValue.Add(entry.Key.ToString(), entry.Value.ToString());

                                property.Widget = dictionary;
                                actorProperties.Add(node.Key, property);

                                var addButton = dictionary.Get<ButtonWidget>("ADD");
                                addButton.OnClick = () =>
                                {
                                    var values = property.Value;
                                    values.Add("", "");
                                    property.Value = values;
                                };
                                addButton.IsDisabled = () => property.Value.ContainsKey("");

                                var defaultButton = dictionary.Get<ButtonWidget>("DEFAULT");
                                defaultButton.OnClick = () => property.Value = property.LoadedValue;
                                defaultButton.IsDisabled = () => !property.IsChanged;
                            }
                            else
                            {
                                var value = "";
                                if (valueInfo.Value != null)
                                {
                                    if (valueInfo.Type.IsArray)
                                    {
                                        var values = (Array)valueInfo.Value;
                                        for (var i = 0; i < values.Length; i++)
                                        {
                                            if (i > 0)
                                                value += ",";
                                            value += values.GetValue(i);
                                        }
                                    }
                                    else
                                        value = valueInfo.Value.ToString();
                                }

                                var propertiesItem = (ContainerWidget)propertiesItemTemplate.Clone();
                                propertiesItem.IsVisible = () => currentActor == localActor.Key;
                                propertiesItem.Get<LabelWidget>("LABEL").GetText = () => node.Key;
                                propertiesList.AddChild(propertiesItem);

                                var property = new StringProperty();
                                property.DefaultValue = value;
                                property.Widget = propertiesItem.Get<TextFieldWidget>("VALUE");
                                property.Widget.Placeholder = property.DefaultValue;
                                actorProperties.Add(node.Key, property);

                                var defaultButton = propertiesItem.Get<ButtonWidget>("DEFAULT");
                                defaultButton.OnClick = () => property.Value = property.LoadedValue;
                                defaultButton.IsDisabled = () => !property.IsChanged;
                            }
                        }
                    });
                }
            }
        }

        private PropertyInfo GetPropertyInfo(List<string> path)
        {
            var info = new PropertyInfo();

            if (path[0] == "rules")
            {
                var parts = path[2].Split(ActorInfo.TraitInstanceSeparator);
                var trait = Game.ModData.DefaultRules.Actors[path[1].ToLower()].TraitInfos<TraitInfo>().First(entry =>
                {
                    if (!entry.GetType().Name.EndsWith(parts[0] + "Info"))
                        return false;

                    if (parts.Length == 1 && entry.InstanceName == null)
                        return true;

                    return parts.Length > 1 && parts[1] == entry.InstanceName;
                });

                path.RemoveRange(0, 3);
                UpdatePropertyInfo(info, trait, path);
            }
            else if (path[0] == "weapons")
            {
                var weapon = Game.ModData.DefaultRules.Weapons[path[1].ToLower()];
                path.RemoveRange(0, 2);

                if (path.Count == 1)
                    UpdatePropertyInfo(info, weapon, path);
                else
                {
                    if (path[0] == "Projectile")
                    {
                        path.RemoveRange(0, 1);
                        UpdatePropertyInfo(info, weapon.Projectile, path);
                    }
                    else
                    {
                        var warhead = weapon.Warheads.First(entry =>
                        {
                            var wEntry = entry as Warhead;

                            return wEntry != null && wEntry.InstanceName == path[0];
                        });

                        path.RemoveRange(0, 1);
                        UpdatePropertyInfo(info, warhead, path);
                    }
                }
            }

            return info;
        }

        private void UpdatePropertyInfo(PropertyInfo info, object trait, List<string> path)
        {
            var field = trait.GetType().GetField(path[0], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                info.Type = field.FieldType;
                info.Value = field.GetValue(trait);
            }
        }

        void UpdateEditor()
        {
            editor.Get<TextFieldWidget>("NAME_FIELD").Text = currentStats.Name;
            editor.Get<TextFieldWidget>("VERSION_FIELD").Text = currentStats.Version;
            editor.Get<TextFieldWidget>("AUTHOR_FIELD").Text = currentStats.Author;
            editor.Get<TextFieldWidget>("DESCRIPTION_FIELD").Text = currentStats.Description;

            foreach (var actor in statsYaml)
            {
                if (actor.Key.StartsWith("Group@"))
                    continue;

                actor.Value.Value.Nodes.ForEach(node =>
                {
                    if (node.Key.StartsWith("Group@"))
                        return;

                    var stringProperty = properties[actor.Key][node.Key] as StringProperty;
                    var dictionaryProperty = properties[actor.Key][node.Key] as DictionaryProperty;
                    if (stringProperty != null)
                    {
                        stringProperty.LoadedValue = (currentStats.GetEntry(actor.Key, node.Key) ?? "").ToString();
                        stringProperty.Value = stringProperty.LoadedValue ?? "";
                    }
                    else if (dictionaryProperty != null)
                    {
                        var values = new Dictionary<string, string>();
                        var loaded = currentStats.GetEntry(actor.Key, node.Key) as IDictionary;

                        if (loaded != null)
                            foreach (DictionaryEntry entry in loaded)
                                values.Add(entry.Key.ToString(), entry.Value.ToString());
                        else
                            values = dictionaryProperty.DefaultValue;

                        dictionaryProperty.LoadedValue = values;
                        dictionaryProperty.Value = dictionaryProperty.LoadedValue;
                    }
                });
            }

            inEditor = true;
        }

        void UpdateStats()
        {
            currentStats.Name = editor.Get<TextFieldWidget>("NAME_FIELD").Text;
            currentStats.Version = editor.Get<TextFieldWidget>("VERSION_FIELD").Text;
            currentStats.Author = editor.Get<TextFieldWidget>("AUTHOR_FIELD").Text;
            currentStats.Description = editor.Get<TextFieldWidget>("DESCRIPTION_FIELD").Text;

            currentStats.Entries.Clear();

            foreach (var actor in statsYaml)
            {
                if (actor.Key.StartsWith("Group@"))
                    continue;

                currentStats.Entries.Add(actor.Key, new Dictionary<string, object>());
                actor.Value.Value.Nodes.ForEach(node =>
                {
                    if (node.Key.StartsWith("Group@"))
                        return;

                    var stringProperty = properties[actor.Key][node.Key] as StringProperty;
                    var dictionaryProperty = properties[actor.Key][node.Key] as DictionaryProperty;
                    if (stringProperty != null && stringProperty.Value != "")
                        currentStats.Entries[actor.Key].Add(node.Key, stringProperty.Value);
                    else if (dictionaryProperty != null && !dictionaryProperty.IsDefault)
                        currentStats.Entries[actor.Key].Add(node.Key, dictionaryProperty.Value);
                });
            }

            currentStats.Save();
        }
    }
}
