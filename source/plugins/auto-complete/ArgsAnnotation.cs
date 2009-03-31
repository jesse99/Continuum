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
					if (range.length == 0 && chars.length() == 1 && chars[0] == ')')
					{
						Close();
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
		private NSAttributedString DoBuildString()
		{
			string rtype = CsHelpers.GetAliasedName(m_member.Type);
			rtype = CsHelpers.TrimNamespace(rtype);
			rtype = CsHelpers.TrimGeneric(rtype);
			string text = rtype + " " + m_member.Text;
			
			var str = NSMutableAttributedString.Create(text);
			
			int i = rtype.Length + m_member.Text.IndexOf('(');
			DoMakeBold(str, rtype.Length + 1, i - (rtype.Length + 1));
			
			for (int j = 0; j < m_member.ArgNames.Length; ++j)
			{
				int k;
				if (j + 1 < m_member.ArgNames.Length)
					k = text.IndexOf(m_member.ArgNames[j] + ',');
				else
					k = text.IndexOf(m_member.ArgNames[j] + ')');
				DoMakeBold(str, k, m_member.ArgNames[j].Length);
			}
			
			return str;
		}
		
		private void DoMakeBold(NSMutableAttributedString str, int index, int length)
		{
			NSRange range = new NSRange(index, length);
			str.addAttribute_value_range(Externs.NSStrokeWidthAttributeName, NSNumber.Create(-5.0f), range);
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private ITextAnnotation m_annotation;
		private Member m_member;
		#endregion
	}
}
