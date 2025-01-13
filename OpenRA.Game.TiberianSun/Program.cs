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

namespace OpenRA.Game.TiberianSun;

using System;
using System.Linq;
using OpenRA.Launcher;
using OpenRA.Mods.Cnc.SpriteLoaders;
using OpenRA.Mods.Common.SpriteLoaders;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (!AppDomain.CurrentDomain.GetAssemblies().Contains(typeof(PngSheetLoader).Assembly))
            throw new Exception("Assembly loading broken!");
        if (!AppDomain.CurrentDomain.GetAssemblies().Contains(typeof(TmpTSLoader).Assembly))
            throw new Exception("Assembly loading broken!");

        DefaultProgram.Main("OpenRA", "Tiberian Sun", "ts", args);
    }
}
