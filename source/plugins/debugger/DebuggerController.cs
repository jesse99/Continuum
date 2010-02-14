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
using MObjc;
//using Mono.Cecil;
//using Mono.Cecil.Binary;
using Mono.Debugger;
using Shared;
using System;
using System.Collections.Generic;
//using System.IO;

namespace Debugger
{
	[ExportClass("DebuggerController", "NSWindowController", Outlets = "label")]
	internal sealed class DebuggerController : NSWindowController
	{
		public DebuggerController(DebuggerDocument doc) : base(NSObject.AllocAndInitInstance("DebuggerController"))
		{
			m_doc = doc;
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("debugger"), this);
			window().setDelegate(this);
			
			m_label = new IBOutlet<NSTextField>(this, "label").Value;
			m_label.setStringValue(NSString.Create("Connecting."));
			
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Register(this, 61, () => m_doc.Debugger.Run(), this.DoIsPaused);
			handler.Register(this, 62, () => m_doc.Debugger.StepOver(), this.DoIsPaused);
			handler.Register(this, 63, () => m_doc.Debugger.StepIn(), this.DoIsPaused);
			handler.Register(this, 64, () => m_doc.Debugger.StepOut(), this.DoIsPaused);
			
			DoOpenCodeWindow();
			
			m_doc.Debugger.StateEvent += this.OnStateChanged;
			m_doc.Debugger.AssemblyLoadedEvent += this.OnAssemblyLoaded;
			m_doc.Debugger.BreakpointEvent += this.OnPaused;
			m_doc.Debugger.SteppedEvent += this.OnPaused;
			
			ActiveObjects.Add(this);
			autorelease();							// get rid of the retain done by AllocAndInitInstance
		}
		
		public void windowWillClose(NSObject notification)
		{
			Boss boss = ObjectModel.Create("Application");
			var handler = boss.Get<IMenuHandler>();
			handler.Deregister(this);
			
			// TODO: get rid of the code window too
			
			m_doc.Debugger.Dispose();
			window().autorelease();
		}
		
		#region Private Methods
		private void OnStateChanged(State state)
		{
			m_label.setStringValue(NSString.Create(state.ToString()));
			
			if (state != State.Paused)
			{
				var text = m_codeBoss.Get<IText>();
				text.Replace(state.ToString());
			}
			
			if (state == State.Connected)
				m_doc.Debugger.Run();
		}
		
		private void OnAssemblyLoaded(AssemblyMirror assembly)
		{
			if (assembly.EntryPoint != null)				// TODO: need a pref or setting for this
			{
				m_doc.Debugger.AddBreakpoint(assembly.EntryPoint, 0);
			}
		}
		
		private void OnPaused(Location location)
		{
			var text = m_codeBoss.Get<IText>();
			
			if (System.IO.File.Exists(location.SourceFile))
			{
				if (m_currentFile != location.SourceFile)
				{
					text.Replace(System.IO.File.ReadAllText(location.SourceFile));
					m_currentFile = location.SourceFile;
				}
				
				var editor = m_codeBoss.Get<ITextEditor>();
				editor.ShowLine(location.LineNumber, -1, 8);
			}
			else
			{
				text.Replace(string.Format("Couldn't find '{0}'.", location.SourceFile));
			}
		}
		
		private void DoOpenCodeWindow()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var create = boss.Get<ICreate>();
			m_codeBoss = create.Create("CodeViewer");
			
			var viewer = m_codeBoss.Get<ICodeViewer>();
			viewer.Init(m_doc.Debugger);
			
			var editor = m_codeBoss.Get<ITextEditor>();
			editor.Editable = false;
		}
		
		private bool DoIsPaused()
		{
			return m_doc.Debugger.State == State.Paused;
		}
		#endregion
		
		#region Fields
		private DebuggerDocument m_doc;
		private NSTextField m_label;
		private Boss m_codeBoss;
		private string m_currentFile;
		#endregion
	}
}
