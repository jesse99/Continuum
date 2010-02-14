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
//using Gear.Helpers;
using MCocoa;
//using MObjc;
using MObjc.Helpers;
//using Mono.Cecil;
//using Mono.Cecil.Binary;
using Mono.Debugger;
using Shared;
using System;
//using System.Diagnostics;
//using System.Linq;
//using System.Threading;
//using System.Collections.Generic;
//using System.IO;

namespace Debugger
{
	// Customizes the normal text editor boss behavior.
	internal sealed class CodeViewer : ICodeViewer, IDocumentWindowTitle, IDocumentExtension
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(Debugger debugger)
		{
			Contract.Requires(debugger != null, "debugger is null");
			
			m_debugger = debugger;
			
			m_debugger.StateEvent += this.OnStateChanged;
			m_debugger.BreakpointEvent += this.OnPaused;
			m_debugger.SteppedEvent += this.OnPaused;
		}
		
		public string GetTitle(string displayName)
		{
			return "[" + m_title + "]";
		}
		
		public string GetExtension()
		{
			if (m_title.Contains("."))
				return System.IO.Path.GetExtension(m_title);
			else
				return ".something-silly";
		}
		
		#region Private Methods
		private void OnStateChanged(State state)
		{
			if (state != State.Paused && state != State.Running)
			{
				DoSetTitle("debug");
				
				var text = Boss.Get<IText>();
				text.Replace(state.ToString());
			}
		}
		
		private void OnPaused(Location location)
		{
			var text = Boss.Get<IText>();
			if (System.IO.File.Exists(location.SourceFile))
			{
				string file = System.IO.Path.GetFileName(location.SourceFile);
				DoSetTitle(file);
				
				if (m_currentFile != location.SourceFile)
				{
					text.Replace(System.IO.File.ReadAllText(location.SourceFile));
					m_currentFile = location.SourceFile;
				}
				
				var editor = Boss.Get<ITextEditor>();
				editor.ShowLine(location.LineNumber, -1, 8);
			}
			else
			{
				string file = System.IO.Path.GetFileNameWithoutExtension(location.SourceFile);
				DoSetTitle(file);
				m_currentFile = null;
				
				text.Replace(string.Format("Couldn't find '{0}'.", location.SourceFile));
			}
//			string file = System.IO.Path.GetFileName(location.SourceFile);
//			DoSetTitle(file);
		}
		
		private void DoSetTitle(string title)
		{
			if (title != m_title)
			{
				m_title = title;
				
				var window = m_boss.Get<IWindow>();
				window.Window.windowController().synchronizeWindowTitleWithDocumentName();
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private Debugger m_debugger;
		private string m_title = "[debug]";
		private string m_currentFile;
		#endregion
	}
}
