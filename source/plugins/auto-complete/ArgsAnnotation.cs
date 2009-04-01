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
using Shared;
using System;
using System.Diagnostics;

namespace AutoComplete
{
	internal sealed class ArgsAnnotation : IArgsAnnotation
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public bool IsOpen
		{
			get {return m_annotation != null;}
		}
		
		public void Open(ITextAnnotation annotation, Member member)
		{
			if (m_annotation != null && m_annotation.Visible)
				m_annotation.Visible = false;
				
			m_annotation = annotation;
			m_member = member;
			m_currentArg = 0;
			m_annotation.String = DoBuildString();
			
			m_annotation.Visible = true;
		}
		
		public bool HandleKey(NSTextView view, NSEvent evt)
		{
			bool handled = false;
			
			if (m_annotation != null)
			{
				if (evt.keyCode() == Constants.EscapeKey)
				{
					if (IsOpen)
					{
						Close();
						handled = true;
					}
				}
				
				if (!handled)
				{
					NSRange range = view.selectedRange();
					
					NSString chars = evt.characters();
					if (range.length == 0 && chars.length() == 1)
					{
						if (chars[0] == ')')
						{
							if (DoClosesAnchor(range.location))
								Close();
						}
						else if (chars[0] == ',')
						{
							int arg = DoGetArgIndex(range.location);
							if (arg >= 0 && arg != m_currentArg)
							{
								m_currentArg = arg;
								m_annotation.String = DoBuildString();
							}
						}
					}
				}
			}
			
			return handled;
		}
		
		public void Close()
		{
			m_annotation.Visible = false;
			m_annotation = null;
		}
		
		#region Private Methods
		private bool DoClosesAnchor(int insertionPoint)
		{
			bool closes = false;
			
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
			
			return closes;
		}
		
		private int DoGetArgIndex(int insertionPoint)
		{
			int arg = -1;
			
			NSRange anchor = m_annotation.Anchor;
			int delta = insertionPoint - (anchor.location + anchor.length);
			if (delta > 0 && delta < 2048)			// quick sanity check
			{
				var it = m_boss.Get<IText>();
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
			
			return arg;
		}
		
		private NSAttributedString DoBuildString()
		{
			string rtype = CsHelpers.GetAliasedName(m_member.Type);
			rtype = CsHelpers.TrimNamespace(rtype);
			rtype = CsHelpers.TrimGeneric(rtype);
			string text = rtype + " " + m_member.Text;
			
			var str = NSMutableAttributedString.Create(text);
			for (int j = 0; j < m_member.ArgNames.Length; ++j)
			{
				if (j == m_currentArg)
				{
					int k;
					if (j + 1 < m_member.ArgNames.Length)
						k = text.IndexOf(m_member.ArgNames[j] + ',');
					else
						k = text.IndexOf(m_member.ArgNames[j] + ')');
					
					DoHilite(str, k, m_member.ArgNames[j].Length);
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
		
		#region Fields
		private Boss m_boss;								// text editor boss
		private ITextAnnotation m_annotation;
		private Member m_member;
		private int m_currentArg;
		#endregion
	}
}
