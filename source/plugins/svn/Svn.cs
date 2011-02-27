// Copyright (C) 2007-2008 Jesse Jones
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

namespace Subversion
{
	internal sealed class Svn : ISccs
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			try
			{
				var result = DoCommand("help");
				if (result.Item1.Contains("usage") && string.IsNullOrEmpty(result.Item2))
					m_installled = true;
			}
			catch
			{
				Log.WriteLine(TraceLevel.Info, "Errors", "svn is not installed");
			}
			
			if (m_installled)
			{
				// Note that if this is changed GetCommands must be updated as well.
				m_commands.Add(Name + " add", this.DoAdd);
				m_commands.Add(Name + " blame", this.DoBlame);
				m_commands.Add(Name + " cat", this.DoCat);
				m_commands.Add(Name + " commit", this.DoCommit);
				m_commands.Add(Name + " diff", this.DoDiff);
				m_commands.Add(Name + " info", this.DoInfo);
				m_commands.Add(Name + " log", this.DoLog);
				m_commands.Add(Name + " propedit svn:ignore", this.DoEditIgnore);
				m_commands.Add(Name + " rename", this.DoRename);
				m_commands.Add(Name + " remove", this.DoRemove);
				m_commands.Add(Name + " resolved", this.DoResolved);
				m_commands.Add(Name + " revert", this.DoRevert);
				m_commands.Add(Name + " status", this.DoStatus);
				m_commands.Add(Name + " update", this.DoUpdate);
			}
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string Name
		{
			get {return "Svn";}
		}
		
		public bool Rename(string oldPath, string newPath)
		{
			bool renamed = false;
			
			if (m_installled && DoIsControlled(oldPath))
			{
				var result = DoCommand("move '{0}' '{1}'", oldPath, newPath);
				if (string.IsNullOrEmpty(result.Item2))
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
				char[] status = DoGetStatus(paths);
				int numModified = status.Count(s => s == 'M' || s == 'C');
				int numControlled = paths.Count(p => DoIsControlled(p));	// unfortunately svn stat won't tell us whether the file is not checked in or simply not dirty
				
				if (numModified == status.Length)
				{
					if (status.Length == 1)
						commands.Add(Name + " commit");		// if these names change SvnTextCommands should be reviewed
					
					int numConflicted = status.Count(s => s == 'C');
					if (numConflicted == paths.Count())
						commands.Add(Name + " resolved");
					
					commands.Add(Name + " diff");
					commands.Add(Name + " revert");
				}
				else if (numModified == 0 && numControlled == paths.Count())
				{
					if (paths.Count() == 1)
						commands.Add(Name + " rename");
					
					commands.Add(Name + " remove");
					commands.Add(Name + " update");
				}
				
				if (numControlled == paths.Count())
				{
					commands.Add(Name + " blame");
					commands.Add(Name + " cat");
					commands.Add(Name + " info");
					commands.Add(Name + " log");
					commands.Add(Name + " status");
					
					if (numControlled == 1)
						commands.Add(Name + " propedit svn:ignore");
				}
				else if (numControlled == 0)
				{
					int numParentsControlled = paths.Count(p => DoIsControlled(Path.GetDirectoryName(p)));	
					if (numParentsControlled == paths.Count())
						commands.Add(Name + " add");
				}
			}
			
			return commands.ToArray();
		}
		
		public void Execute(string command, string path)
		{
			Contract.Requires(m_commands.ContainsKey(command), command + " is not an Svn command");
			
			m_commands[command](path);
		}
		
		#region Private Methods
		public char[] DoGetStatus(IEnumerable<string> paths)
		{
			char[] status = new char[paths.Count()];
			
			try
			{
				int i = 0;
				foreach (string path in paths)
				{
					var result = DoCommand("stat '{0}'", path);
					if (string.IsNullOrEmpty(result.Item2) && result.Item1.Length > 0)
						status[i++] = result.Item1[0];
					else
						status[i++] = ' ';
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Error calling svn stat:");	// this should not happen
				Console.Error.WriteLine(e.Message);
			}
			
			return status;
		}
		
		private bool DoIsControlled(string path)
		{
			bool controlled = false;
			
			if (m_installled)
			{
				try
				{
					var result = DoCommand("info '{0}'", path);
					if (string.IsNullOrEmpty(result.Item2))
						controlled = result.Item1.StartsWith("Path:");	// unfortunately svn's return code is always zero so we need to parse the output...
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("Error calling svn info:");	// this should not happen
					Console.Error.WriteLine(e.Message);
				}
			}
			
			return controlled;
		}
		
		private Tuple<string, string> DoCommand(string format, params object[] args)
		{
			return DoCommand(string.Format(format, args));
		}
		
		private Tuple<string, string> DoCommand(string command)
		{
			string stdout, stderr;
			
			using (Process process = new Process())
			{
				process.StartInfo.FileName = "svn";
				process.StartInfo.Arguments = command;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				
				process.Start();
				
				stdout = process.StandardOutput.ReadToEnd();
				stderr = process.StandardError.ReadToEnd();
				process.WaitForExit();
			}
			
			return Tuple.Create(stdout, stderr);
		}
		
		private void DoAdd(string path)
		{
			var result = DoCommand("add '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
		}
		
		private void DoBlame(string path)
		{
			var result = DoCommand("blame '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path), ".svn-blame");
			
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
		
		private void DoCat(string path)
		{
			var result = DoCommand("cat '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path) + " (HEAD)", Path.GetExtension(path));
			
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
		
		private void DoCommit(string path)
		{
			string name = Path.GetFileName(path);
			
			var get = new GetString{Title = "Commit Message", Label = "Message:", Text = name + ": "};
			string message = get.Run();
			if (message != null)
			{
				var result = DoCommand("commit -m '{0}' '{1}'", message, path);
				if (!string.IsNullOrEmpty(result.Item2))
					throw new InvalidOperationException(result.Item2);
			}
		}
		
		private void DoDiff(string path)
		{
			var result = DoCommand("diff '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
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
		
		private void DoEditIgnore(string path)
		{
			if (File.Exists(path))
				path = Path.GetDirectoryName(path);
			
			var result = DoCommand("propget svn:ignore '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			var getter = new GetText{Title = "Ignore List", Text = result.Item1};
			string text = getter.Run();
			if (text != null)
			{
				result = DoCommand("propset svn:ignore '{0}' '{1}'", text, path);
				if (!string.IsNullOrEmpty(result.Item2))
					throw new InvalidOperationException(result.Item2);
			}
		}
		
		private void DoInfo(string path)
		{
			var result = DoCommand("info '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			
			transcript.Show();
			transcript.WriteLine(Output.Command, "Svn info " + path);
			transcript.WriteLine(Output.Normal, "{0}", result.Item1);
		}
		
		private void DoLog(string path)
		{
			var result = DoCommand("log '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(Path.GetFileNameWithoutExtension(path), ".svn-log");
			
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
		
		private void DoRename(string path)
		{
			string oldName = Path.GetFileName(path);
			
			var get = new GetString{Title = "New Name", Label = "Name:", Text = oldName};
			string newName = get.Run();
			if (newName != null && newName != oldName)
			{
				string oldDir = Path.GetDirectoryName(path);
				string newPath = Path.Combine(oldDir, newName);
				
				var result = DoCommand("move '{0}' '{1}'", path, newPath);
				if (!string.IsNullOrEmpty(result.Item2))
					throw new InvalidOperationException(result.Item2);
			}
		}
		
		private void DoRemove(string path)
		{
			var result = DoCommand("remove '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
		}
		
		private void DoResolved(string path)
		{
			var result = DoCommand("resolved '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
		}
		
		private void DoRevert(string path)
		{
			var result = DoCommand("revert '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
		}
		
		private void DoStatus(string path)
		{
			var result = DoCommand("status '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
			
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			
			transcript.Show();
			transcript.WriteLine(Output.Command, "Svn status " + path);
			transcript.WriteLine(Output.Normal, "{0}", result.Item1);
		}
		
		private void DoUpdate(string path)
		{
			var result = DoCommand("update '{0}'", path);
			if (!string.IsNullOrEmpty(result.Item2))
				throw new InvalidOperationException(result.Item2);
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private bool m_installled;
		private Dictionary<string, Action<string>> m_commands = new Dictionary<string, Action<string>>();
		#endregion
	}
}
