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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TextEditor
{
	// Stores StyleRuns and applies them to a text view.
	internal sealed class CurrentStyles : IObserver
	{
		static CurrentStyles()
		{
			ms_styleObserver = new ObserverTrampoline(CurrentStyles.DoResetFont);
			foreach (string name in ms_names)
			{
				Broadcaster.Register(name + "-pre", ms_styleObserver);
				ms_attributes.Add(name, NSMutableDictionary.Create().Retain());
				DoResetFont(name + "-pre", null);
			}
			
			// This should include everything which might be applied from a style run.
			var attrs = NSMutableDictionary.Create();
			attrs.setObject_forKey(NSFont.fontWithName_size(NSString.Create("Helvetica"), 12.0f), Externs.NSFontAttributeName);
			attrs.setObject_forKey(NSColor.blackColor(), Externs.NSForegroundColorAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0), Externs.NSUnderlineStyleAttributeName);
			attrs.setObject_forKey(NSNumber.Create(1), Externs.NSLigatureAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0.0f), Externs.NSBaselineOffsetAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0), Externs.NSStrokeWidthAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0), Externs.NSStrikethroughStyleAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0), Externs.NSObliquenessAttributeName);
			attrs.setObject_forKey(NSNumber.Create(0.0f), Externs.NSExpansionAttributeName);
			ms_defaultAttrs = attrs.Retain();
			
			ms_nullAttributes = new NSString[]		// we can't add null values to NSDictionary so we use this supplemental collection
			{
				Externs.NSBackgroundColorAttributeName,
				Externs.NSKernAttributeName,
				Externs.NSStrokeColorAttributeName,
				Externs.NSUnderlineColorAttributeName,
				Externs.NSStrikethroughColorAttributeName,
				Externs.NSShadowAttributeName,
			};
			
//			Log.WriteLine("XXX", "default: {0:D}", ms_attributes["text default font changed"]);
//			Log.WriteLine("XXX", "keyword: {0:D}", ms_attributes["text keyword font changed"]);
//			Log.WriteLine("XXX", "spaces: {0:D}", ms_attributes["text spaces color changed"]);
		}
		
		public CurrentStyles(TextController controller, NSTextStorage storage)
		{
			m_controller = controller;
			m_storage = storage;
			
			foreach (string name in ms_names)
			{
				Broadcaster.Register(name, this);
			}
			
			ActiveObjects.Add(this);
		}
		
		public int Edit
		{
			get {return m_edit;}
		}
		
		public bool Applied
			{
				get {return m_applied;}
			}
		
		public void Stop()
		{
			m_stopped = true;
		}
		
		// We need to remember where the first edit happened in case the user pastes
		// text to the start which is identical to what was previously there (e.g. copy
		// pasting the first using directive line).
		public void EditedRange(NSRange range)
		{
			m_editStart = Math.Min(range.location, m_editStart);
		}
		
		public void Reset()
		{
			m_currentRuns.Clear();
		}
		
		// Note that we don't apply the runs at the start which match runs we have already
		// applied. This speeds things up a fair amount and also prevents the text from
		// jumping around when lines have different heights (the text view has to redo
		// formatting for every line we touch). It might also be able to skip matching runs
		// at the end, but that is harder because the offsets won't line up.
		public void Reset(int edit, StyleRun[] runs)
		{
			m_edit = edit;
			
			int matchCount = 0;
			int max = Math.Min(m_appliedRuns.Count, runs.Length);
			for (int i = 0; i < max && m_appliedRuns[i] == runs[i] && m_appliedRuns[i].Offset < m_editStart; ++i)
			{
				++matchCount;
			}
			Log.WriteLine(TraceLevel.Verbose, "Styler", "skipping {0} runs at the start", matchCount);
			
			if (matchCount < m_appliedRuns.Count)
				m_appliedRuns.RemoveRange(matchCount, m_appliedRuns.Count - matchCount);
			
			m_currentRuns = new List<StyleRun>(runs.Length - matchCount);
			for (int i = matchCount; i < runs.Length; ++i)
				m_currentRuns.Add(runs[i]);
			Log.WriteLine(TraceLevel.Verbose, "Styler", "queuing {0} runs", m_currentRuns.Count);
			
			m_editStart = int.MaxValue;
		}
		
		public void ApplyDefault()
		{
			if (m_currentRuns.Count > 0 && !m_stopped)		// no point in resetting styles if ApplyStyles isn't going to fix them up
			{
				m_storage.beginEditing();
				try
				{
					int start = 0;
					if (m_appliedRuns.Count > 0)
						start = m_appliedRuns.Last().Offset + m_appliedRuns.Last().Length;
						
					NSRange range = new NSRange(start, (int) m_storage.length() - start);
					
					// Set all relevant attributes to their defaults.
					var attrs = NSMutableDictionary.Create();
					attrs.addEntriesFromDictionary(ms_defaultAttrs);
					
					foreach (NSString name in ms_nullAttributes)
					{
						m_storage.removeAttribute_range(name, range);
					}
					
					// Override the defaults with the defaults the user has chosen.
					attrs.addEntriesFromDictionary(ms_attributes["text default font changed"]);
					
					// Reset the attributes to the defaults (which will later be reset using the style runs).
					m_storage.addAttributes_range(attrs, range);
				}
				finally
				{
					m_storage.endEditing();
				}
			}
		}
		
		public void ApplyStyles()
		{
			if (!m_queued)
			{
				DoApplyStyles();
			}
		}
		
		public void OnBroadcast(string name, object value)
		{
			if (!m_stopped)
				DoUpdateAttributes(name, value);
		}
		
		// Attributes applied to zero-width characters don't show up so if something
		// like a control character is causing an error we need to grow the range to
		// the left until we find a non-zero lenth character so the error hiliting at
		// least shows up near the error.
		internal static NSRange AdjustRangeForZeroWidthChars(string text, NSRange range)
		{
			int loc = range.location;
			int len = range.length;
			
			if (loc > 0 && CharHelpers.IsZeroWidth(text[loc]))
			{
				do
				{
					--loc;
					++len;
				}
				while (loc > 0 && (CharHelpers.IsZeroWidth(text[loc]) || text[loc] == '\t'));
			}
			
			return new NSRange(loc, len);
		}
		
		#region Private Methods
		private static readonly TimeSpan ApplyDelay = TimeSpan.FromMilliseconds(100);
		
		public void DoApplyStyles()
		{
			const int MaxApplies = 200;
			
			m_queued = false;
			if (m_edit == m_controller.EditCount)
			{
				m_storage.beginEditing();
				try
				{
					// Apply the next set of runs.
					int length = (int) m_storage.length();
					int count = Math.Min(m_currentRuns.Count, MaxApplies);
					for (int i = 0; i < count; ++i)
					{
						if (i > 0)				// note that we can't use a short circuited or expression in the contract because of the Format call
							Contract.Assert(m_currentRuns[i - 1].Offset <= m_currentRuns[i].Offset,
								string.Format("runs are not sorted: {0} and {1}", m_currentRuns[i - 1], m_currentRuns[i]));
						DoApplyStyle(m_currentRuns[i], length);
					}
					
					// Move the ones we applied over to m_appliedRuns.
					m_appliedRuns.Capacity = m_appliedRuns.Count + count;
					for (int i = 0; i < count; ++i)
						m_appliedRuns.Add(m_currentRuns[i]);
						
					if (count > 0)
						m_currentRuns.RemoveRange(0, count);
					
					// If there are more runs left to process then queue up another
					// chunk.
					if (m_currentRuns.Count > 0)
					{
						m_queued = true;
						NSApplication.sharedApplication().BeginInvoke(this.DoApplyStyles, ApplyDelay);
					}
					else
//						NSApplication.sharedApplication().BeginInvoke(() => {m_applied = true;});
						m_applied = true;
				}
				finally
				{
					m_storage.endEditing();
				}
			}
			else
			{
				m_currentRuns.Clear();
			}
		}
		
		private void DoApplyStyle(StyleRun run, int length)
		{
			NSRange range = new NSRange(run.Offset, run.Length);
			if (run.Offset + run.Length > length)	// this may happen if the text is edited while we are applying runs
				return;
			if (run.Length == 0)
				return;
			
			switch (run.Type)
			{
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
					range = AdjustRangeForZeroWidthChars(m_controller.Text, range);
					m_storage.addAttribute_value_range(Externs.NSUnderlineStyleAttributeName, NSNumber.Create(2), range);
					m_storage.addAttribute_value_range(Externs.NSUnderlineColorAttributeName, ms_errorColor, range);
					break;
				
				default:
					Contract.Assert(false, "Bad style type: " + run.Type);
					break;
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
		#endregion
		
		#region Fields
		private TextController m_controller;
		private NSTextStorage m_storage;
		private int m_edit = -1;
		private List<StyleRun> m_appliedRuns = new List<StyleRun>();
		private List<StyleRun> m_currentRuns = new List<StyleRun>();
		private bool m_queued;
		private int m_editStart = int.MaxValue;
		private bool m_stopped;
			private bool m_applied;
		
		private static ObserverTrampoline ms_styleObserver;
		private static NSColor ms_errorColor = NSColor.redColor().Retain();
		private static Dictionary<string, NSMutableDictionary> ms_attributes = new Dictionary<string, NSMutableDictionary>();
		private static string[] ms_names = new[]{"text preprocess font changed", "text type font changed", "text member font changed", "text spaces color changed", "text tabs color changed", "text default font changed", "text keyword font changed", "text string font changed", "text number font changed", "text comment font changed", "text other1 font changed", "text other2 font changed"};
		private static NSDictionary ms_defaultAttrs;
		private static NSString[] ms_nullAttributes;
		#endregion
	}
}
