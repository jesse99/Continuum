// Copyright (C) 2008 Jesse Jones
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

namespace Find
{
	[ExportClass("FindProgressController", "NSWindowController", Outlets = "bar text")]
	internal sealed class FindProgressController : NSWindowController
	{
		public FindProgressController(IFindProgress find) : base("FindProgressController", "find-progress")
		{					
			m_find = find;
			m_bar = new IBOutlet<NSProgressIndicator>(this, "bar");
			m_text = new IBOutlet<NSTextField>(this, "text");
			
			window().setDelegate(this);
			window().setTitle(NSString.Create(m_find.Title));
			Unused.Value = window().setFrameAutosaveName(NSString.Create("find progress window"));
			window().makeKeyAndOrderFront(null);
			
			ms_controllers.Add(this);
			
			NSApplication.sharedApplication().BeginInvoke(this.DoUpdate, TimeSpan.FromMilliseconds(50));
		}
		
		public void windowWillClose(NSNotification n)
		{
			Unused.Value = n;
			
			m_find.Cancelled = true;
			
			Unused.Value = ms_controllers.Remove(this);
			release();
		}
						
		#region Private Methods -----------------------------------------------
		// TODO: might be nice to have a disclosure widget to show a list of the
		// files changed, ideally with line numbers.
		private void DoUpdate()
		{
			m_bar.Value.setMaxValue(m_find.FileCount);	// this is not known when we start out so we'll just set it each time through

			if (m_find.ProcessedCount >= m_find.FileCount)
			{
				m_bar.Value.setDoubleValue(m_find.FileCount);
				
				if (m_find.ChangeCount > 0)
					m_text.Value.setStringValue(NSString.Create("Processed {0} files and changed {1}.", m_find.FileCount, m_find.ChangeCount));
				else
					m_text.Value.setStringValue(NSString.Create("Processed {0} files. None were changed.", m_find.FileCount));
			}
			else
			{
				m_bar.Value.setDoubleValue(m_find.ProcessedCount);
				m_text.Value.setStringValue(NSString.Create(m_find.Processing));

				NSApplication.sharedApplication().BeginInvoke(this.DoUpdate, TimeSpan.FromSeconds(1));
			}
		}
		#endregion

		#region Fields --------------------------------------------------------
		private IFindProgress m_find;
		private IBOutlet<NSProgressIndicator> m_bar;
		private IBOutlet<NSTextField> m_text;

		private static List<FindProgressController> ms_controllers = new List<FindProgressController>();
		#endregion
	}
}
