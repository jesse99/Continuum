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
	[ExportClass("DeclarationsPopup", "NSPopUpButton")]
	internal sealed class DeclarationsPopup : NSPopUpButton, IObserver
	{
		public DeclarationsPopup(IntPtr instance) : base(instance)
		{
			setTarget(this);
			setAction("selectedItemChanged:");
			setAutoenablesItems(false);
			
			Broadcaster.Register("computed declarations", this);
			
			ActiveObjects.Add(this);
		}
		
		public void Init(TextController controller)
		{
			Contract.Requires(controller != null, "controller is null");
			
			m_controller = controller;
			DoReset();
		}
		
		public void Stop()
		{
			Broadcaster.Unregister(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "computed declarations":
					DoUpdate((Declarations) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public new void mouseDown(NSEvent e)
		{
			if ((e.modifierFlags_i() & Enums.NSAlternateKeyMask) != 0)
			{
				DoBuildUsingNamesOrder();
				SuperCall(NSPopUpButton.Class, "mouseDown:", e);
				DoBuildUsingOffsetsOrder();
			}
			else
				SuperCall(NSPopUpButton.Class, "mouseDown:", e);
		}
		
		public void textSelectionChanged()
		{
			NSRange selection = m_controller.TextView.selectedRange();
			int offset = selection.location;
		
			// Find the last declaration the selection start intersects.
			int index = -1;
			for (int i = 0; i < m_declarations.Decs.Length; ++i)
			{
				if (m_declarations.Decs[i].Extent.Intersects(offset))
					index = i;
			}
			
			selectItemAtIndex(index);
		}
		
		public void selectedItemChanged(NSObject sender)
		{
			Declaration d = m_indexTable[indexOfSelectedItem()];
			
			int begin = d.Extent.location;
			int count = 0;
			while (begin + count < m_controller.Text.Length && m_controller.Text[begin + count] != '\n')
				++count;
				
			m_controller.ShowLine(begin, begin, count);
		}
		
		#region Private Methods
		private void DoReset()
		{
			m_declarations = new Declarations();
			m_indexTable = new Dictionary<int, Declaration>();
			
			removeAllItems();
		}
		
		private void DoUpdate(Declarations decs)
		{
			if (m_controller.Path != null && Paths.AreEqual(decs.Path, m_controller.Path))
			{
				bool changed = decs != m_declarations;
				
				// Note that we still need to save the new info because it has
				// updated offsets.
				m_declarations = decs;
				
				if (changed)
				{
					DoBuildUsingOffsetsOrder();
					textSelectionChanged();
				}
			}
		}
		
		private void DoBuildUsingOffsetsOrder()
		{
			DoBuild(m_declarations.Decs);
		}
		
		private void DoBuild(IList<Declaration> declarations)
		{
			string[] names = (from d in declarations select d.Name).ToArray();
			
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
			
			removeAllItems();
			m_indexTable.Clear();
			
			for (int i = 0; i < declarations.Count; ++i)
			{
				var dict = NSMutableDictionary.Create();
				
				NSFont font = NSFont.systemFontOfSize(NSFont.smallSystemFontSize());
				dict.setObject_forKey(font, Externs.NSFontAttributeName);
				
				if (declarations[i].IsType)
					dict.setObject_forKey(NSNumber.Create(-4.0f), Externs.NSStrokeWidthAttributeName);
				
				addItemWithTitle(NSString.Empty);
				NSMenuItem item = itemAtIndex(i);
				item.setAttributedTitle(NSAttributedString.Create(names[i], dict));
				
				if (declarations[i].IsDirective)
					item.setEnabled(false);
				
				m_indexTable.Add(i, declarations[i]);
			}
		}
		
		[Pure]
		private int DoCountSpaces(string s)
		{
			int count = 0;
			
			for (int i = 0; i < s.Length && s[i] == ' '; ++i)
				++count;
				
			return count;
		}
		
		private void DoBuildUsingNamesOrder()
		{
			var items = (from d in m_declarations.Decs where !d.IsDirective select new QualifiedName(d.Name, d)).ToList();
			
			// Sort the items by building a full name consisting of whatever it is declared under
			// plus the item name.
			for (int i = 0; i < items.Count - 1; ++i)
			{
				Contract.Assert(DoCountSpaces(items[i].Name) == 0, items[i].Name + " is indented");
				
				int numSpaces = DoCountSpaces(items[i + 1].Name);
				for (int j = i + 1; j < items.Count && items[j].Name.Length > 0 && items[j].Name[0] == ' '; ++j)
				{
					items[j].Prefix += items[i].Name + ".";
					items[j].Name = items[j].Name.Substring(numSpaces);
				}
			}
			
			items.Sort((lhs, rhs) =>lhs.FullName.CompareTo(rhs.FullName));
			var declarations = (from d in items select d.Declaration).ToArray();
			
			DoBuild(declarations);
		}
		#endregion
		
		#region Private Types
		private sealed class QualifiedName
		{
			public QualifiedName(string name, Declaration d)
			{
				Prefix = string.Empty;
				Name = name;
				Declaration = d;
			}
			
			public string Prefix {get; set;}
			public string Name {get; set;}
			public string FullName {get {return Prefix + Name;}}
			public Declaration Declaration {get; private set;}
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private Declarations m_declarations = new Declarations();
		private Dictionary<int, Declaration> m_indexTable = new Dictionary<int, Declaration>();
		#endregion
	}
}
