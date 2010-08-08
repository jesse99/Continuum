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
using MCocoa;
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;

namespace Debugger
{
	// Customizes the normal text editor boss behavior.
	internal sealed class CodeViewer : ICodeViewer, IDocumentWindowTitle, IDocumentExtension, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			Broadcaster.Register("closing document window", this);
			Broadcaster.Register("debugger loaded assembly", this);
			Broadcaster.Register("debugger processed breakpoint event", this);
			Broadcaster.Register("debugger thrown exception", this);
			Broadcaster.Register("debugger processed step event", this);
			Broadcaster.Register("debugger stopped", this);
			Broadcaster.Register("debugger state changed", this);
			Broadcaster.Register("changed stack frame", this);
			Broadcaster.Register("changed thread", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(DebuggerDocument doc)
		{
			Contract.Requires(doc != null, "doc is null");
			
			m_document = doc;
			
			var window = m_boss.Get<IWindow>();
			window.Window.setFrameAutosaveName(NSString.Create("debugger code viewer"));
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "closing document window":
					if (m_boss == value)
					{
						var window = m_boss.Get<IWindow>();
						window.Window.saveFrameUsingName(NSString.Create("debugger code viewer"));
						
						m_document = null;
						Broadcaster.Unregister(this);
//						NSApplication.sharedApplication().BeginInvoke(() => m_boss.Free());
					}
					break;
					
				case "debugger stopped":
					var window = m_boss.Get<IWindow>();
					Action action = () => window.Window.close();
					NSApplication.sharedApplication().BeginInvoke(action, TimeSpan.FromMilliseconds(100));
					break;
				
				case "debugger loaded assembly":
					var assembly = (AssemblyMirror) value;
					DoAssemblyLoaded(assembly);
					break;
				
				case "debugger processed breakpoint event":
				case "debugger thrown exception":
					var context = (Context) value;
					DoBreakpoint(context);
					break;
				
				case "debugger processed step event":
					var context2 = (Context) value;
					DoPaused(context2);
					break;
				
				case "changed stack frame":
					var frame = (LiveStackFrame) value;
					var context3 = new Context(frame.Thread, frame.Method, frame.ILOffset);
					DoPaused(context3);
					break;
				
				case "changed thread":
					var stack = (LiveStack) value;
					var context4 = new Context(stack[0].Thread, stack[0].Method, stack[0].ILOffset);
					DoPaused(context4);
					break;
				
				case "debugger state changed":
					var state = (State) value;
					DoStateChanged(state);
					break;
				
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
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
		
		public void OpenSource()
		{
			Boss boss = ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(m_context.SourceFile, m_context.Location.LineNumber, -1, 1);
		}
		
		public void ShowIL()
		{
			m_showIL = true;
			CacheAssembly(m_context.Method.DeclaringType.Assembly);
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
		
		public static void CacheAssembly(AssemblyMirror assembly)
		{
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
		
		#region Private Methods
		private void DoStateChanged(State state)
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
		
		private void DoBreakpoint(Context context)
		{
			var window = m_boss.Get<IWindow>();
			window.Window.makeKeyAndOrderFront(window.Window);
			
			DoPaused(context);
		}
		
		private void DoPaused(Context context)
		{
			m_context = context;
			
			if (context.SourceFile != null && System.IO.File.Exists(context.SourceFile) && (!CanDisplayIL() || !m_showIL))
			{
				DoShowSource();
			}
			else
			{
				CacheAssembly(m_context.Method.DeclaringType.Assembly);
				
				AssemblyMirror assembly = m_context.Method.DeclaringType.Assembly;
				if (assembly.Metadata != null && context.Method.Metadata != null)
					DoShowIL(context);
				else
					DoShowNothing(context);
			}
		}
		
		private void DoShowSource()
		{
			if (m_currentView != m_context.SourceFile)
			{
				Broadcaster.Invoke("swapping code view", m_boss);
				m_path = m_context.SourceFile;
				
				string name = System.IO.Path.GetFileName(m_context.SourceFile);
				DoSetTitle(name);
				
				var overlay = m_boss.Get<ITextOverlay>();
				if (DoSourceIsOutOfDate())
				{
					overlay.Text = "Source is newer than the assembly.";
					overlay.Color = NSColor.colorWithDeviceRed_green_blue_alpha(1.0f, 0.0f, 0.0f, 0.2f);
				}
				else
				{
					overlay.Text = null;
				}
				
				var text = m_boss.Get<IText>();
				text.Replace(System.IO.File.ReadAllText(m_context.SourceFile));
				m_currentView = m_context.SourceFile;
				m_lines.Clear();
				m_document.Debugger.StepBy = StepSize.Line;
				
				Broadcaster.Invoke("swapped code view", m_boss);
			}
			
			var editor = m_boss.Get<ITextEditor>();
			editor.ShowLine(m_context.Location.LineNumber, -1, 8);
			
			DoSetInstructionPointer(m_context.Location.LineNumber);
		}
		
		private void DoShowIL(Context context)
		{
			string name = context.Method.FullName;
			if (m_currentView != name)
			{
				Broadcaster.Invoke("swapping code view", m_boss);
				
				DoSetTitle(name);
				m_path = null;
				
				var overlay = m_boss.Get<ITextOverlay>();
				overlay.Text = null;
				
				Boss boss = ObjectModel.Create("Disassembler");
				var disassembler = boss.Get<IDisassembler>();
				
				var text = m_boss.Get<IText>();
				m_currentView = name;
				string source = disassembler.Disassemble(context.Method.Metadata, m_context.Method.DeclaringType.Assembly.Location);
				DoBuildLineTable(source);
				
				text.Replace(source);
				m_document.Debugger.StepBy = StepSize.Min;
				
				Broadcaster.Invoke("swapped code view", m_boss);
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
			if (m_currentView != name)
			{
				Broadcaster.Invoke("swapping code view", m_boss);
				
				DoSetTitle(name);
				m_path = null;
				
				var overlay = m_boss.Get<ITextOverlay>();
				overlay.Text = null;
				
				m_currentView = name;
				m_lines.Clear();
				
				var text = m_boss.Get<IText>();
				text.Replace("...");
				DoSetInstructionPointer(-1);
				
				Broadcaster.Invoke("swapped code view", m_boss);
			}
		}
		
		private bool DoSourceIsOutOfDate()
		{
			DateTime stime = System.IO.File.GetLastWriteTime(m_context.SourceFile);
			DateTime atime = System.IO.File.GetLastWriteTime(m_context.Method.DeclaringType.Assembly.Location);
			return stime > atime;
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
				var range = new NSRange(metrics.GetLineOffset(line), 1);
				
				var editor = m_boss.Get<ITextEditor>();
				m_ipAnnotation = editor.GetAnnotation(range, AnnotationAlignment.Center);
				
				m_ipAnnotation.BackColor = NSColor.greenColor();
				m_ipAnnotation.Text = ">";
				m_ipAnnotation.Draggable = false;
				m_ipAnnotation.Visible = true;
			}
		}
		
		private void DoAssemblyLoaded(AssemblyMirror assembly)
		{
			if (m_document.BreakInMain && assembly.EntryPoint != null)
			{
				m_document.Debugger.AddBreakpoint(new Breakpoint("entry point", 1), assembly.EntryPoint, 0);
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private bool m_showIL;
		private DebuggerDocument m_document;
		private string m_title = "[debug]";
		private string m_currentView;
		private Context m_context;
		private string m_path;
		private Dictionary<long, int> m_lines = new Dictionary<long, int>();	// il offset => line number
		
		private ITextAnnotation m_ipAnnotation;
		#endregion
	}
}
