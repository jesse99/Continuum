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

using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoComplete
{
	internal sealed class ArgsAnnotation : IArgsAnnotation, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			Broadcaster.Register("args color changed", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public bool IsValid
		{
			get {return m_annotation != null && m_annotation.IsValid;}
		}
		
		public bool IsVisible
		{
			get {return m_annotation != null && m_annotation.Visible;}
		}
		
		public void Open(ITextAnnotation annotation, Member[] members, int index)
		{
			Trace.Assert(annotation != null, "annotation is null");
			Trace.Assert(members != null, "members is null");
			Trace.Assert(index >= 0, "index is negative");
			Trace.Assert(index < members.Length, "index is too large");
			
			if (IsVisible)
			{
				m_oldStates.Add(new State(m_annotation, m_members, m_index));
				m_annotation.SetContext(null);
				m_annotation.Visible = false;
			}
			
			m_annotation = annotation;
			m_members = members;
			m_index = index;
			m_currentArg = 0;
			m_annotation.String = DoBuildString();
			DoUpdateBackColor();
			
			if (members.Length > 1)
				DoResetContextMenu();
			
			m_annotation.Visible = true;
		}
		
		public bool HandleKey(NSTextView view, NSEvent evt)
		{
			bool handled = false;
			
			if (IsValid && evt.keyCode() == Constants.EscapeKey)
			{
				Close();
				handled = true;
			}
			
			if (!handled)
			{
				NSRange range = view.selectedRange();
				
				NSString chars = evt.characters();
				if (range.length == 0 && chars.length() == 1)
				{
					if (IsValid && chars[0] == ')')
					{
						if (DoClosesAnchor(range.location))
							Close();
					}
					else if (chars[0] == ',')
					{
						int arg = -1;
						
						if (IsValid)
						{
							arg = DoGetArgIndex(m_boss, m_annotation, range.location);
						}
						else if (m_oldStates.Count > 0)
						{
							arg = DoGetArgIndex(m_boss, m_oldStates.Last().Annotation, range.location);
							if (arg >= 0)
							{
								m_annotation = m_oldStates.Last().Annotation;
								m_members = m_oldStates.Last().Members;
								m_index = m_oldStates.Last().Index;
								m_currentArg = -1;
								m_annotation.Visible = true;
								m_oldStates.RemoveLast();
							}
							else
								m_oldStates.Clear();		// states aren't synched up with what is being edited so clear them all
						}
						
						if (arg >= 0 && arg != m_currentArg)
						{
							m_currentArg = arg;
							m_annotation.String = DoBuildString();
						}
					}
				}
			}
			
			return handled;
		}
		
		public void Close()
		{
			Broadcaster.Unregister(this);
			
			m_annotation.Close();
			m_annotation = null;
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "args color changed":
					DoUpdateBackColor();
					break;
					
				default:
					Trace.Fail("bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoResetContextMenu()
		{
			var items = new List<AnnontationContextItem>();
			for (int i = 0; i < m_members.Length; ++i)
			{
				string text = m_members[i].Type + " " + m_members[i].Text;
				int state = i == m_index ? 1 : 0;
				
				int j = i;				// need this because we don't want the delegate using the mutated iteration variable
				var item = new AnnontationContextItem(text, state, () => DoUseOverload(j));
				items.Add(item);
			}
			m_annotation.SetContext(items);
		}
		
		private void DoUseOverload(int index)
		{
			m_index = index;
			m_annotation.String = DoBuildString();
			DoResetContextMenu();
		}
		
		private void DoUpdateBackColor()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create("args color")).To<NSData>();
			var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
			
			if (m_annotation != null)
				m_annotation.BackColor = color;
		}
		
		private bool DoClosesAnchor(int insertionPoint)
		{
			bool closes = false;
			
			if (m_annotation.IsValid)
			{
				NSRange anchor = m_annotation.Anchor;
				int delta = insertionPoint - (anchor.location + anchor.length);
				if (delta > 0 && delta < 2048)			// quick sanity check
				{
					var it = m_boss.Get<IText>();
					string text = it.Text;
					Trace.Assert(insertionPoint < text.Length, "insertionPoint is too large");
					
					int start = text.IndexOf('(', anchor.location, anchor.length);
					Trace.Assert(start >= 0, "couldn't find '(' in the anchor: " + text.Substring(anchor.location, anchor.length));
			
					int i = start + 1;
					string[] braces = new string[]{"()", "[]", "{}"};
					while (i < insertionPoint)
					{
						if (Array.Exists(braces, b => text[i] == b[0]))
						{
							i = TextHelpers.SkipBraces(text, i, insertionPoint, braces);
							if (i == insertionPoint)
								return false;
						}
						else
						{
							++i;
						}
					}
					closes = true;
				}
			}
			
			return closes;
		}
		
		private static int DoGetArgIndex(Boss boss, ITextAnnotation annotation, int insertionPoint)
		{
			int arg = -1;
			
			if (annotation.IsValid)
			{
				NSRange anchor = annotation.Anchor;
				int delta = insertionPoint - (anchor.location + anchor.length);
				if (delta > 0 && delta < 2048)			// quick sanity check
				{
					var it = boss.Get<IText>();
					string text = it.Text;
					Trace.Assert(insertionPoint < text.Length, "insertionPoint is too large");
					
					int start = text.IndexOf('(', anchor.location, anchor.length);
					Trace.Assert(start >= 0, "couldn't find '(' in the anchor: " + text.Substring(anchor.location, anchor.length));
			
					arg = 1;						// we start at 1 because the user has typed a comma (tho it will not show up yet)
					int i = start + 1;
					string[] braces = new string[]{"()", "[]", "{}"};
					while (i < insertionPoint)
					{
						if (Array.Exists(braces, b => text[i] == b[0]))
						{
							i = TextHelpers.SkipBraces(text, i, insertionPoint, braces);
							if (i == insertionPoint)
								return -1;
						}
						else if (text[i] == '<')
						{
							int k = TextHelpers.SkipBraces(text, i, insertionPoint, "<>");
							if (k < insertionPoint || text[k] == '>')
								i = k;
							else
								++i;							// presumably the < was a less than instead of a generic brace
						}
						else
						{
							if (text[i] == ',')
								++arg;
							++i;
						}
					}
				}
			}
			
			return arg;
		}
		
		private NSAttributedString DoBuildString()
		{
			Member member = m_members[m_index];
			
			string rtype = CsHelpers.GetAliasedName(member.Type);
			rtype = CsHelpers.TrimNamespace(rtype);
			rtype = CsHelpers.TrimGeneric(rtype);
			string text = rtype + " " + member.Text;
			
			var str = NSMutableAttributedString.Create(text);
			for (int j = 0; j < member.ArgNames.Length; ++j)
			{
				if (j == m_currentArg)
				{
					char delimiter;
					if (j + 1 < member.ArgNames.Length)
						delimiter = ',';
					else
						delimiter = ')';
						
					int k = text.IndexOf(member.ArgNames[j] + delimiter);
					Trace.Assert(k >= 0, string.Format("couldn't find '{0}' in '{1}'", delimiter, text));
					Trace.Assert(member.ArgNames[j].Length > 0, "arg is empty");
					Trace.Assert(k + member.ArgNames[j].Length <= text.Length, "arg is too long");
					
					DoHilite(str, k, member.ArgNames[j].Length);
				}
			}
			
			return str;
		}
		
		// Not sure if it would be better to use but NSForegroundColorAttributeName
		// isn't working for some reason.
		private void DoHilite(NSMutableAttributedString str, int index, int length)
		{
			NSRange range = new NSRange(index, length);
			str.addAttribute_value_range(Externs.NSStrokeWidthAttributeName, NSNumber.Create(-4.0f), range);
		}
		#endregion
		
		#region Private Types
		private sealed class State
		{
			public State(ITextAnnotation annotation, Member[] members, int index)
			{
				Annotation = annotation;
				Members = members;
				Index = index;
			}
			
			public ITextAnnotation Annotation {get; private set;}
			public Member[] Members {get; private set;}
			public int Index {get; private set;}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;								// text editor boss
		private ITextAnnotation m_annotation;
		private Member[] m_members;
		private int m_index;
		private int m_currentArg;
		private List<State> m_oldStates = new List<State>();
		#endregion
	}
}
