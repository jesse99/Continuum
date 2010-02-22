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
using Shared;
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace Debugger
{
	// Manages breakpoints added by the user.
	internal sealed class Breakpoints : IStartup, IShutdown, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public void OnStartup()
		{
			DoLoadPrefs();
			
			Broadcaster.Register("opening document window", this);
			Broadcaster.Register("closing document window", this);
		}
		
		public void OnShutdown()
		{
			DoSavePrefs();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		internal static IEnumerator<Breakpoint> GetBreakpoints(string file)
		{
			Contract.Requires(!string.IsNullOrEmpty(file), "file is empty or null");
			
			foreach (Entry entry in ms_entries)
			{
				if (entry.Breakpoint.File == file)
				{
					if (entry.Annotation == null)
					{
						yield return entry.Breakpoint;
					}
					else if (entry.Annotation.IsValid)
					{
						var metrics = entry.Annotation.Parent.Get<ITextMetrics>();
						int line = metrics.GetLine(entry.Annotation.Anchor.location);
						if (line == entry.Breakpoint.Line)
							yield return entry.Breakpoint;
						else
							yield return new Breakpoint(entry.Breakpoint.File, line);
					}
				}
			}
		}
		
		public void OnBroadcast(string name, object value)
		{
			Boss boss = value as Boss;
			
			switch (name)
			{
				case "opening document window":
					if (boss.Has<ITextEditor>())
					{
						var handler = boss.Get<IMenuHandler>();
						handler.Register(this, 65, () => DoToggle(boss), () => DoIsEnabled(boss));
						
						DoRestoreAnnotations(boss);
					}
					break;
					
				case "closing document window":
					if (boss.Has<ITextEditor>())
					{
						var handler = boss.Get<IMenuHandler>();
						handler.Deregister(this);
						
						DoResetAnnotations(boss);
					}
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private bool DoIsEnabled(Boss boss)
		{
			return DoGetFile(boss) != null && DoGetLine(boss) > 1;
		}
		
		private void DoToggle(Boss boss)
		{
			string file = DoGetFile(boss);
			int line = DoGetLine(boss);
			
			int i = ms_entries.FindIndex(b => b.Breakpoint.File == file && b.Breakpoint.Line == line);
			if (i >= 0)
			{
				ms_entries[i].Annotation.Close();
				ms_entries.RemoveAt(i);
			}
			else if (line > 1 && file != null)		// can't put a breakpoint on the very first line because we have to backup a line to position the annotation window correctly
			{
				ITextAnnotation annotation = DoCreateBreakpoint(boss, file, line);
				ms_entries.Add(new Entry(annotation, file, line));
			}
		}
		
		private ITextAnnotation DoCreateBreakpoint(Boss boss, string file, int line)
		{
			var metrics = boss.Get<ITextMetrics>();
			var range = new NSRange(metrics.GetLineOffset(line - 1), 1);
			
			var editor = boss.Get<ITextEditor>();
			ITextAnnotation annotation = editor.GetAnnotation(range);
			annotation.BackColor = NSColor.redColor();
			annotation.Text = "B";
			annotation.Draggable = false;
			annotation.Visible = true;
			
			return annotation;
		}
		
		private string DoGetFile(Boss boss)
		{
			var editor = boss.Get<ITextEditor>();
			string file = null;
			if (boss.Has<ICodeViewer>())
			{
				var viewer = boss.Get<ICodeViewer>();
				file = viewer.Path;
			}
			else
			{
				file = editor.Path;
			}
			
			return file;
		}
		
		private int DoGetLine(Boss boss)
		{
			var text = boss.Get<IText>();
			int offset = text.Selection.location;
			
			var metrics = boss.Get<ITextMetrics>();
			int line = metrics.GetLine(offset);
			
			return line;
		}
		
		private void DoRestoreAnnotations(Boss boss)
		{
			string file = DoGetFile(boss);
			foreach (Entry entry in ms_entries)
			{
				if (entry.Breakpoint.File == file)
				{
					Contract.Assert(entry.Annotation == null, "entry.Annotation is not null");
					
					ITextAnnotation annotation = DoCreateBreakpoint(boss, entry.Breakpoint.File, entry.Breakpoint.Line);
					entry.Annotation = annotation;
				}
			}
		}
		
		private void DoResetAnnotations(Boss boss)
		{
			string file = DoGetFile(boss);
			
			for (int i = 0; i < ms_entries.Count; ++i)
			{
				Entry entry = ms_entries[i];
				if (entry.Breakpoint.File == file)
				{
					Contract.Assert(entry.Annotation != null, "annotation is null");
					
					if (entry.Annotation.IsValid)
					{
						// If the anchor text was not deleted then we need to reset
						// the annotation reference and potentially update the line.
						var metrics = boss.Get<ITextMetrics>();
						int line = metrics.GetLine(entry.Annotation.Anchor.location);
						
						ms_entries[i] = new Entry(file, line);
					}
				}
			}
			
			// If the anchor text was deleted then we need to remove the breakpoint.
			ms_entries.RemoveAll(e => e.Breakpoint.File == file && e.Annotation != null && !e.Annotation.IsValid);
		}
		
		private void DoLoadPrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			NSArray array = defaults.arrayForKey(NSString.Create("breakpoints"));
			if (!NSObject.IsNullOrNil(array))
			{
				foreach (NSString name in array)
				{
					string[] parts = name.ToString().Split(':');
					
					int line;
					if (parts.Length == 2 && int.TryParse(parts[1], out line))
					{
						ms_entries.Add(new Entry(parts[0], line));
					}
					else
					{
						Log.WriteLine(TraceLevel.Error, "Startup", "bad breakpoint pref: {0:D}", name);
					}
				}
			}
		}
		
		private void DoSavePrefs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			var names =
				(from e in ms_entries
					where e.Annotation == null || e.Annotation.IsValid
				select e.Breakpoint.File + ':' + e.Breakpoint.Line).ToArray();
			defaults.setObject_forKey(NSArray.Create(names), NSString.Create("breakpoints"));
		}
		#endregion
		
		#region Private Types 
		private sealed class Entry
		{
			public Entry(string file, int line)
			{
				Breakpoint = new Breakpoint(file, line);
			}
			
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
