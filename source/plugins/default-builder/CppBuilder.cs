// Copyright (C) 2011 Jesse Jones
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
using Shared;
using System;
using System.Diagnostics;
using System.IO;

namespace DefaultBuilder
{
	internal sealed class CppBuilder : IBuilder
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(string path)
		{
			m_path = path;
			
//			string contents = File.ReadAllText(path, System.Text.Encoding.UTF8);
//			m_parser = new WafParser(contents);
//			m_variables = new List<Variable>(m_parser.Variables);
			
//			DoLoadPrefs();
		}
		
		public static string[] Globs
		{
			get {return new string[]{"*.c", "*.cpp", "*.cxx", "*.cc"};}
		}
		
		public string DefaultTarget
		{
			get {return "build";}
		}
		
		public string[] Targets
		{
			get {return new string[]{"build", "run", "buildAndRun", "clean"};}
		}
		
		public bool StderrIsExpected
		{
			get {return false;}
		}
		
		public string Command
		{
			get {return m_command + Environment.NewLine;}
		}
		
		public Process Build(string target)
		{
			DoBuildCommandLine(target);
			
//			foreach (KeyValuePair<string, int> f in flags)
//				if (f.Key == "verbosity" && f.Value > 0)
//					args += string.Format("-{0} ", new string('v', f.Value));
//				else if (f.Key == "jobs" && f.Value != 8)
//					args += string.Format("--jobs={0} ", f.Value);
//				else if (f.Value == 1)
//					args += string.Format("--{0} ", f.Key);
//			
//			foreach (Variable v in vars)
//				if (v.Value.Length > 0 && v.Value != v.DefaultValue)
//					if (v.Value.IndexOf(' ') >= 0)
//						args += string.Format("{0}=\"{1}\" ", v.Name, v.Value);
//					else
//						args += string.Format("{0}={1} ", v.Name, v.Value);
//			args += target;
			
//			m_command = "python " + args + Environment.NewLine;
			
			var process = new Process();
			process.StartInfo.FileName = "sh";
			process.StartInfo.Arguments = ".build.sh";
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.WorkingDirectory = m_path;
			
			return process;
		}
		
		public void SetBuildFlags()
		{
//			var controller = new WafFlagsController(m_flags);
//			Unused.Value = NSApplication.sharedApplication().runModalForWindow(controller.window());
//			controller.release();
//			
//			DoSavePrefs();
		}
		
		public void SetBuildVariables()
		{
//			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
//			var vars = boss.Get<IBuildVariables>();
//			if (vars.Change(m_variables))
//				DoSavePrefs();
		}
		
		#region Private Methods
		private void DoBuildCommandLine(string target)
		{
			if (m_outDir == null)
				m_outDir = Path.Combine(m_path, "bin");
				
			string exe = Path.GetFileName(m_path);
			exe = Path.ChangeExtension(exe, ".exe");
			exe = Path.Combine(m_outDir, exe);
			exe = DoGetRelativePath(exe);
				
			switch (target)
			{
				case "build":
					m_command = DoGetBuildCommand(exe);
					break;
					
				case "run":
					m_command = DoGetRunCommand(exe);
					break;
					
				case "buildAndRun":
					m_command = string.Format("{0} && {1}", DoGetBuildCommand(exe), DoGetRunCommand(exe));
					break;
					
				case "clean":
					m_command = DoGetCleanCommand();
					break;
					
				default:
					Contract.Assert(false, "bad target: " + target);
					break;
			}
			
			string outFile = Path.Combine(m_path, ".build.sh");
			File.WriteAllText(outFile, m_command);
		}
		
		private string DoGetBuildCommand(string exe)
		{
			if (!Directory.Exists(m_outDir))
				Directory.CreateDirectory(m_outDir);
			
			return string.Format("g++ -o {0} {1}", exe, DoGetSrcFiles());
		}
		
		private string DoGetRunCommand(string exe)
		{
			return exe;
		}
		
		private string DoGetCleanCommand()
		{
			return string.Format("rm -rf '{0}' && rm -f .build.sh", m_outDir);
		}
		
		private string DoGetSrcFiles()
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (string glob in Globs)
			{
				foreach (string path in Directory.GetFiles(m_path, glob, SearchOption.AllDirectories))
				{
					builder.Append(DoGetRelativePath(path));
					builder.Append(' ');
				}
			}
			
			return builder.ToString();
		}
		
		private string DoGetRelativePath(string path)
		{
			Contract.Requires(!path.EndsWith("/"));
			
			if (path.StartsWith(m_path + "/"))
				path = path.Substring(m_path.Length + 1);
			
			if (path.Contains(" "))
				path = string.Format("'{0}'", path);
				
			return path;
		}
		
//		private void DoSavePrefs()
//		{
//			// custom flags
//			string key = m_path + "-variables";		// this will break if the project is moved, but that should be rather rare
//			
//			var dict = NSMutableDictionary.Create();
//			foreach (var entry in m_variables)
//			{
//				if (entry.Value.Length > 0 && entry.Value != entry.DefaultValue)
//					dict.setObject_forKey(NSString.Create(entry.Value), NSString.Create(entry.Name));
//			}
//			
//			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
//			defaults.setObject_forKey(dict, NSString.Create(key));
//			
//			// standard flags
//			key = m_path + "-flags";
//			
//			dict = NSMutableDictionary.Create();
//			foreach (var entry in m_flags)
//			{
//				dict.setObject_forKey(NSString.Create(entry.Value.ToString()), NSString.Create(entry.Key));
//			}
//			
//			defaults.setObject_forKey(dict, NSString.Create(key));
//		}
		
//		private void DoLoadPrefs()
//		{
//			// custom flags
//			string key = m_path + "-variables";
//			string value;
//			
//			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
//			NSObject pref = defaults.objectForKey(NSString.Create(key));
//			if (!NSObject.IsNullOrNil(pref))
//			{
//				NSMutableDictionary dict = pref.To<NSMutableDictionary>();
//				
//				foreach (var entry in dict)
//				{
//					string name = entry.Key.ToString();
//					value = entry.Value.ToString();
//					
//					int i = m_variables.FindIndex(e => e.Name == name);
//					if (i >= 0)
//					{
//						Variable old = m_variables[i];
//						m_variables[i] = new Variable(old.Name, old.DefaultValue, value);
//					}
//					else
//					{
//						Variable v = new Variable(name, string.Empty, value);
//						i = m_variables.FindIndex(e => e.Name.Length == 0);
//						if (i >= 0)
//							m_variables.Insert(i, v);
//						else
//							m_variables.Add(v);
//					}
//				}
//			}
//			
//			// standard flags
//			key = m_path + "-flags";
//			pref = defaults.objectForKey(NSString.Create(key));
//			if (!NSObject.IsNullOrNil(pref))
//			{
//				NSMutableDictionary dict = pref.To<NSMutableDictionary>();
//				
//				foreach (var entry in dict)
//				{
//					string name = entry.Key.ToString();
//					value = entry.Value.ToString();
//					
//					m_flags[name] = int.Parse(value);
//				}
//			}
//		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_path;
		private string m_outDir;
		private string m_command = string.Empty;
		
//		private List<Variable> m_variables = new List<Variable>();
//		private Dictionary<string, int> m_flags = new Dictionary<string, int>();
		#endregion
	}
}
