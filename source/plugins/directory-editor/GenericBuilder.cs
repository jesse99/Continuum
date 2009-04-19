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
			
			// There seems to be a race here where make doesn't always recognize
			// that the file we just saved changed. So, we'll sleep a little bit to try
			// to avoid the race.
			System.Threading.Thread.Sleep(200);
		}
		
		private void DoGotStdoutData(object sender, DataReceivedEventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoGotStdoutData(sender, e));
				return;
			}
			
			m_results.WriteOutput(e.Data);
			m_results.WriteOutput(Environment.NewLine);
		}
		
		private void DoGotStderrData(object sender, DataReceivedEventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoGotStderrData(sender, e));
				return;
			}
				
			m_results.WriteError(e.Data);
			m_results.WriteError(Environment.NewLine);
			
			m_errors.AppendLine(e.Data);
		}
		
		private void DoProcessExited(object sender, EventArgs e)	// threaded
		{
			if (NSApplication.sharedApplication().InvokeRequired)
			{
				NSApplication.sharedApplication().BeginInvoke(() => DoProcessExited(sender, e));
				return;
			}
			
			m_results.OnStopped();
			
			string errors = m_errors.ToString();
			if (m_process.ExitCode == 0 && errors.Length == 0)	// poorly written makefiles may return OK even if there were errors...
			{
				TimeSpan elapsed = DateTime.Now - m_startTime;
				m_results.WriteOutput(string.Format("built in {0:0.0} secs", elapsed.TotalSeconds));
				m_results.WriteOutput(Environment.NewLine);
				m_results.WriteOutput(Environment.NewLine);
				
				Broadcaster.Invoke("built target", m_target);
			}
			else
			{
				m_results.WriteError("exited with code " + m_process.ExitCode);
				m_results.WriteError(Environment.NewLine);
				m_results.WriteError(Environment.NewLine);
			}
			
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
			boss.CallRepeated<IParseErrors>(parser => parser.Parse(text, errors));
			
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
				boss.CallRepeated<ICanBuild>(can =>
				{
					IBuilder b = can.GetBuilder(path);
					if (b != null)
						builders.Add(path, b);
				});
			}
			
			if (builders.Count > 1)	// TODO: probably better to popup an alert and return null
				throw new InvalidOperationException("Multiple build files: " + string.Join(", ", builders.Keys.ToList().ToArray()));
			
			IBuilder builder = builders.Count > 0 ? builders.Values.First() : null;
			if (builder != null)
				builder.Init(builders.Keys.First());
			return builder;
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