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
using System.Collections.Generic;
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
		
		public bool IsShowingSource()
		{
			return m_lines.Count == 0;
		}
		
		public bool CanDisplaySource()
		{
			bool can = false;
			
			if (m_context != null)
			{
				can = System.IO.File.Exists(m_context.SourceFile);
			}
			
			return can;
		}
		
		public bool CanDisplayIL()
		{
			bool can = false;
			
			if (m_context != null)
			{
				AssemblyMirror assembly = m_context.Method.DeclaringType.Assembly;
				can = assembly.Metadata != null || System.IO.File.Exists(assembly.Location);
			}
			
			return can;
		}
		
		public void ShowSource()
		{
			m_showIL = false;
			DoShowSource();
		}
		
		public void ShowIL()
		{
			m_showIL = true;
			DoCacheAssembly();
			DoShowIL(m_context);
		}
		
		public string Path
		{
			get {return m_path;}
		}
		
		public string GetTitle(string displayName)
		{
			return "[" + m_title + "]";
		}
		
		public string GetExtension()
		{
			if (m_lines.Count > 0)
				return ".cil";
			else if (m_title.Contains("."))
				return System.IO.Path.GetExtension(m_title);
			else
				return ".something-silly";
		}
		
		#region Private Methods
		private void OnStateChanged(State state)
		{
			if (state == State.Running)
			{
				m_context = null;
				DoSetInstructionPointer(-1);
			}
			else if (state != State.Paused)
			{
				DoSetTitle("debug");
				
				var text = Boss.Get<IText>();
				text.Replace(state.ToString());
			}
		}
		
		private void OnPaused(Context context)
		{
			m_context = context;
			
			if (context.SourceFile != null && System.IO.File.Exists(context.SourceFile) && (!CanDisplayIL() || !m_showIL))
			{
				DoShowSource();
			}
			else
			{
				DoCacheAssembly();
				
				AssemblyMirror assembly = m_context.Method.DeclaringType.Assembly;
				if (assembly.Metadata != null && context.Method.Metadata != null)
					DoShowIL(context);
				else
					DoShowNothing(context);
			}
		}
		
		private void DoShowSource()
		{
			m_path = System.IO.Path.GetFileName(m_context.SourceFile);
			DoSetTitle(m_path);
			
			if (m_currentView != m_context.SourceFile)
			{
				var overlay = m_boss.Get<ITextOverlay>();
				if (DoSourceIsOutOfDate())
				{
					overlay.Text = "Source is newer than the assembly.";
					overlay.Color = NSColor.colorWithDeviceRed_green_blue_alpha(1.0f, 0.0f, 0.0f, 0.4f);
				}
				else
				{
					overlay.Text = null;
				}
				
				var text = m_boss.Get<IText>();
				text.Replace(System.IO.File.ReadAllText(m_context.SourceFile));
				m_currentView = m_context.SourceFile;
				m_lines.Clear();
				m_debugger.StepBy = StepSize.Line;
			}
			
			var editor = m_boss.Get<ITextEditor>();
			editor.ShowLine(m_context.Location.LineNumber, -1, 8);
			
			DoSetInstructionPointer(m_context.Location.LineNumber);
		}
		
		private void DoShowIL(Context context)
		{
			string name = context.Method.FullName;
			DoSetTitle(name);
			m_path = null;
			
			if (m_currentView != name)
			{
				var overlay = m_boss.Get<ITextOverlay>();
				overlay.Text = null;
				
				Boss boss = ObjectModel.Create("Disassembler");
				var disassembler = boss.Get<IDisassembler>();
				
				var text = m_boss.Get<IText>();
				m_currentView = name;
				string source = disassembler.Disassemble(context.Method.Metadata, m_context.Method.DeclaringType.Assembly.Location);
				DoBuildLineTable(source);
				
				text.Replace(source);
				m_debugger.StepBy = StepSize.Min;
			}
			
			int line;
			if (m_lines.TryGetValue(context.Offset, out line))
			{
				var editor = m_boss.Get<ITextEditor>();
				editor.ShowLine(line, -1, 8);
			
				DoSetInstructionPointer(line);
			}
			else
			{
				DoSetInstructionPointer(-1);
			}
		}
		private void DoShowNothing(Context context)
		{
			string name = context.Method.FullName;
			DoSetTitle(name);
			m_path = null;
			
			if (m_currentView != name)
			{
				var overlay = m_boss.Get<ITextOverlay>();
				overlay.Text = null;
				
				m_currentView = name;
				m_lines.Clear();
				
				var text = m_boss.Get<IText>();
				text.Replace("...");
				DoSetInstructionPointer(-1);
			}
		}
		
		private bool DoSourceIsOutOfDate()
		{
			DateTime stime = System.IO.File.GetLastWriteTime(m_context.SourceFile);
			DateTime atime = System.IO.File.GetLastWriteTime(m_context.Method.DeclaringType.Assembly.Location);
			return stime > atime;
		}
		
		private void DoCacheAssembly()
		{
			AssemblyMirror assembly = m_context.Method.DeclaringType.Assembly;
			if (assembly.Metadata == null)
			{
				AssemblyCache.AcquireLock();
				try
				{
					assembly.Metadata = AssemblyCache.Load(assembly.Location, true);
				}
				finally
				{
					AssemblyCache.ReleaseLock();
				}
			}
		}
		
		// The disassembly may include stuff like attributes in the header as well
		// as comments in the code so we'll search for the offset instead of relying
		// on something like MethodMirror.ILOffsets.
		private void DoBuildLineTable(string source)
		{
			m_lines.Clear();
			
			int i = 0;
			int line = 1;
			while (i < source.Length)
			{
				long offset = DoMatchOffset(source, i);
				if (offset >= 0)
				{
					m_lines[offset] = line;		// note that we can get duplicate offsets for things like try blocks
				}
				
				i = source.IndexOf('\n', i) + 1;
				++line;
			}
		}
		
		private long DoMatchOffset(string source, int i)
		{
			if (i + 5 < source.Length && source[i + 4] == ' ')
			{
				long offset;
				if (long.TryParse(source.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out offset))
					return offset;
			}
			
			return -1;
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
		
		private void DoSetInstructionPointer(int line)
		{
			if (m_ipAnnotation != null)
			{
				m_ipAnnotation.Close();
				m_ipAnnotation = null;
			}
			
			if (line > 1)
			{
				var metrics = m_boss.Get<ITextMetrics>();
				var range = new NSRange(metrics.GetLineOffset(line - 1), 1);
				
				var editor = m_boss.Get<ITextEditor>();
				m_ipAnnotation = editor.GetAnnotation(range);
				
				m_ipAnnotation.BackColor = NSColor.greenColor();
				m_ipAnnotation.Text = ">";
				m_ipAnnotation.Draggable = false;
				m_ipAnnotation.Visible = true;
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private bool m_showIL;
		private Debugger m_debugger;
		private string m_title = "[debug]";
		private string m_currentView;
		private Context m_context;
		private string m_path;
		private Dictionary<long, int> m_lines = new Dictionary<long, int>();	// il offset => line number
		
		private ITextAnnotation m_ipAnnotation;
		#endregion
	}
}
