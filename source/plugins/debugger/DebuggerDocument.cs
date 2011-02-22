// Copyright (C) 2009 Jesse Jones
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
using MObjc;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Debugger
{
	[ExportClass("DebuggerDocument", "NSDocument")]
	internal sealed class DebuggerDocument : NSDocument, IObserver
	{
		private DebuggerDocument(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
			
			if (ms_windows == null)
				ms_windows = new DebuggerWindows();
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Register(this, 61, () => m_debugger.Run(), this.DoIsPaused);
			handler.Register(this, 62, () => m_debugger.StepOver(), this.DoIsPaused);
			handler.Register(this, 63, () => m_debugger.StepIn(), this.DoIsPaused);
			handler.Register(this, 64, () => m_debugger.StepOut(), this.DoIsPaused);
			handler.Register(this, 68, () => m_debugger.Suspend(), this.DoIsNotPaused);
			
			Broadcaster.Register("debugger loaded app domain", this);
			Broadcaster.Register("debugger unloaded app domain", this);
			Broadcaster.Register("debugger loaded assembly", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "debugger loaded app domain":
					var domain = (AppDomainMirror) value;
					m_domains.Add(domain);
					break;
				
				case "debugger unloaded app domain":
					var domain2 = (AppDomainMirror) value;
					m_domains.Remove(domain2);
					break;
				
				case "debugger loaded assembly":
					var assembly = (AssemblyMirror) value;
					m_assemblies.Add(assembly);
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		// Used when opening mdb files.
		public new void makeWindowControllers()
		{
			try
			{
				if (Debugger.IsRunning)
					throw new Exception("The debugger is already running.");
				
				makeWindowControllersNoUI(null, NSString.Empty, NSString.Empty, NSString.Empty, true);
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Couldn't start the debugger.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
				
				close();									// note that exceptions leaving here are ignored by cocoa
			}
		}
		
		// Used when opening mdb files or exe files (via AppleScript).
		public void makeWindowControllersNoUI(NSString uses, NSString args, NSString env, NSString workingDir, bool breakInMain)
		{
			var info = new ProcessStartInfo();
			
			m_exeDir = DoGetExeDir();
			m_exe = DoGetExePath();
			
			if (DoIsCocoa())
				DoSetCocoaOptions(info, uses, args, env, workingDir);
			else
				DoSetCliOptions(info, uses, args, env, workingDir);
			
			// The soft debugger returns a VM in EndInvoke even when you pass
			// it a completely bogus path so we need to do this check. 
			if (!File.Exists(m_exe))
				throw new FileNotFoundException(m_exe + " was not found.");
			
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "Launching debugger with:");
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "file: {0}", info.FileName);
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "working dir: {0}", info.WorkingDirectory);
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "args: {0}", info.Arguments);
			Log.WriteLine(TraceLevel.Verbose, "Debugger", "vars: {0}", DoStringDictToStr(info.EnvironmentVariables));
			
			m_debugger = new Debugger(info, breakInMain);
			
			NSWindow window = DoCreateCodeWindow();
			addWindowController(window.windowController());
		}
		
		public new void close()
		{
			if (m_debugger != null)
			{
				m_debugger.Shutdown();
				m_debugger = null;
			}
			
			SuperCall(NSDocument.Class, "close");
		}
		
		public void getInfo()
		{
			DoShowInfo();
		}
		
		public Debugger Debugger
		{
			get {return m_debugger;}
		}
		
		public bool readFromFileWrapper_ofType_error(NSFileWrapper data, NSString typeName, IntPtr outError)
		{
			// fileURL() isn't set in these methods so there isn't much we can do.
			return true;
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			return true;
		}
		
		#region Protected Methods
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnDealloc()
		{
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Deregister(this);
			
			Broadcaster.Unregister(this);
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private string DoGetCocoaExe()
		{
			string exe = null;
			
			string path = fileURL().path().ToString();
			if (path.EndsWith(".app"))
			{
				string name = Path.GetFileNameWithoutExtension(path);
				
				string candidate = Path.Combine(path, name);
				if (File.Exists(candidate))
				{
					exe = candidate;			// monomac app built with MonoDevelop
				}
				else
				{
					candidate = Path.Combine(path, "Contents");
					candidate = Path.Combine(candidate, "MacOS");
					candidate = Path.Combine(candidate, name);
					if (File.Exists(candidate))
						exe = candidate;		// monomac app built with Continnum (or an mcocoa app)
				}
			}
			
			return exe;
		}
		
		private string DoGetExeDir()
		{
			string path = fileURL().path().ToString();
			if (path.EndsWith(".app"))
			{
				path = Path.Combine(path, "Contents");
				path = Path.Combine(path, "Resources");
			}
			else
			{
				path = Path.GetDirectoryName(path);	// if we didn't open the app bundle assume we are opening the exe or mdb
			}
			
			return path;
		}
		
		private string DoGetExePath()
		{
			string exe = null;
			
			string path = fileURL().path().ToString();
			if (path.EndsWith(".app"))
			{
				string candidate = Path.GetFileName(path);
				candidate = Path.ChangeExtension(candidate, ".exe");
				candidate = Path.Combine(m_exeDir, candidate);
				if (File.Exists(candidate))
				{
					exe = candidate;
				}
				else
				{
					string[] candidates = Directory.GetFiles(m_exeDir, "*.exe");
					if (candidates.Length == 1)
						exe = candidates[0];
					else
						Log.WriteLine(TraceLevel.Error, "Debugger", "Found {0} exe's in {1}", candidates.Length, m_exeDir);
				}
			}
			else if (path.EndsWith(".exe"))
			{
				exe = path;
			}
			else if (path.EndsWith(".mdb"))
			{
				exe = Path.ChangeExtension(path, ".exe");
			}
			
			return exe;
		}
		
		private bool DoIsCocoa()
		{
			string dll = Path.Combine(m_exeDir, "MonoMac.dll");
			if (File.Exists(dll))
				return true;
			
			dll = Path.Combine(m_exeDir, "mcocoa.dll");
			if (File.Exists(dll))
				return true;
			
			return false;
		}
		
		private void DoSetCommonOptions(ProcessStartInfo info, NSString uses, NSString args, NSString env, NSString workingDir)
		{
			if (!NSObject.IsNullOrNil(args))
				info.Arguments = string.Format("--debug {0} {1:D}", m_exe, args);	// note that the exe should not be the first arg (NSApplication can get confused if so)
			else
				info.Arguments = string.Format("--debug {0}", m_exe);
			
			info.FileName = NSObject.IsNullOrNil(uses) ? "mono" : uses.ToString();
			info.RedirectStandardInput = false;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.UseShellExecute = false;
			if (!NSObject.IsNullOrNil(workingDir))
				info.WorkingDirectory = workingDir.ToString();
			else
				info.WorkingDirectory = m_exeDir;	// this case is used only when debugging via AppleScript with no using parameter
			
			if (!NSObject.IsNullOrNil(env))
			{
				// When running an app via a shell the app will get all the environment variables exported
				// from that shell (e.g. those set in .bash_profile). When running via the Finder only a few
				// variables are exported (and none from .bash_profile). 
				//
				// When running an app via Continuum (with or without the debugger) the app will default
				// to the variables Continnuum was started up with, which depends upon whether Continuum
				// was started via the Finder.
				foreach (string entry in env.ToString().Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries))
				{
					int i = entry.IndexOf('=');
					if (i < 0)
						throw new Exception("Expected an '=' in " + entry);
					
					// Environment variables set by the user override existing variables.
					info.EnvironmentVariables[entry.Substring(0, i)] = entry.Substring(i + 1);
				}
			}
		}
		
		private void DoSetCliOptions(ProcessStartInfo info, NSString uses, NSString args, NSString env, NSString workingDir)
		{
			DoSetCommonOptions(info, uses, args, env, workingDir);
		}
		
		private void DoSetCocoaOptions(ProcessStartInfo info, NSString uses, NSString args, NSString env, NSString workingDir)
		{
			DoSetCommonOptions(info, uses, args, env, workingDir);
			
			if (info.FileName == "mono")
				info.FileName = DoGetCocoaExe() ?? info.FileName;
			
			DoPrependEnvVar(info, "DYLD_FALLBACK_LIBRARY_PATH", "/Library/Frameworks/Mono.framework/Versions/Current/lib", ":");
			DoPrependEnvVar(info, "DYLD_FALLBACK_LIBRARY_PATH", m_exeDir, ":");
			
			DoPrependEnvVar(info, "PATH", "/Library/Frameworks/Mono.framework/Versions/Current/bin", ":");
			DoPrependEnvVar(info, "PATH", m_exeDir, ":");
			
//			DoPrependEnvVar(info, "MONO_ENV_OPTIONS", "--debug " + m_exe, " ");		// debugger hanged on startup when using this
		}
		
		private void DoPrependEnvVar(ProcessStartInfo info, string key, string value, string sep)
		{
			if (info.EnvironmentVariables.ContainsKey(key))
				info.EnvironmentVariables[key] = value + sep + info.EnvironmentVariables[key];
			else
				info.EnvironmentVariables.Add(key, value);
		}
		
		private NSWindow DoCreateCodeWindow()
		{
			Boss pluginBoss = ObjectModel.Create("TextWindow");
			var create = pluginBoss.Get<ICreate>();
			Boss boss = create.Create("CodeViewer");
			
			var viewer = boss.Get<ICodeViewer>();
			viewer.Init(this);
			
			var editor = boss.Get<ITextEditor>();
			editor.Editable = false;
			
			var window = boss.Get<IWindow>();
			return window.Window;
		}
		
		private bool DoIsPaused()
		{
			return m_debugger != null && m_debugger.IsPaused;
		}
		
		private bool DoIsNotPaused()
		{
			return m_debugger != null && !m_debugger.IsPaused;
		}
		
		private void DoShowInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (AssemblyMirror assembly in m_assemblies)
			{
				DoShowInfo(assembly, builder);
				builder.AppendLine();
			}
			
			foreach (AppDomainMirror domain in m_domains)
			{
				DoShowInfo(domain, builder);
			}
			
			DoShowInfo(builder.ToString(), "Debugger");
		}
		
		private void DoShowInfo(AssemblyMirror assembly, System.Text.StringBuilder builder)
		{
			CodeViewer.CacheAssembly(assembly);
			
			if (assembly.Metadata != null)
			{
				builder.AppendLine(string.Format("Assembly: {0}", assembly.Metadata.Name.Name));
				builder.AppendLine(string.Format("Culture: {0}", !string.IsNullOrEmpty(assembly.Metadata.Name.Culture) ? assembly.Metadata.Name.Culture : "neutral"));
				builder.AppendLine(string.Format("Version: {0}", assembly.Metadata.Name.Version));
				builder.AppendLine(string.Format("Kind: {0}", assembly.Metadata.Kind));
				builder.AppendLine(string.Format("Runtime: {0}", assembly.Metadata.Runtime));
				builder.AppendLine(string.Format("Flags: {0}", assembly.Metadata.Name.Flags));
				builder.AppendLine(string.Format("HashAlgorithm: {0}", assembly.Metadata.Name.HashAlgorithm));
				builder.AppendLine(string.Format("Hash: {0}", assembly.Metadata.Name.Hash != null && assembly.Metadata.Name.Hash.Length > 0 ? BitConverter.ToString(assembly.Metadata.Name.Hash) : "none"));
				builder.AppendLine(string.Format("PublicKey: {0}", assembly.Metadata.Name.PublicKey != null && assembly.Metadata.Name.PublicKey.Length > 0 ? BitConverter.ToString(assembly.Metadata.Name.PublicKey) : "none"));
				builder.AppendLine(string.Format("PublicKeyToken: {0}", assembly.Metadata.Name.PublicKeyToken != null ? BitConverter.ToString(assembly.Metadata.Name.PublicKeyToken) : "none"));
			}
			
			builder.AppendLine(string.Format("Location: {0}", assembly.Location));
		}
		
		private void DoShowInfo(AppDomainMirror domain, System.Text.StringBuilder builder)
		{
			builder.AppendLine(string.Format("Domain: {0}", domain.FriendlyName));
		}
		
		private void DoShowInfo(string text, string label)	// TODO: this is a copy of the method in AssemblyController
		{
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(label.Replace(".", string.Empty), ".info");
			
			try
			{
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", text);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			catch (Exception e)	// can sometimes land here if too many files are open (max is system wide and only 256)
			{
				NSString title = NSString.Create("Couldn't process '{0}'.", file);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private string DoStringDictToStr(System.Collections.Specialized.StringDictionary dict)
		{
			var builder = new System.Text.StringBuilder();
			foreach (string key in dict.Keys)
			{
				builder.Append(key);
				string value = dict[key];
				if (!string.IsNullOrEmpty(value))
				{
					builder.Append('=');
					builder.Append(value);
				}
				builder.Append(' ');
			}
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields
		private string m_exeDir;
		private string m_exe;
		private Debugger m_debugger;
		
		private HashSet<AssemblyMirror> m_assemblies = new HashSet<AssemblyMirror>();
		private HashSet<AppDomainMirror> m_domains = new HashSet<AppDomainMirror>();
		
		private static DebuggerWindows ms_windows;
		#endregion
	}
}
