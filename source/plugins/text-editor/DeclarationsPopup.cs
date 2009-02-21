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

using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;
using System.Linq;

namespace TextEditor
{
	[ExportClass("DeclarationsPopup", "NSPopUpButton")]
	internal sealed class DeclarationsPopup : NSPopUpButton
	{
		public DeclarationsPopup(IntPtr instance) : base(instance)
		{
			setTarget(this);
			setAction("selectedItemChanged:");
			
			ActiveObjects.Add(this);
		}
		
		public void Init(TextController controller, IDeclarations getter)
		{
			Trace.Assert(controller != null, "controller is null");
			
			m_controller = controller;
			m_getter = getter;
		}
		
		public void textSelectionChanged()
		{
			NSRange selection = m_controller.TextView.selectedRange();
			int offset = selection.location;
			
			// Find the last declaration the selection start intersects.
			int index = -1;
			for (int i = 0; i < m_declarations.Length; ++i)
			{
				if (m_declarations[i].Extent.Intersects(offset))
					index = i;
			}
			
			selectItemAtIndex(index);
		}
		
		public void textWasStyled()
		{
			DoReset();
		}
		
		public void selectedItemChanged(NSObject sender)
		{
			Declaration d = m_declarations[indexOfSelectedItem()];
			
			int begin = d.Extent.location;
			int count = 0;
			while (begin + count < m_controller.Text.Length && m_controller.Text[begin + count] != '\n')
				++count;
				
			m_controller.ShowLine(begin, begin, count);
		}
		
		#region Private Methods
		private void DoReset()
		{
			if (m_getter != null)
			{
				int edit;
				StyleRun[] runs;
				CsGlobalNamespace globals;
				
				var styles = m_controller.Boss.Get<IStyles>();
				styles.Get(out edit, out runs, out globals);
				Trace.Assert(edit == m_controller.EditCount, "controller called us with a bad edit count");
				
				m_declarations = m_getter.Get(m_controller.Text, runs, globals);
				
				string[] names = (from d in m_declarations select d.Name).ToArray();
				
				// NSPopUpButton wants all the menu items to be unique so we'll
				// fix up the names here.
				for (int i = 0; i < names.Length - 1; ++i)
				{
					for (int j = i + 1; j < names.Length; ++j)
					{
						if (names[i] == names[j])
							names[j] += Constants.ZeroWidthSpace;
					}
				}
				
				var items = NSArray.Create(names.ToArray());
				removeAllItems();
				addItemsWithTitles(items);
			}
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private IDeclarations m_getter;
		private Declaration[] m_declarations = new Declaration[0];
		#endregion
	}
}
