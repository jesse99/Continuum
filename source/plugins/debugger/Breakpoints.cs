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
using System.Linq;
using System.Collections.Generic;

namespace Debugger
{
	// Manages breakpoints added by the user.
	internal sealed class Breakpoints : IInterface, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			
			var handler = m_boss.Get<IMenuHandler>();
			handler.Register(this, 65, this.DoToggle, this.DoIsEnabled);
			
			Broadcaster.Register("opening document window", this);
			Broadcaster.Register("closing document window", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		internal static Breakpoint[] Values
		{
			get {return (from e in ms_entries select e.Breakpoint).ToArray();}
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opening document window":
					if (m_boss == value)
						DoRestoreAnnotations();
					break;
					
				case "closing document window":
					if (m_boss == value)
						DoResetAnnotations();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private bool DoIsEnabled()
		{
			return DoGetFile() != null && DoGetLine() > 1;
		}
		
		private void DoToggle()
		{
			string file = DoGetFile();
			int line = DoGetLine();
			
			int i = ms_entries.FindIndex(b => b.Breakpoint.File == file && b.Breakpoint.Line == line);
			if (i >= 0)
			{
				ms_entries[i].Annotation.Close();
				ms_entries.RemoveAt(i);
			}
			else if (line > 1 && file != null)		// can't put a breakpoint on the very first line because we have to backup a line to position the annotation window correctly
			{
				ITextAnnotation annotation = DoCreateBreakpoint(file, line);
				ms_entries.Add(new Entry(annotation, file, line));
			}
		}
		
		private ITextAnnotation DoCreateBreakpoint(string file, int line)
		{
			var metrics = m_boss.Get<ITextMetrics>();
			var range = new NSRange(metrics.GetLineOffset(line - 1), 1);
			
			var editor = m_boss.Get<ITextEditor>();
			ITextAnnotation annotation = editor.GetAnnotation(range);
			annotation.BackColor = NSColor.redColor();
			annotation.Text = "B";
			annotation.Draggable = false;
			annotation.Visible = true;
			
			return annotation;
		}
		
		private string DoGetFile()
		{
			var editor = m_boss.Get<ITextEditor>();
			string file = null;
			if (m_boss.Has<ICodeViewer>())
			{
				var viewer = m_boss.Get<ICodeViewer>();
				file = viewer.Path;
			}
			else
			{
				file = editor.Path;
			}
			
			return file;
		}
		
		private int DoGetLine()
		{
			var text = m_boss.Get<IText>();
			int offset = text.Selection.location;
			
			var metrics = m_boss.Get<ITextMetrics>();
			int line = metrics.GetLine(offset);
			
			return line;
		}
		
		private void DoRestoreAnnotations()
		{
			string file = DoGetFile();
			foreach (Entry entry in ms_entries)
			{
				if (entry.Breakpoint.File == file)
				{
					Contract.Assert(entry.Annotation == null, "entry.Annotation is not null");
					
					ITextAnnotation annotation = DoCreateBreakpoint(entry.Breakpoint.File, entry.Breakpoint.Line);
					entry.Annotation = annotation;
				}
			}
		}
		
		private void DoResetAnnotations()
		{
			string file = DoGetFile();
			foreach (Entry entry in ms_entries)
			{
				if (entry.Breakpoint.File == file)
					entry.Annotation = null;
			}
		}
		#endregion
		
		#region Private Types 
		private sealed class Entry
		{
			public Entry(ITextAnnotation annotation, string file, int line)
			{
				Contract.Requires(annotation != null, "annotation is null");
				
				Annotation = annotation;
				Breakpoint = new Breakpoint(file, line);
			}
			
			public ITextAnnotation Annotation {get; set;}
			
			public Breakpoint Breakpoint {get; private set;}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private static List<Entry> ms_entries = new List<Entry>();
		#endregion
	}
}
