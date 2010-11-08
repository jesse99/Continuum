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

using MCocoa;
using MObjc;
using Shared;
using System;

namespace TextEditor
{
	// Used to restore the view settings (e.g. scroller and selection) to that which
	// was used when the document was last closed or to select (and show) a new
	// range after the document is opened. Note that this has to be done after the
	// text is laid out so that restoring the selection or calling scrollRangeToVisible
	// works correctly.
	internal sealed class RestoreViewState
	{
		public RestoreViewState(TextController controller)
		{
			m_controller = controller;
			ActiveObjects.Add(this);
		}
		
		public void SetPath(string path)
		{
			if (!string.IsNullOrEmpty(path))
			{
				bool wrap = false;
				if (WindowDatabase.GetViewSettings(path, ref m_length, ref m_origin, ref m_selected, ref wrap))
				{
					if (wrap)
						m_controller.toggleWordWrap(null);
				}
			}
		}
		
		public void ShowLine(int begin, int end, int count)
		{
			m_length = -1;
			m_origin = NSPoint.Zero;
			m_selected = new NSRange(begin, 0);
			m_visible = new NSRange(begin, end - begin);
			m_deferred = new NSRange(begin, count);
		}
		
		public void ShowSelection(NSRange range)
		{
			m_length = -1;
			m_origin = NSPoint.Zero;
			m_selected = range;
			m_visible = range;
			m_deferred = range;
		}
		
		public bool OnCompletedLayout(NSLayoutManager layout, bool atEnd)
		{
			bool finished = false;
			
			if (atEnd || (m_origin == NSPoint.Zero && layout.firstUnlaidCharacterIndex() > m_selected.location + 100))
			{
				if (m_length == -1 || m_length == m_controller.Text.Length)	// only restore the view if it has not changed since we last had it open
				{
					if (m_origin != NSPoint.Zero)
					{
						var clip = m_controller.ScrollView.contentView().To<NSClipView>();
						clip.scrollToPoint(m_origin);
						m_controller.ScrollView.reflectScrolledClipView(clip);
						
						if (m_selected != NSRange.Empty)
							m_controller.TextView.setSelectedRange(m_selected);
					}
					else if (m_selected != NSRange.Empty && m_selected.location + m_selected.length <= m_controller.TextView.textStorage().string_().length())
					{
						m_controller.TextView.setSelectedRange(m_selected);
						m_controller.TextView.scrollRangeToVisible(m_visible);
						
						m_controller.TextView.showFindIndicatorForRange(m_deferred);
					}
				}
				
				finished = true;
			}
			
			return finished;
		}
		
		#region Fields
		private TextController m_controller;
		private int m_length = -1;
		private NSPoint m_origin;
		private NSRange m_selected;
		private NSRange m_visible;
		private NSRange m_deferred;
		#endregion
	}
}
