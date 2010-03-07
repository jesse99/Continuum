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
			Broadcaster.Register("swapping code view", this);
			Broadcaster.Register("swapped code view", this);
			Broadcaster.Register("closing document window", this);
		}
		
		public void OnShutdown()
		{
			DoMoveBreakpointsFromWindowToSaved();		// saved windows won't be closed
			DoSavePrefs();
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		// Note that this may return the same breakpoint more than once.
		internal static IEnumerable<Breakpoint> GetBreakpoints(string file)
		{
			Contract.Requires(!string.IsNullOrEmpty(file), "file is empty or null");
			
			// First get the breakpoints for open windows.
			bool found = false;
			foreach (WindowedBreakpoint wbp in ms_windowedBreakpoints)
			{
				if (wbp.File == file)
				{
					found = true;
					yield return new Breakpoint(wbp.File, wbp.GetLine());
				}
			}
			
			if (!found)
			{
				// If we did not find a window associated with the file then try to
				// get breakpoints for closed windows or from a previous run of 
				// Continuum.
				foreach (Breakpoint bp in ms_savedBreakpoints)
				{
					if (bp.File == file)
					{
						yield return bp;
					}
				}
			}
		}
		
		internal static event Action<Breakpoint> AddedBreakpoint;
		internal static event Action<Breakpoint> RemovingBreakpoint;
		
		public void OnBroadcast(string name, object value)
		{
			Boss boss = value as Boss;
			
			switch (name)
			{
				case "opening document window":
					if (boss.Has<ITextEditor>())
					{
						var handler = boss.Get<IMenuHandler>();
						handler.Register(this, 65, () => DoToggle(boss), () => DoCanToggleBreakpoint(boss));
						
						DoCopyBreakpointsToWindow(boss);
					}
					break;
				
				case "swapping code view":
					DoMoveBreakpointsFromWindowToSaved(boss);
					break;
				
				case "swapped code view":
					DoCopyBreakpointsToWindow(boss);
					break;
				
				case "closing document window":
					if (boss.Has<ITextEditor>())
					{
						var handler = boss.Get<IMenuHandler>();
						handler.Deregister(this);
						
						DoMoveBreakpointsFromWindowToSaved(boss);
					}
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private bool DoCanToggleBreakpoint(Boss boss)
		{
			return DoGetFile(boss) != null && DoGetLineAtSelection(boss) > 1;
		}
		
		private void DoToggle(Boss boss)
		{
			string file = DoGetFile(boss);
			int line = DoGetLineAtSelection(boss);
			
			if (file != null)
				if (!DoRemoveBreakpoints(file, line))
					DoAddBreakpoints(file, line);
		}
		
		private bool DoRemoveBreakpoints(string file, int line)
		{
			int count = ms_windowedBreakpoints.RemoveAll(bp =>
			{
				bool removing = false;
				
				if (bp.File == file && bp.GetLine() == line)
				{
					if (RemovingBreakpoint != null)
						RemovingBreakpoint(new Breakpoint(file, line));
					
					bp.Annotation.Close();
					removing = true;
				}
				
				return removing;
			});
			
			return count > 0;
		}
		
		// There may be multiple windows viewing the same file so we need to
		// iterate over all document windows.
		private void DoAddBreakpoints(string file, int line)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			Boss[] bosses = boss.Get<IWindows>().All();
			foreach (Boss b in bosses)
			{
				if (DoGetFile(b) == file)
				{
					var bp = new WindowedBreakpoint(DoCreateBreakpoint(b, file, line), file);
					ms_windowedBreakpoints.Add(bp);
					
					if (AddedBreakpoint != null)
						AddedBreakpoint(new Breakpoint(file, line));
				}
			}
		}
		
		private ITextAnnotation DoCreateBreakpoint(Boss boss, string file, int line)
		{
			var metrics = boss.Get<ITextMetrics>();
			var range = new NSRange(metrics.GetLineOffset(line), 1);
			
			var editor = boss.Get<ITextEditor>();
			ITextAnnotation annotation = editor.GetAnnotation(range, AnnotationAlignment.Center);
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
		
		private int DoGetLineAtSelection(Boss boss)
		{
			var text = boss.Get<IText>();
			int offset = text.Selection.location;
			
			var metrics = boss.Get<ITextMetrics>();
			int line = metrics.GetLine(offset);
			
			return line;
		}
		
		// This is called as windows open so we only need to process the opening window.
		private void DoCopyBreakpointsToWindow(Boss boss)
		{
			string file = DoGetFile(boss);
			if (file != null)
			{
				// Another document window may be open for the same file so we need to
				// check those first.
				var bps = new List<WindowedBreakpoint>();
				foreach (WindowedBreakpoint wbp in ms_windowedBreakpoints)
				{
					if (wbp.File == file && wbp.Annotation.Parent != boss && wbp.Annotation.IsValid)
					{
						bps.Add(wbp);
					}
				}
				
				if (bps.Count > 0)
				{
					foreach (WindowedBreakpoint wbp in bps)
					{
						var b = new WindowedBreakpoint(DoCreateBreakpoint(boss, file, wbp.GetLine()), file);
						ms_windowedBreakpoints.Add(b);
					}
				}
				else
				{
					foreach (Breakpoint bp in ms_savedBreakpoints)
					{
						if (bp.File == file)
						{
							var b = new WindowedBreakpoint(DoCreateBreakpoint(boss, file, bp.Line), file);
							ms_windowedBreakpoints.Add(b);
						}
					}
				}
			}
		}
		
		private void DoMoveBreakpointsFromWindowToSaved()
		{
			Console.WriteLine("DoMoveBreakpointsFromWindowToSaved");
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			Boss[] bosses = boss.Get<IWindows>().All();
			foreach (Boss b in bosses)
			{
				DoMoveBreakpointsFromWindowToSaved(b);
			}
		}
		
		private void DoMoveBreakpointsFromWindowToSaved(Boss boss)
		{
			string file = DoGetFile(boss);
			
			if (file != null)
			{
			foreach (WindowedBreakpoint ww in ms_windowedBreakpoints)
			{
				if (ww.File == file)
				{
					Console.WriteLine("    {0} == {1}", ww.File, file);
					if (ww.Annotation.Parent == boss)
					{
						Console.WriteLine("    {0} == {1}", ww.Annotation.Parent, boss);
						if (ww.Annotation.IsValid)
							Console.WriteLine("    {0} == {1}", ww.Annotation.IsValid, true);
						else
							Console.WriteLine("    {0} != {1}", ww.Annotation.IsValid, true);
					}
					else
					{
						Console.WriteLine("    {0} != {1}", ww.Annotation.Parent, boss);
					}
				}
				else
				{
					Console.WriteLine("    {0} != {1}", ww.File, file);
				}
			}
				// Get a list of breakpoints within that window.
				IEnumerable<int> lines =
					from b in ms_windowedBreakpoints
						where b.File == file && b.Annotation.Parent == boss && b.Annotation.IsValid
					select b.GetLine();
				
				// Clear all breakpoints for that file.
				ms_savedBreakpoints.RemoveAll(b => b.File == file);
				
				// Add the breakpoints that were within the file.
				foreach (int line in lines)
				{
				Console.WriteLine("    moved {0}:{1}", System.IO.Path.GetFileName(file), line);
					ms_savedBreakpoints.Add(new Breakpoint(file, line));
				}
			}
			
			// FInish clearing breakpoints (we have to do this one down here because lines
			// is computed lazily.
			ms_windowedBreakpoints.RemoveAll(b => b.File == file && b.Annotation.Parent == boss);
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
						ms_savedBreakpoints.Add(new Breakpoint(parts[0], line));
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
			Contract.Assert(ms_windowedBreakpoints.Count == 0, "all windowed breakpoints must be moved into saved");
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			var names =
				(from e in ms_savedBreakpoints
				select e.File + ':' + e.Line).ToArray();
			defaults.setObject_forKey(NSArray.Create(names), NSString.Create("breakpoints"));
		}
		#endregion
		
		#region Private Types 
		private sealed class WindowedBreakpoint
		{
			public WindowedBreakpoint(ITextAnnotation annotation, string file)
			{
				Contract.Requires(annotation != null, "annotation is null");
				Contract.Requires(!string.IsNullOrEmpty(file), "file is null or empty");
				
				Annotation = annotation;
				File = file;
			}
			
			public ITextAnnotation Annotation {get; set;}
			
			// Full path to the file.
			public string File {get; private set;}
			
			public int GetLine()
			{
				var metrics = Annotation.Parent.Get<ITextMetrics>();
				int line = metrics.GetLine(Annotation.Anchor.location);
				return line;
			}
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private static List<Breakpoint> ms_savedBreakpoints = new List<Breakpoint>();
		private static List<WindowedBreakpoint> ms_windowedBreakpoints = new List<WindowedBreakpoint>();
		#endregion
	}
}
