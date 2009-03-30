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
	internal sealed class Annotation : IAnnotation
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
		
		public void Open(ITextAnnotation annotation, Func<NSTextView, IAnnotation, NSEvent, bool> keyHandler)
		{
			if (m_annotation != null && m_annotation.Visible)
				m_annotation.Visible = false;
				
			m_annotation = annotation;
			m_keyHandler = keyHandler;
			
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
				
				if (!handled && m_keyHandler != null)
					handled = m_keyHandler(view, this, evt);
			}
			
			return handled;
		}
		
		public void Close()
		{
			m_annotation.Visible = false;
			m_annotation = null;
			m_keyHandler = null;
		}
		
		#region Fields
		private Boss m_boss;
		private ITextAnnotation m_annotation;
		private Func<NSTextView, IAnnotation, NSEvent, bool> m_keyHandler;
		#endregion
	}
}
