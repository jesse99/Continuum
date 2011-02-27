// Copyright (C) 2010 Jesse Jones
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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Git
{
	internal sealed class Git : ISccs
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			try
			{
				var result = DoCommand(null, "help");
				if (result.Item1.Contains("usage") && string.IsNullOrEmpty(result.Item2) && result.Item3 == 0)
					m_installled = true;
			}
			catch
			{
				Log.WriteLine(TraceLevel.Info, "Errors", "git is not installed");
			}
			
			if (m_installled)
			{
				// Note that if this is changed GetCommands must be updated as well.
				m_commands.Add(Name + " blame", this.DoBlame);
				m_commands.Add(Name + " checkout", this.DoCheckout);
				m_commands.Add(Name + " diff", this.DoDiff);
				m_commands.Add(Name + "k", this.DoGitk);
				m_commands.Add(Name + " log", this.DoLog);
			}
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string Name
		{
			get {return "Git";}
		}
		
		public bool Rename(string oldPath, string newPath)
		{
			bool renamed = false;
			
			if (m_installled && DoIsControlled(oldPath))
			{
				var result = DoCommand(oldPath, "mv '{0}' '{1}'", oldPath, newPath);
				if (string.IsNullOrEmpty(result.Item2) && result.Item3 == 0)
					renamed = true;
			}
			
			return renamed;
		}
		
		public bool Duplicate(string path)
		{
			return false;		// let the fallback handle it
		}
		
		public string[] GetCommands()
		{
			return m_commands.Keys.ToArray();
		}
		
		public string[] GetCommands(IEnumerable<string> paths)
		{
			Contract.Requires(paths.Any(), "paths is empty");
			
			List<string> commands = new List<string>(m_commands.Count);
			
			if (m_installled)
			{
				int numModified = paths.Count(this.DoIsModified);
				int numControlled = paths.Count(this.DoIsControlled);
				
				if (numModified == paths.Count())
				{
					commands.Add(Name + " blame");
					commands.Add(Name + " checkout");
					commands.Add(Name + " diff");
					commands.Add(Name + " log");
				}
				else if (numControlled == paths.Count())
				{
					commands.Add(Name + " blame");
					commands.Add(Name + " log");
				}
				
				if (numControlled == 1 && paths.Count() == 1)
				{
					commands.Add(Name + "k");
				}
			}
			
			return commands.ToArray();
		}
		
		public void Execute(string command, string path)
		{
			Contract.Requires(m_commands.ContainsKey(command), command + " is not a git command");
			
			m_commands[command](path);
		}
		
		#region Private Methods
		private bool DoIsControlled(string path)
		{
			bool controlled = false;
			
			if (m_installled)
			{
				try
				{
					var result = DoCommand(path, "status '{0}'", path);
					if (string.IsNullOrEmpty(result.Item2) && result.Item3 == 0)
						controlled = true;
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("Error calling git status:");	// this should not happen
					Console.Error.WriteLine(e.Message);
				}
			}
			
			return controlled;
		}
		
		private bool DoIsModified(string path)
		{
			bool modified = false;
			
			if (m_installled)
			{
				try
				{
					var result = DoCommand(path, "status -s '{0}'", path);
					if (string.IsNullOrEmpty(result.Item2) && result.Item3 == 0)
						if (result.Item1.Trim().StartsWith("M "))
							modified = true;
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("Error calling git status:");	// this should not happen
					Console.Error.WriteLine(e.Message);
				}
			}
			
			return modified;
		}
		
		private Tuple<string, string, int> DoCommand(string path, string format, params object[] args)
		{
			return DoCommand(path, string.Format(format, args));
		}
		
		private Tuple<string, string, int> DoCommand(string path, string command)
		{
			string stdout, stderr;
			int err;
			
			using (Process process = new Process())
			{
				process.StartInfo.FileName = "git";
				process.StartInfo.Arguments = command;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				
				if (path != null)
				{
					if (!Directory.Exists(path))
						path = Path.GetDirectoryName(path);
						
					process.StartInfo.WorkingDirectory = path;
				}
				
				process.Start();
				
				stdout = process.StandardOutput.ReadToEnd();
				stderr = process.StandardError.ReadToEnd();
				process.WaitForExit();
				err = process.ExitCode;
			}
			
			return Tuple.Create(stdout, stderr, err);
		}
		
		private void DoBlame(string path)
		{
			var result = DoCommand(path, "blame --date=relative '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			else if (result.Item3 != 0)
				throw new InvalidOperationException(string.Format("git result code was {0}", result.Item3));
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path), ".git-blame");
			
			try
			{
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", result.Item1);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			catch (IOException e)	// can sometimes land here if too many files are open (max is system wide and only 256)
			{
				NSString title = NSString.Create("Couldn't process '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private void DoCheckout(string path)
		{
			var result = DoCommand(path, "checkout '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			else if (result.Item3 != 0)
				throw new InvalidOperationException(string.Format("git result code was {0}", result.Item3));
		}
		
		private void DoDiff(string path)
		{
			var result = DoCommand(path, "diff --no-color --ignore-all-space '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			else if (result.Item3 != 0)
				throw new InvalidOperationException(string.Format("git result code was {0}", result.Item3));
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path), ".diff");
			
			try
			{
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", result.Item1);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			catch (IOException e)	// can sometimes land here if too many files are open (max is system wide and only 256)
			{
				NSString title = NSString.Create("Couldn't process '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private void DoGitk(string path)
		{
			using (Process process = new Process())
			{
				process.StartInfo.FileName = "gitk";
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = false;
				process.StartInfo.RedirectStandardError = false;
				
				if (path != null)
				{
					if (!Directory.Exists(path))
						path = Path.GetDirectoryName(path);
						
					process.StartInfo.WorkingDirectory = path;
				}
				
				process.Start();
			}
		}
		
		private void DoLog(string path)
		{
			var result = DoCommand(path, "log --no-color --relative-date '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			else if (result.Item3 != 0)
				throw new InvalidOperationException(string.Format("git result code was {0}", result.Item3));
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path), ".git-log");
			
			try
			{
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", result.Item1);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			catch (IOException e)	// can sometimes land here if too many files are open (max is system wide and only 256)
			{
				NSString title = NSString.Create("Couldn't process '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private bool m_installled;
		private Dictionary<string, Action<string>> m_commands = new Dictionary<string, Action<string>>();
		#endregion
	}
}
