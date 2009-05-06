// Copyright (C) 2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Gear;
using Gear.Helpers;
using MCocoa;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	// Helper class to make it easier to work with ISccs interfaces.
	public static class Sccs
	{
		public static void Rename(string oldPath, string newPath)
		{
			Contract.Requires(!string.IsNullOrEmpty(oldPath), "oldPath is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(newPath), "newPath is null or empty");
			Contract.Requires(!Paths.AreEqual(oldPath, newPath), "oldPath equals newPath");
			
			try
			{
				// Use a real sccs,
				Boss boss = ObjectModel.Create("Sccs");
				if (boss.Has<ISccs>())
				{
					var candidate = boss.Get<ISccs>();
					while (candidate != null)
					{
						if (candidate.Rename(oldPath, newPath))
							return;
						
						candidate = boss.GetNext<ISccs>(candidate);
					}
				}
				
				// or the fallback.
				boss = ObjectModel.Create("Application");
				var fallback = boss.Get<ISccs>();
				if (!fallback.Rename(oldPath, newPath))
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Rename failed.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		public static void Duplicate(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			try
			{
				// Use a real sccs,
				Boss boss = ObjectModel.Create("Sccs");
				if (boss.Has<ISccs>())
				{
					var candidate = boss.Get<ISccs>();
					while (candidate != null)
					{
						if (candidate.Duplicate(path))
							return;
						
						candidate = boss.GetNext<ISccs>(candidate);
					}
				}
				
				// or the fallback.
				boss = ObjectModel.Create("Application");
				var fallback = boss.Get<ISccs>();
				if (!fallback.Duplicate(path))
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Duplicate failed.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		// Returns the name of each supported sccs along with all the commands
		// it supports.
		public static Dictionary<string, string[]> GetCommands()
		{
			var commands = new Dictionary<string, string[]>();
			
			Boss boss = ObjectModel.Create("Sccs");
			if (boss.Has<ISccs>())
			{
				var sccs = boss.Get<ISccs>();
				while (sccs != null)
				{
					commands.Add(sccs.Name, sccs.GetCommands());
					sccs = boss.GetNext<ISccs>(sccs);
				}
			}
			
			// or the fallback.
			boss = ObjectModel.Create("Application");
			var fallback = boss.Get<ISccs>();
			commands.Add(fallback.Name, fallback.GetCommands());
			
			return commands;
		}
		
		// Returns the name of each supported sccs along with all the commands
		// it supports for every specified path.
		public static Dictionary<string, string[]> GetCommands(IEnumerable<string> paths)
		{
			var commands = new Dictionary<string, string[]>();
			
			if (paths.Count() > 0)
			{
				Boss boss = ObjectModel.Create("Sccs");
				if (boss.Has<ISccs>())
				{
					var sccs = boss.Get<ISccs>();
					while (sccs != null)
					{
						commands.Add(sccs.Name, sccs.GetCommands(paths));
						sccs = boss.GetNext<ISccs>(sccs);
					}
				}
				
				// or the fallback.
				boss = ObjectModel.Create("Application");
				var fallback = boss.Get<ISccs>();
				commands.Add(fallback.Name, fallback.GetCommands(paths));
			}
			
			return commands;
		}
		
		// Executes the command for a single sccs.
		public static void Execute(string command, string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(command), "command is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			try
			{
				// Use a real sccs,
				Boss boss = ObjectModel.Create("Sccs");
				if (boss.Has<ISccs>())
				{
					var candidate = boss.Get<ISccs>();
					while (candidate != null)
					{
						if (Array.IndexOf(candidate.GetCommands(), command) >= 0)
						{
							candidate.Execute(command, path);
							return;
						}
						
						candidate = boss.GetNext<ISccs>(candidate);
					}
				}
				
				// or the fallback.
				boss = ObjectModel.Create("Application");
				var fallback = boss.Get<ISccs>();
				if (Array.IndexOf(fallback.GetCommands(), command) >= 0)
					fallback.Execute(command, path);
				else
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				NSString title = NSString.Create(command + " failed.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
	}
}
