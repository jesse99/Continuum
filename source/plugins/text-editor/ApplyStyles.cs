// Copyright (C) 2009 Jesse Jones
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

using Gear.Helpers;
using MCocoa;
using MObjc;
using Gear;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TextEditor
{
	// Handles all of the styling for text views.
	internal sealed class ApplyStyles : IObserver
	{
		static ApplyStyles()
		{
			ms_tabObserver = new ObserverTrampoline(ApplyStyles.DoResetTabStops);
			Broadcaster.Register("tab stops changed-pre", ms_tabObserver);
			DoResetTabStops("tab stops changed-pre", null);
		}
		
		public ApplyStyles(TextController controller, NSTextView text)
		{
			Contract.Requires(text.textStorage().layoutManagers().count() == 1, "expected one layout not " + text.textStorage().layoutManagers().count());
			
			m_controller = controller;
			m_storage = text.textStorage();
			m_layout = m_storage.layoutManagers().objectAtIndex(0).To<NSLayoutManager>();
			
			m_regexStyles = new CurrentStyles(controller, m_storage, this.DoFinishedRegex);
			m_parsedStyles = new CurrentStyles(controller, m_storage);
			
			Broadcaster.Register("tab stops changed", this);
			Broadcaster.Register("selected line color changed", this);
			Broadcaster.Register("computed style runs", this);
			Broadcaster.Register("computed parsed style runs", this);
			Broadcaster.Register("text changed", this);
			
			if (ms_selectedLineColor == null)
				DoSetTempAttrs();
			DoApplyParagraphStyles(true);
			
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "tab stops changed":
					DoApplyParagraphStyles((bool) value);
					break;
					
				case "selected line color changed":
					DoUpdateSelectedLineAttrs(name, value);
					break;
					
				case "computed style runs":
					DoApplyRegexStyles((StyleRuns) value);
					break;
					
				case "computed parsed style runs":
					DoApplyParsedStyles((StyleRuns) value);
					break;
					
				case "text changed":
					// If the text has changed stop applying styles until we get updated info.
					m_regexStyles.Reset();
					m_parsedStyles.Reset();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void Stop()
		{
			m_regexStyles.Stop();
			m_parsedStyles.Stop();
			
			Broadcaster.Unregister(this);
		}
		
		public void EditedRange(NSRange range)
		{
			m_regexStyles.EditedRange(range);
			m_parsedStyles.EditedRange(range);
		}
		
		public void HighlightLine(int offset, int length)
		{
			int textLength = (int) m_storage.length();
			
			NSRange range = new NSRange(0, textLength);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSBackgroundColorAttributeName, range);
			
			m_lineRange = DoGetClampedRange(offset, length, textLength);
			if (m_lineRange.length > 0)
			{
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSBackgroundColorAttributeName, ms_selectedLineColor, m_lineRange);
			}
		}
		
		public void HighlightError(int offset, int length)
		{
			int textLength = (int) m_storage.length();
			
			NSRange range = new NSRange(0, textLength);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSUnderlineStyleAttributeName, range);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSUnderlineColorAttributeName, range);
			
			m_errorRange = DoGetClampedRange(offset, length, textLength);
			if (m_errorRange.length > 0)
			{
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSUnderlineStyleAttributeName, NSNumber.Create(2), m_errorRange);
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSUnderlineColorAttributeName, ms_errorColor, m_errorRange);
			}
		}
		
		#region Private Methods		
		private void DoApplyRegexStyles(StyleRuns runs)
		{
			if (m_controller.Path != null && Paths.AreEqual(runs.Path, m_controller.Path))
			{
				m_finished = false;
				Console.WriteLine("applying regex");
			
				m_regexStyles.Reset(runs.Edit, runs.Runs);
				m_regexStyles.ApplyDefault();
				m_regexStyles.ApplyStyles();
			}
		}
		
		private void DoApplyParsedStyles(StyleRuns runs)
		{
			if (m_controller.Path != null && Paths.AreEqual(runs.Path, m_controller.Path))
			{
				Array.Sort(runs.Runs, (lhs, rhs) => lhs.CompareTo(rhs));
				m_parsedStyles.Reset(runs.Edit, runs.Runs);
				
				if (m_finished && m_parsedStyles.Edit == m_regexStyles.Edit)
				{
					m_parsedStyles.ApplyStyles();
				}
			}
		}
		
		private void DoFinishedRegex()
		{
			m_finished = true;
			
			if (m_parsedStyles.Edit == m_regexStyles.Edit)
			{
				m_parsedStyles.ApplyStyles();
			}
		}
		
		private static void DoResetTabStops(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			float stops = defaults.floatForKey(NSString.Create("tab stops"));
			
			NSMutableParagraphStyle style = NSMutableParagraphStyle.Create();
			style.setTabStops(NSMutableArray.Create());
			style.setDefaultTabInterval(stops);
			
			ms_paragraphAttrs.setObject_forKey(style, Externs.NSParagraphStyleAttributeName);
		}
		
		private void DoApplyParagraphStyles(bool commit)
		{
			if (commit)
			{
				NSRange range = new NSRange(0, (int) m_storage.length());
				m_storage.addAttributes_range(ms_paragraphAttrs, range);
			}
		}
		
		// We need to apply the temporary attributes even when the text has changed so
		// we need to be careful with the offsets and lengths we use.
		private NSRange DoGetClampedRange(int offset, int length, int textLength)
		{
			length = Math.Min(length, textLength - offset);
			return new NSRange(offset, length);
		}
		
		private static void DoSetTempAttrs()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			var data = defaults.objectForKey(NSString.Create("selected line color")).To<NSData>();
			if (!NSObject.IsNullOrNil(data))
				ms_selectedLineColor = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>().Retain();
			else
				ms_selectedLineColor = NSColor.yellowColor().Retain();
				
			ms_errorColor = NSColor.redColor().Retain();
		}
		
		private void DoUpdateSelectedLineAttrs(string name, object value)
		{
			DoSetTempAttrs();						// little inefficient to always do this but it shouldn't matter				
			
			HighlightLine(m_lineRange.location, m_lineRange.length);
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private NSTextStorage m_storage;
		private NSLayoutManager m_layout;
		private CurrentStyles m_regexStyles;
		private CurrentStyles m_parsedStyles;
		private NSRange m_lineRange;
		private NSRange m_errorRange;
		private bool m_finished;
		
		private static ObserverTrampoline ms_tabObserver;
		private static NSColor ms_selectedLineColor;
		private static NSColor ms_errorColor;
		private static NSMutableDictionary ms_paragraphAttrs = NSMutableDictionary.Create().Retain();
		#endregion
	}
}
