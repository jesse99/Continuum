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
using System.Collections.Generic;
using System.Diagnostics;

namespace MakeBuilder
{
	[ExportClass("VariablesController", "NSWindowController", Outlets = "table")]
	internal sealed class VariablesController : NSWindowController
	{
		public VariablesController(List<Variable> variables) : base(NSObject.AllocNative("VariablesController"))
		{		
			m_docVariables = variables;
			m_newVariables = new List<Variable>(variables);

			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("build-variables"), this);	

			m_table = new IBOutlet<NSTableView>(this, "table");
			m_table.Value.setDataSource(this);

			Unused.Value = window().setFrameAutosaveName(NSString.Create("build-variables window"));
			window().makeKeyAndOrderFront(this);

			ActiveObjects.Add(this);
		}
							
		public void envOK(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSOKButton);
			m_table.Value.setDataSource(null);				// need this or the table sometimes calls back into us after we go away (the table isn't sticking around so this seems to be some sort of teardown order issue)
			window().orderOut(this);
			window().release();
			
			m_docVariables.Clear();
			m_docVariables.AddRange(m_newVariables);
		}
	
		public void envCancel(NSObject sender)
		{
			Unused.Value = sender;
			
			NSApplication.sharedApplication().stopModalWithCode(Enums.NSCancelButton);
			window().orderOut(this);
			window().release();
		}
	
		public void restoreDefaults(NSObject sender)
		{
			Unused.Value = sender;
			
			for (int i = 0; i < m_newVariables.Count; ++i)
			{
				Variable old = m_newVariables[i];
				m_newVariables[i] = new Variable(old.Name, old.DefaultValue, old.DefaultValue);
			}
				
			m_table.Value.reloadData();
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			Unused.Value = table;
			
			return m_newVariables.Count;
		}
	
		[Register("tableView:objectValueForTableColumn:row:")]		
		public NSObject GetCell(NSTableView table, NSTableColumn column, int row)
		{
			Unused.Value = table;
			
			Variable variable = m_newVariables[row];
		
			if (column.identifier().ToString() == "1")
				return NSString.Create(variable.Name);
			else 
				if (variable.Value.Length > 0)
					return NSString.Create(variable.Value);
				else
					return NSString.Create(variable.DefaultValue);
		}
	
		[Register("tableView:setObjectValue:forTableColumn:row:")]		
		public void SetCell(NSTableView table, NSObject v, NSTableColumn column, int row)
		{		
			Unused.Value = table;
			
			Variable old = m_newVariables[row];
			
			if ("1" == column.identifier().ToString())
				m_newVariables[row] = new Variable(v.ToString(), old.DefaultValue, old.Value);
	
			else if ("2" == column.identifier().ToString())
				m_newVariables[row] = new Variable(old.Name, old.DefaultValue, v.ToString());
				
			else
				Contract.Assert(false, "how did we get identifier: " + column.identifier());
		}
		
		#region Fields
		private IBOutlet<NSTableView> m_table;
		private List<Variable> m_docVariables;
		private List<Variable> m_newVariables;
		#endregion
	}
}
