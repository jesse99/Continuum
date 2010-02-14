// Copyright (C) 2007-2008 Jesse Jones
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
using System.Text;
using System.Text.RegularExpressions;

namespace Find
{
	internal sealed class FindInFile : IFind
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Find()
		{
			if (m_find == null)
				m_find = new FindController();
			
			m_find.Open(this);
		}
		
		public void FindInFiles()
		{
			if (m_findInFiles == null)
				m_findInFiles = new FindInFilesController();
			
			m_findInFiles.Open(this);
		}
		
		public void FindNext()
		{
			try
			{
				IText text = DoFindTextWindow();
				
				int index = text.Selection.location + text.Selection.length;
				if (m_find.UseRegex && text.Selection.length == 0)
					if (FindText == "^" || FindText == "$")
						++index;
				int length = text.Text.Length - index;
				
				DoMakeRe();
				Match match = m_re.Match(text.Text, index, length);
				if (match.Success)
				{
					var window = text.Boss.Get<IWindow>();
					window.Window.makeKeyAndOrderFront(null);
					
					text.Selection = new NSRange(match.Index, match.Length);
					text.ShowSelection();
				}
				else
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't find next");
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunCriticalAlertPanel(null, message);
			}
		}
		
		// This code is not very efficient but there's no good way to
		// do a backwards regex search or even to find all matches
		// within a substring.
		public void FindPrevious()
		{
			try
			{
				IText text = DoFindTextWindow();
				DoMakeRe();
				
				int index = 0;
				Match match = null;
				while (true)
				{
					Match candidate = m_re.Match(text.Text, index);
					if (candidate.Success)
					{
						if (candidate.Index < text.Selection.location)
						{
							match = candidate;
							index = candidate.Index + candidate.Length;
							if (m_find.UseRegex && text.Selection.length == 0)
								if (FindText == "^" || FindText == "$")
									++index;
						}
						else
							break;
					}
					else
						break;
				}
					
				if (match != null)
				{
					var window = text.Boss.Get<IWindow>();
					window.Window.makeKeyAndOrderFront(null);
					
					text.Selection = new NSRange(match.Index, match.Length);
					text.ShowSelection();
				}
				else
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't find previous");
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunCriticalAlertPanel(null, message);
			}
		}
		
		public void Replace()
		{
			try
			{
				IText text = DoFindTextWindow();
				
				int index = text.Selection.location;
				int length = text.Text.Length - index;
				
				DoMakeRe();
				Match match = m_re.Match(text.Text, index, length);
				if (match.Success)
				{
					string result = match.Result(ReplaceText);
					text.Replace(result, match.Index, match.Length, "Replace");
					
					text.Selection = new NSRange(match.Index, result.Length);
					text.ShowSelection();
				}
				else
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't replace");
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunCriticalAlertPanel(null, message);
			}
		}
		
		public void ReplaceAndFind()
		{
			try
			{
				IText text = DoFindTextWindow();
				DoMakeRe();
				
				// If the current selection matches the re then replace it.
				Match match = m_re.Match(text.Text, text.Selection.location, text.Selection.length);
				if (match.Success)
				{
					string result = match.Result(ReplaceText);
					text.Replace(result, match.Index, match.Length, "Replace");
				}
				
				// And find the next string.
				FindNext();
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't find replace and find");
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunCriticalAlertPanel(null, message);
			}
		}
		
		public void ReplaceAll()
		{
			try
			{
				IText text = DoFindTextWindow();
				int index = text.Selection.location;
				
				DoMakeRe();
				
				string result = m_re.Replace(text.Text, ReplaceText, int.MaxValue, index);
				if (result != text.Text)
				{
					text.Replace(result, 0, text.Text.Length, "Replace All");
					
					text.Selection = new NSRange(index, 0);
					text.ShowSelection();
				}
				else
					Functions.NSBeep();
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't replace all");
				Log.WriteLine(TraceLevel.Error, "App", e.ToString());
				
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunCriticalAlertPanel(null, message);
			}
		}
		
		public bool CanFind()
		{
			IText text = DoFindTextWindow();
			
			return text != null && text.Text.Length > 0;
		}
		
		public bool CanFindNext()
		{
			return m_find != null && CanFind() && FindText.Length > 0;
		}
		
		public bool CanFindPrevious()
		{
			return m_find != null && CanFind() && FindText.Length > 0;
		}
		
		public bool CanUseSelectionForFind()
		{
			bool can = false;
			
			IText text = DoFindTextWindow();
			if (text != null && text.Text.Length > 0)
				can = text.Selection.length > 0;
			
			return can;
		}
		
		public bool CanUseSelectionForReplace()
		{
			bool can = false;
			
			IText text = DoFindTextWindow();
			if (text != null && text.Text.Length > 0)
			{
				if (text.Selection.length > 0)
				{
					if (text.Boss.Has<ITextEditor>())
					{
						var editor = text.Boss.Get<ITextEditor>();
						can = editor.Editable;
					}
					else
					{
						can = true;
					}
				}
			}
			
			return can;
		}
		
		public bool CanReplace()
		{
			IText text = DoFindTextWindow();
			
			bool can = false;
			
			// Window needs some text.
			if (text != null && text.Text.Length > 0)
			{
				// Need text to find (but not to replace since we can replace with nothing).
				if (m_find != null && FindText.Length > 0)
				{
					// Window needs to be editable.
					if (text.Boss.Has<ITextEditor>())
					{
						var editor = text.Boss.Get<ITextEditor>();
						can = editor.Editable;
					}
					else
					{
						can = true;
					}
				}
			}
			
			return can;
		}
		
		public void UseSelectionForFind()
		{
			if (m_find == null)
				m_find = new FindController();
			if (m_findInFiles == null)
				m_findInFiles = new FindInFilesController();
				
			IText text = DoFindTextWindow();
			Contract.Assert(text != null, "text is null");
			
			string s = text.Text.Substring(text.Selection.location, text.Selection.length);
			if (s.Length > 0)
			{	
				m_find.UpdateFindList();
				m_findInFiles.UpdateFindList();
			}
			m_find.FindText = s;
			m_findInFiles.FindText = s;
		}
		
		public void UseSelectionForReplace()
		{
			if (m_find == null)
				m_find = new FindController();
			if (m_findInFiles == null)
				m_findInFiles = new FindInFilesController();
				
			IText text = DoFindTextWindow();
			Contract.Assert(text != null, "text is null");
			
			string s = text.Text.Substring(text.Selection.location, text.Selection.length);
			if (s.Length > 0)
			{
				m_find.UpdateReplaceList();
				m_findInFiles.UpdateReplaceList();
			}
			m_find.ReplaceText = s;
			m_findInFiles.ReplaceText = s;
		}
		
		public string FindText
		{
			get {return m_find != null ? m_find.FindText : string.Empty;}
		}
		
		public string ReplaceText
		{
			get {return m_find != null ? m_find.ReplaceText : string.Empty;}
		}
		
		#region Private Methods
		private void DoMakeRe()
		{
			Re re = new Re
			{
				UseRegex = m_find.UseRegex,
				CaseSensitive = m_find.CaseSensitive,
				MatchWords = m_find.MatchWords,
				WithinText = m_find.WithinText,
			};
			m_re = re.Make(FindText);
		}
		
		private IText DoFindTextWindow()
		{
			IText result = null;
			
			// First try the text editor windows.
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			Boss main = windows.Main();
			if (main != null)
				result = main.Get<IText>();
			
			// Then the transcript window.
			if (result == null)
			{
				boss = ObjectModel.Create("Application");
				result = boss.Get<IText>();
			}
			
			return result;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private FindController m_find;
		private FindInFilesController m_findInFiles;
		private Regex m_re;
		#endregion
	}
}
