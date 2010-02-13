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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Continuum
{
	internal static class Program
	{
		public static void Main(string[] args)
		{
			Log.WriteLine("Startup", "started up on {0}", DateTime.Now);
			
			// Note that we have to be careful not to use mobjc
			// until all the plugins are loaded so all classes are
			// properly registered.
			DoLoadPlugins();
			Log.WriteLine(TraceLevel.Verbose, "Startup", "loaded plugins");
			Registrar.CanInit = true;
			
			Boss appBoss = ObjectModel.Create("Application");
			IApplication app = appBoss.Get<IApplication>();
			Log.WriteLine(TraceLevel.Verbose, "Startup", "running app");
			app.Run(args);
			
			// note that we don't actually land here when quitting...
		}
		
		// TODO: about box should list all of the loaded plugins along with
		// their version numbers
		private static void DoLoadPlugins()
		{
			Gear.Helpers.Unused.Value = typeof(IStartup);		// force shared.dll to load (we need to do this or the plugins will fail when they try to use shared types from Bosses.xml)
			
			string loc = Assembly.GetExecutingAssembly().Location;
			string root = Path.GetDirectoryName(loc);
			string path = Path.Combine(root, "plugins");
			Log.WriteLine(TraceLevel.Verbose, "Startup", "loading plugins using '{0}'", path);
			
			// TODO: we might want required and optional plugins
			Plugins plugins = new Plugins(path, "*.dll");
			foreach (KeyValuePair<string, Exception> entry in plugins.Failures)
			{
				Console.Error.WriteLine("Failed to load {0}: {1}", entry.Key, entry.Value.Message);
				if (entry.Value.InnerException != null)
					Console.Error.WriteLine("   Inner Exception: {0}", entry.Value.InnerException);
			}
		}
	}
}
