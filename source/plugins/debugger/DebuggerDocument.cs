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
				
				m_breakInMain = true;
				makeWindowControllersNoUI(NSString.Empty);
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
		public void makeWindowControllersNoUI(NSString args)
		{
			string path = fileURL().path().ToString();
			string dir = Path.GetDirectoryName(path);
			
			if (path.EndsWith(".mdb"))
				m_executable = Path.Combine(dir, Path.GetFileNameWithoutExtension(path));
			else
				m_executable = Path.Combine(dir, path);
			
			// The Soft debugger returns a VM in EndInvoke even when you pass
			// it a completely bogus path so we need to do this check. 
			if (!File.Exists(m_executable))
				throw new FileNotFoundException(m_executable + " was not found.");
			
			ProcessStartInfo info = new ProcessStartInfo();
			if (!NSObject.IsNullOrNil(args))
				info.Arguments = string.Format("--debug {0} {1:D}", m_executable, args);
			else
				info.Arguments = string.Format("--debug {0}", m_executable);
//			info.EnvironmentVariables.Add("key", "value");		// TODO: might want to support this
			info.FileName = "mono";
			info.RedirectStandardInput = false;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.UseShellExecute = false;
			info.WorkingDirectory = dir;
			
			m_debugger = new Debugger(info);
			
			NSWindow window = DoCreateCodeWindow();
			addWindowController(window.windowController());
		}
		
		public new void close()
		{
			if (m_debugger != null)
			{
				m_debugger.Dispose();
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
		
		// Full path to the executable.
		public string Executable
		{
			get {return m_executable;}
		}
		
		public bool BreakInMain
		{
			get {return m_breakInMain;}
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			bool read = false;
			
			try
			{
				// Don't think fileURL is available here so its hard to do anything too useful.
				read = true;
			}
			catch (Exception e)
			{
				NSMutableDictionary userInfo = NSMutableDictionary.Create();
				userInfo.setObject_forKey(NSString.Create("Couldn't start up the debugger."), Externs.NSLocalizedDescriptionKey);
				userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
				
				NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 1, userInfo);
				Marshal.WriteIntPtr(outError, error);
			}
			
			return read;
		}
		
		#region Private Methods
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
		#endregion
		
		#region Fields
		private string m_executable;
		private Debugger m_debugger;
		private bool m_breakInMain;
		
		private HashSet<AssemblyMirror> m_assemblies = new HashSet<AssemblyMirror>();
		private HashSet<AppDomainMirror> m_domains = new HashSet<AppDomainMirror>();
		
		private static DebuggerWindows ms_windows;
		#endregion
	}
}
