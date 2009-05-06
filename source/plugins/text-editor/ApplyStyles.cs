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
	// Color codes text using StyleRuns.
	internal sealed class ApplyStyles : IObserver
	{
		static ApplyStyles()
		{
			ms_tabObserver = new ObserverTrampoline(ApplyStyles.DoResetTabStops);
			Broadcaster.Register("tab stops changed-pre", ms_tabObserver);
			DoResetTabStops("tab stops changed-pre", null);
			
			ms_styleObserver = new ObserverTrampoline(ApplyStyles.DoResetFont);
			foreach (string name in ms_names)
			{
				Broadcaster.Register(name + "-pre", ms_styleObserver);
				ms_attributes.Add(name, NSMutableDictionary.Create().Retain());
				DoResetFont(name + "-pre", null);
			}
		}
		
		public ApplyStyles(TextController controller, NSTextView text, NSScrollView scroller)
		{
			Contract.Requires(text.textStorage().layoutManagers().count() == 1, "expected one layout not " + text.textStorage().layoutManagers().count());
			
			m_controller = controller;
			m_text = text;
			m_storage = text.textStorage();
			m_layout = m_storage.layoutManagers().objectAtIndex(0).To<NSLayoutManager>();
			m_scroller = scroller;
			
			Broadcaster.Register("tab stops changed", this);
			foreach (string name in ms_names)
			{
				Broadcaster.Register(name, this);
			}
			
			Broadcaster.Register("selected line color changed", this);
			if (ms_selectedLineColor == null)
				DoSetTempAttrs();
			
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "tab stops changed":
					DoUpdateTabStops(name, value);
					break;
					
				case "selected line color changed":
					DoUpdateSelectedLineAttrs(name, value);
					break;
					
				default:
					DoUpdateAttributes(name, value);
					break;
			}
		}
		
		public void Reset(string text)
		{
			m_storage.setAttributedString(GetDefaultStyledString(text));
		}
		
		internal static NSAttributedString GetDefaultStyledString(string text)
		{
			return GetDefaultStyledString(NSString.Create(text));;
		}
		
		internal static NSAttributedString GetDefaultStyledString(NSString text)
		{
			NSMutableDictionary attrs = NSMutableDictionary.Create();
			attrs.addEntriesFromDictionary(ms_attributes["text default font changed"]);
			attrs.addEntriesFromDictionary(ms_paragraphAttrs);
			
			return NSAttributedString.Alloc().initWithString_attributes(text, attrs);
		}
		
		public void Stop()
		{
			m_stopped = true;
			Broadcaster.Unregister(this);
		}
		
		public void Apply(int editCount, List<StyleRun> runs)
		{
			if (!m_stopped)
			{
				m_editCount = editCount;
				m_length = (int) m_storage.length();
				m_workSet = runs;
				m_origin = -1;
				m_elapsed = TimeSpan.Zero;
				m_numIterations = 0;
				
				if (runs.Count > 0)
				{
					m_currentSet = new List<StyleRun>(runs);
					m_currentSet.Sort();
					
					// This is an optimization for the common case where we have a whole
					// bunch of runs at the start which are indentical to runs we have
					// already applied (there may also be attributes at the end which will
					// be equal to what we applied but they are more difficult to handle
					// because the run offsets will normally have changed).
					//
					// Note that this is also important because the text will jump if we apply
					// attributes to text above whatever text we're viewing (and some runs
					// are taller than others).
					//
					// TODO: maybe it would be even better to use attributeRuns and only
					// apply styles if the style name (or a numeric id) does not match.
					if (m_appliedSet != null && m_appliedSet.Count > 0)
					{
						m_workSet.Sort();				// won't be sorted for c#
						m_appliedSet.Sort();
						
						int count = 0;
						int max = Math.Min(m_appliedSet.Count, m_workSet.Count);
						while (count < max && m_appliedSet[count] == m_workSet[count] && m_appliedSet[count].Offset < m_editStart)
							++count;
						m_workSet.RemoveRange(0, count);
						m_appliedSet.RemoveRange(count, m_appliedSet.Count - count);
						
						if (m_workSet.Count > 0)
							DoResetAttributes(m_workSet[0].Offset, m_length - m_workSet[0].Offset, m_length);
					}
					else
					{
//	Console.WriteLine("   applying all the runs");
//	for (int i = 0; i < runs.Count; ++i)
//		if (runs[i].Type != StyleType.Default && runs[i].Type != StyleType.Tabs)
//			Console.WriteLine("   {0} {1}", runs[i].Type, m_text.string_().description().Substring(runs[i].Offset, runs[i].Length));
						m_appliedSet = new List<StyleRun>(runs.Count);
						DoResetAttributes(0, m_length, m_length);
					}
					
					m_editStart = int.MaxValue;
					
					DoApplyStyles();
				}
				else
				{
					// If there are no runs (which will be the case for the plain-text language) then we
					// need to set the attributes to the default (so attributes within pasted text are reset).
					m_currentSet = new List<StyleRun>();
					m_workSet = new List<StyleRun>();
					m_appliedSet = new List<StyleRun>();
					
					int textLength = (int) m_storage.length();
					NSRange range = new NSRange(0, textLength);
					m_storage.setAttributes_range(ms_attributes["text default font changed"], range);
				}
			}
		}
		
		// On occasion the text which is inserted matches the text which was moved. This
		// messes up the m_appliedSet optimization so we keep track of where the first edit
		// was.
		public void EditedRange(NSRange range)
		{
			m_editStart = Math.Min(range.location, m_editStart);
		}
		
		public void HighlightLine(int offset, int length)
		{
			int textLength = (int) m_storage.length();
			
			NSRange range = new NSRange(0, textLength);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSBackgroundColorAttributeName, range);
			
			m_line = new StyleRun(offset, length, StyleType.Line);
			
			range = DoGetClampedRange(m_line.Offset, m_line.Length, textLength);
			if (range.length > 0)
			{
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSBackgroundColorAttributeName, ms_selectedLineColor, range);
			}
		}
		
		public void HighlightError(int offset, int length)
		{
			int textLength = (int) m_storage.length();
			
			NSRange range = new NSRange(0, textLength);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSUnderlineStyleAttributeName, range);
			m_layout.removeTemporaryAttribute_forCharacterRange(Externs.NSUnderlineColorAttributeName, range);
			
			m_error = new StyleRun(offset, length, StyleType.Error);
			
			range = DoGetClampedRange(m_error.Offset, m_error.Length, textLength);
			if (range.length > 0)
			{
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSUnderlineStyleAttributeName, NSNumber.Create(2), range);
				m_layout.addTemporaryAttribute_value_forCharacterRange(Externs.NSUnderlineColorAttributeName, ms_errorColor, range);
			}
		}
		
		#region Private Methods
		private void DoSortStyles()
		{
			NSRect bounds = m_scroller.documentVisibleRect();
			NSPoint center = bounds.Center;
			int origin = (int) m_text.characterIndexForInsertionAtPoint(center);
			
			if (origin != m_origin)
			{
				m_origin = origin;
			
				Stopwatch watch = Stopwatch.StartNew();
				m_workSet.Sort((lhs, rhs) =>
				{
					// We want to sort the runs so that those closest to the origin
					// are processed first.
					int lhsMidpoint = lhs.Offset + lhs.Length/2;
					int rhsMidpoint = rhs.Offset + rhs.Length/2;
					
					// Except for type and member which must be processed after the
					// run they are within.
					if (lhs.Type == StyleType.Type || lhs.Type == StyleType.Member)
						lhsMidpoint += 2*m_length;
					if (rhs.Type == StyleType.Type || rhs.Type == StyleType.Member)
						rhsMidpoint += 2*m_length;
					
					int lhsDist = Math.Abs(lhsMidpoint - m_origin);
					int rhsDist = Math.Abs(rhsMidpoint - m_origin);
					
					return rhsDist.CompareTo(lhsDist);
				});
				Log.WriteLine(TraceLevel.Verbose, "Styler", "    sort: {0:0.000} secs", watch.ElapsedMilliseconds/1000.0);
			}
		}
		
		private const int MaxChunkSize = 200;
		private static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(100);
		
		// Applying styles is a slow operation and has to be done in the main thread
		// so we do it in chunks centered on the user's selection.
		private void DoApplyStyles()
		{
			if (!m_stopped)
			{
				// Only apply the runs if the text has not changed (or the runs will appear
				// in the wrong place).
				int length = (int) m_storage.length();
				if (m_editCount != m_controller.EditCount || m_length != length)
					m_workSet.Clear();
					
				if (m_workSet.Count > 0)
				{
					Stopwatch watch = Stopwatch.StartNew();
					DoSortStyles();
					m_storage.beginEditing();
					
					try
					{
						for (int i = m_workSet.Count - 1; i >= Math.Max(0, m_workSet.Count - MaxChunkSize); --i)
						{
//		if (m_workSet[i].Type != StyleType.Default && m_workSet[i].Type != StyleType.Tabs)
//			Console.WriteLine("   {0} {1}", m_workSet[i].Type, m_text.string_().description().Substring(m_workSet[i].Offset, m_workSet[i].Length));

#if DEBUG
							if (i + 1 < m_workSet.Count)
								if (m_workSet[i].Type != StyleType.Type && m_workSet[i].Type != StyleType.Member)
									if (m_workSet[i + 1].Type == StyleType.Type || m_workSet[i + 1].Type == StyleType.Member)
										Log.WriteLine(TraceLevel.Error, "Styler", "Applying {0} after {1}", m_workSet[i].Type, m_workSet[i + 1].Type);
#endif
						
							DoApplyStyle(m_workSet[i], length);
							m_appliedSet.Add(m_workSet[i]);
						}
						
						if (m_workSet.Count > MaxChunkSize)
							m_workSet.RemoveRange(m_workSet.Count - MaxChunkSize, MaxChunkSize);
						else
							m_workSet.Clear();
					}
					finally
					{
						m_storage.endEditing();
						m_elapsed += watch.Elapsed;
						++m_numIterations;
					}
					
					if (m_workSet.Count > 0)
					{
						NSApplication.sharedApplication().BeginInvoke(this.DoApplyStyles, ChunkDelay);
					}
					else
					{
						Log.WriteLine(TraceLevel.Verbose, "Styler", "applied style in {0:0.000} secs", m_elapsed.TotalMilliseconds/1000.0);
						Log.WriteLine(TraceLevel.Verbose, "Styler", "took {0} iterations, average time {1:0.000}", m_numIterations, (m_elapsed.TotalMilliseconds/m_numIterations)/1000.0);
					}
				}
			}
		}
		
		// TODO: it would be quite a bit nicer if we could use setAttributes_range for all but
		// the Error and Line runs. However if we do this the tab stops attribute will be over
		// written which means that the attributes we set must include tab stops. But if this
		// is done the text system thinks the tab stops have changed and the line jumps
		// around as it is edited (and ensureLayoutForBoundingRect_inTextContainer doesn't
		// help).
		private void DoApplyStyle(StyleRun run, int length)
		{
			NSRange range = new NSRange(run.Offset, run.Length);
			if (run.Offset + run.Length > length)	// this may happen if the text is edited while we are applying runs
				return;
			if (run.Length == 0)
				return;
			
			switch (run.Type)
			{
				case StyleType.Default:
					m_storage.addAttributes_range(ms_attributes["text default font changed"], range);
					break;
				
				case StyleType.Spaces:		// note that we can't do something similiar for control characters because they are zero-width
					m_storage.addAttributes_range(ms_attributes["text spaces color changed"], range);
					break;
				
				case StyleType.Tabs:
					m_storage.addAttributes_range(ms_attributes["text tabs color changed"], range);
					break;
				
				case StyleType.Keyword:
					m_storage.addAttributes_range(ms_attributes["text keyword font changed"], range);
					break;
				
				case StyleType.String:
					m_storage.addAttributes_range(ms_attributes["text string font changed"], range);
					break;
				
				case StyleType.Number:
					m_storage.addAttributes_range(ms_attributes["text number font changed"], range);
					break;
				
				case StyleType.Comment:
					m_storage.addAttributes_range(ms_attributes["text comment font changed"], range);
					break;
				
				case StyleType.Other1:
					m_storage.addAttributes_range(ms_attributes["text other1 font changed"], range);
					break;
				
				case StyleType.Preprocessor:
					m_storage.addAttributes_range(ms_attributes["text preprocess font changed"], range);
					break;
				
				case StyleType.Other2:
					m_storage.addAttributes_range(ms_attributes["text other2 font changed"], range);
					break;
				
				case StyleType.Member:
					m_storage.addAttributes_range(ms_attributes["text member font changed"], range);
					break;
				
				case StyleType.Type:
					m_storage.addAttributes_range(ms_attributes["text type font changed"], range);
					break;
				
				case StyleType.Error:				// this is a parse error (HighlightError handles build errors)
					m_storage.addAttribute_value_range(Externs.NSUnderlineStyleAttributeName, NSNumber.Create(2), range);
					m_storage.addAttribute_value_range(Externs.NSUnderlineColorAttributeName, ms_errorColor, range);
					break;
				
				default:
					Contract.Assert(false, "Bad style type: " + run.Type);
					break;
			}
		}
		
		private void DoResetAttributes(int offset, int length, int textLength)
		{
			if (offset + length > textLength)	// this may happen if the text is edited while we are applying runs
				return;
			if (length == 0)
				return;
			
			NSRange range = new NSRange(offset, length);
			
			m_storage.removeAttribute_range(Externs.NSForegroundColorAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSBackgroundColorAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSUnderlineStyleAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSUnderlineColorAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSStrokeWidthAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSObliquenessAttributeName, range);
			m_storage.removeAttribute_range(Externs.NSShadowAttributeName, range);
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
		
		private void DoUpdateTabStops(string inName, object value)
		{
			if ((bool) value)
			{
				NSRange range = new NSRange(0, (int) m_storage.length());
				m_storage.addAttributes_range(ms_paragraphAttrs, range);
			}
		}
		
		private static void DoResetFont(string name, object value)
		{
			Contract.Assert(name.EndsWith("-pre"), name + " does not end with '-pre'");
			name = name.Remove(name.Length - 4);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			string key = name.Substring(0, name.Length - "changed".Length);
			
			ms_attributes[name].removeAllObjects();
			
			// font
			NSString fname = defaults.stringForKey(NSString.Create(key + "name"));
			float ptSize = defaults.floatForKey(NSString.Create(key + "size"));
			if (!NSObject.IsNullOrNil(fname))
			{
				NSFont font = NSFont.fontWithName_size(fname, ptSize);
				ms_attributes[name].setObject_forKey(font, Externs.NSFontAttributeName);
			}
			
			// attributes
			string attrKey = key + "attributes";
			
			var data = defaults.objectForKey(NSString.Create(attrKey)).To<NSData>();
			if (!NSObject.IsNullOrNil(data))
			{
				NSDictionary attributes = NSUnarchiver.unarchiveObjectWithData(data).To<NSDictionary>();
				ms_attributes[name].addEntriesFromDictionary(attributes);
			}
			
			// name
			ms_attributes[name].setObject_forKey(NSString.Create(name), NSString.Create("style name"));
		}
		
		// We need to apply the temporary attributes even when the text has changed so
		// we need to be careful with the offsets and lengths we use.
		private NSRange DoGetClampedRange(int offset, int length, int textLength)
		{
			length = Math.Min(length, textLength - offset);
			return new NSRange(offset, length);
		}
		
		private void DoUpdateAttributes(string inName, object value)
		{
			if ((bool) value)
			{
				m_storage.beginEditing();
				
				try
				{
					int index = 0;
					var sname = NSString.Create("style name");
					while (index < m_storage.length())
					{
						NSRange range;
						NSDictionary attrs = m_storage.attributesAtIndex_effectiveRange((uint) index, out range);
						if (!NSObject.IsNullOrNil(attrs))
						{
							NSObject name = attrs.objectForKey(sname);
							if (!NSObject.IsNullOrNil(name))
							{
								if (inName == name.description())
									m_storage.setAttributes_range(ms_attributes[inName], range);
							}
						}
						
						index = range.location + range.length;
					}
				}
				finally
				{
					m_storage.endEditing();
				}
			}
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
			
			HighlightLine(m_line.Offset, m_line.Length);
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private NSTextView m_text;
		private NSTextStorage m_storage;
		private NSLayoutManager m_layout;
		private NSScrollView m_scroller;
		private bool m_stopped;
		private int m_origin;
		private TimeSpan m_elapsed;
		private int m_numIterations;
		private int m_editCount;
		private int m_length;
		private int m_editStart = int.MaxValue;
		private List<StyleRun> m_workSet;			// runs that still need to be applied, runs at the end are applied first
		private List<StyleRun> m_currentSet;		// runs the text should be using (and will be using if m_workSet is empty)
		private List<StyleRun> m_appliedSet;
		private StyleRun m_line;
		private StyleRun m_error;
		
		private static ObserverTrampoline ms_tabObserver;
		private static ObserverTrampoline ms_styleObserver;
		private static NSColor ms_selectedLineColor;
		private static NSColor ms_errorColor;
		private static Dictionary<string, NSMutableDictionary> ms_attributes = new Dictionary<string, NSMutableDictionary>();
		private static string[] ms_names = new[]{"text preprocess font changed", "text type font changed", "text member font changed", "text spaces color changed", "text tabs color changed", "text default font changed", "text keyword font changed", "text string font changed", "text number font changed", "text comment font changed", "text other1 font changed", "text other2 font changed"};
		private static NSMutableDictionary ms_paragraphAttrs = NSMutableDictionary.Create().Retain();
		#endregion
	}
}
