// Copyright (C) 2008-2009 Jesse Jones
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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DirectoryEditor
{
	[Serializable]
	public enum State {Opened, Building, Built, Canceled}
	
	internal sealed class GenericBuilder
	{
		public GenericBuilder(string path)
		{	
			m_path = path;
			m_builder = DoFindBuilder(path);
			
			if (m_builder != null)
				if (m_builder.DefaultTarget != null)
					m_target = m_builder.DefaultTarget;
				else if (m_builder.Targets.Length > 0)
					m_target = m_builder.Targets[0];
			
			Boss boss = ObjectModel.Create("Application");
			m_results = boss.Get<IBuildStatus>();
			
			ActiveObjects.Add(this);
		}
		
		public bool CanBuild
		{
			get {return m_builder != null;}
		}
		
		// May be null if there are no targets.
		public string Target
		{
			get {return m_target;}
			set {m_target = value;}
		}
		
		public string[] Targets
		{
			get {return m_builder != null ? m_builder.Targets : new string[0];}
		}
		
		public State State
		{
			get {return m_state;}
		}
		
		public void Build()
		{
			DoSaveChanges();
			
			Boss boss = ObjectModel.Create("Application");
			var errorHandler = boss.Get<IHandleBuildError>();
			errorHandler.Close();
			
			m_state = State.Building;
			m_errors = new StringBuilder();
			
			try
			{
				if (m_process != null)		// this should not normally happen, but may if an exception is thrown (or Exited isn't called for some reason)
				{
					Log.WriteLine(TraceLevel.Warning, "Errors", "m_process was not null");
					
					AssemblyCache.ReleaseLock();
					m_process.Dispose();
					m_process = null;
				}
				
				m_process = m_builder.Build(m_target);
				AssemblyCache.AcquireLock();
				m_process.EnableRaisingEvents = true;
				m_process.Exited += this.DoProcessExited;
				m_process.StartInfo.RedirectStandardOutput = true;
				m_process.StartInfo.RedirectStandardError = true;
				m_process.OutputDataReceived += this.DoGotStdoutData;
				m_process.ErrorDataReceived += this.DoGotStderrData;
				
				m_results.OnStarted();
				m_results.WriteCommand(m_builder.Command);
				
				m_startTime = DateTime.Now;
				m_process.Start();
				
				m_process.BeginOutputReadLine();
				m_process.BeginErrorReadLine();
			}
			catch (Exception e)		// started getting "Error creating standard error pipe" with mono 2.6.1 (which is usually wrapped within a TargetInvocationException)
			{
				m_process.Dispose();
				AssemblyCache.ReleaseLock();
				m_process = null;
				m_state = State.Canceled;
				
				Log.WriteLine(TraceLevel.Info, "Errors", "Failed to build:");
				Log.WriteLine(TraceLevel.Info, "Errors", e.ToString());
				
				NSString title = NSString.Create("Couldn't build {0}.", m_target);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		public void Cancel()
		{
			m_state = State.Canceled;	
			
			try
			{
				m_process.Kill();
			}
			catch (InvalidOperationException e)
			{
				// This will be thrown if the process has already exited. This may happen
				// if we lose a race or if we get confused because of recursive make weirdness.
				Log.WriteLine(TraceLevel.Info, "Errors", "error killing the build process:");
				Log.WriteLine(TraceLevel.Info, "Errors", e.Message);
			}
		}
		
		public void SetBuildFlags()
		{
			m_builder.SetBuildFlags();
		}
		
		public void SetBuildVariables()
		{
			m_builder.SetBuildVariables();
		}
		
		#region Private Methods
		private void DoSaveChanges()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				editor.Save();
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoGotStdoutData(object sender, DataReceivedEventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoNonThreadedGotStdoutData(sender, e));
				return;
			}
			
			DoNonThreadedGotStdoutData(sender, e);
		}
		
		[ThreadModel(ThreadModel.MainThread | ThreadModel.AllowEveryCaller)]
		private void DoNonThreadedGotStdoutData(object sender, DataReceivedEventArgs e)
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			if (e.Data != null)
			{
				m_results.WriteOutput(e.Data);
				m_results.WriteOutput(Environment.NewLine);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoGotStderrData(object sender, DataReceivedEventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoNonThreadedGotStderrData(sender, e));
				return;
			}
			
			DoNonThreadedGotStderrData(sender, e);
		}
		
		[ThreadModel(ThreadModel.MainThread | ThreadModel.AllowEveryCaller)]
		private void DoNonThreadedGotStderrData(object sender, DataReceivedEventArgs e)
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			if (e.Data != null)
			{
				m_results.WriteError(e.Data);
				m_results.WriteError(Environment.NewLine);
				
				m_errors.AppendLine(e.Data);
			}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoProcessExited(object sender, EventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoNonThreadedProcessExited(sender, e));
				return;
			}
			
			DoNonThreadedProcessExited(sender, e);
		}
		
		[ThreadModel(ThreadModel.MainThread | ThreadModel.AllowEveryCaller)]
		private void DoNonThreadedProcessExited(object sender, EventArgs e)
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			m_results.OnStopped();
			
			string errors = m_errors.ToString();
			if (m_process.ExitCode == 0 && errors.Length == 0)	// poorly written makefiles may return OK even if there were errors...
			{
				TimeSpan elapsed = DateTime.Now - m_startTime;
				m_results.WriteOutput(string.Format("built in {0:0.0} secs", elapsed.TotalSeconds));
				m_results.WriteOutput(Environment.NewLine);
				m_results.WriteOutput(Environment.NewLine);
			}
			else
			{
				m_results.WriteError("exited with code " + m_process.ExitCode);
				m_results.WriteError(Environment.NewLine);
				m_results.WriteError(Environment.NewLine);
			}
			
			if (m_process.ExitCode == 0)
				Broadcaster.Invoke("built target", m_target);
			
			DoHandleErrors(errors);
			
			m_process.Dispose();
			AssemblyCache.ReleaseLock();			// note that we need to be very careful about where we call this to ensure that we don't wind up calling it twice
			m_process = null;
			
			if (m_state != State.Canceled)
				m_state = State.Built;
		}
		
		private void DoHandleErrors(string text)
		{
			var errors = new List<BuildError>();
			
			Boss boss = ObjectModel.Create("Application");
			foreach (IParseErrors parser in boss.GetRepeated<IParseErrors>())
			{
				parser.Parse(text, errors);
			}
			
			var handler = boss.Get<IHandleBuildError>();
			if (errors.Count == 0)
				handler.Reset();
			else
				handler.Set(m_path, errors.ToArray());
		}
		
		private IBuilder DoFindBuilder(string dir)
		{
			Dictionary<string, IBuilder> builders = new Dictionary<string, IBuilder>();
			Boss boss = ObjectModel.Create("Builders");
			
			foreach (string path in System.IO.Directory.GetFiles(dir))
			{
				foreach (ICanBuild can in boss.GetRepeated<ICanBuild>())
				{
					IBuilder b = can.GetBuilder(path);
					if (b != null)
						builders.Add(path, b);
				}
			}
			
			IBuilder builder = null;
			string path2 = DoChooseBuilder(builders);
			if (path2 != null)
			{
				builder = builders[path2];
				builder.Init(path2);
			}
			
			return builder;
		}
		
		private string DoChooseBuilder(Dictionary<string, IBuilder> builders)
		{
			string path = null;
			
			if (builders.Count > 1)
			{
				string[] paths = builders.Keys.ToArray();
				Array.Sort(paths);
				
				var getter = new GetItem<string>{Title = "Choose Build File", Items = paths};
				string[] result = getter.Run(i => System.IO.Path.GetFileName(i));
				if (result.Length > 0)
					path = result[0];
			}
			else if (builders.Count == 1)
			{
				path = builders.Keys.First();
			}
			
			return path;
		}
		#endregion
		
		#region Fields
		private string m_path;
		private IBuilder m_builder;
		private string m_target;
		private State m_state;
		private Process m_process;
		private IBuildStatus m_results;
		private StringBuilder m_errors;
		private DateTime m_startTime;
		#endregion
	}
}	