// Copyright (C) 2010 Jesse Jones
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
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;

using Debug = Debugger;

namespace Debugger
{
	[ExportClass("VariableItem", "NSObject")]
	internal sealed class VariableItem : NSObject
	{
		// These form a tree representing all of the values the user is currently examining
		// in the variables window. Note that we try to preserve the objects that form the
		// tree to prevent the outline view from closing open items. 
		public VariableItem(ThreadMirror thread, string name, object hint, object value, int index) : base(NSObject.AllocAndInitInstance("VariableItem"))
		{
			Contract.Requires(!string.IsNullOrEmpty(name));
			Contract.Requires(value != null);					// null debugger values are PrimitiveValues with a null Value
			Contract.Requires(index >= 0);
			
			Value = value;
			AttributedName = NSMutableAttributedString.Create(name).Retain();
			m_index = index;
			m_hint = hint;
			
			Item item = GetItem.Invoke(thread, hint, Value);
			AttributedType = NSAttributedString.Create(item.Type).Retain();
			AttributedValue = NSAttributedString.Create(item.Text).Retain();
			NumberOfChildren = item.Count;
		}
		
		public NSMutableAttributedString AttributedName {get; private set;}
		public NSAttributedString AttributedType {get; private set;}
		public NSAttributedString AttributedValue {get; private set;}
		
		public object Value {get; private set;}
		
		public int NumberOfChildren {get; private set;}
		
		public VariableItem GetChild(ThreadMirror thread, int index)
		{
			Contract.Requires(index >= 0);
			Contract.Requires(index < NumberOfChildren);
			
			if (m_children == null)
			{
				m_children = new VariableItem[NumberOfChildren];
				
				if (DoShouldEagerlyLoad())
					DoEagerLoad(thread);
			}
			
			if (m_children[index] == null)
				m_children[index] = Debug::GetChild.Invoke(thread, Value, index);
			
			return m_children[index];
		}
		
		public void RefreshValue(ThreadMirror thread, object value)
		{
			Contract.Requires(value != null);
			
			Value = value;
			Refresh(thread);
		}
		
		// Update the value and recursively all the children to reflect state changes in the debugee.
		public void Refresh(ThreadMirror thread)
		{
			Item item = GetItem.Invoke(thread, m_hint, Value);
			DoRefreshValueText(item.Text);
			DoRefreshTypeText(item.Type);
			
			if (item.Count == NumberOfChildren)
			{
				if (m_children != null)
				{
					for (int i = 0; i < item.Count; ++i)
					{
						VariableItem child = m_children[i];
						
						if (child != null)
						{
							VariableItem newChild = Debug::GetChild.Invoke(thread, Value, child.m_index);
							Contract.Assert(child.AttributedName.ToString() == newChild.AttributedName.ToString(), string.Format("oldName: {0}, newName: {1}", child.AttributedName.ToString(), newChild.AttributedName.ToString()));
							
							child.RefreshValue(thread, newChild.Value);
						}
					}
				}
			}
			else
			{
				DoDeleteChildren();
				NumberOfChildren = item.Count;
			}
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			if (AttributedName != null)		// this may be called if a ctor throws so we need all the null checks
				AttributedName.release();
			
			if (AttributedType != null)
				AttributedType.release();
			
			if (AttributedValue != null)
				AttributedValue.release();
			
			DoDeleteChildren();
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		// There are a couple of reasons why we want to eagerly create children: for most
		// types we want to sort the children by name and we need them all to do that.
		// In addition, StackFrames don't remain useable for very long so we need to get
		// all of their children while we can.
		//
		// Conversely collection classes may be very large so we only want to load the 
		// children that we must (NSOutlineView will be smart and only ask for the children
		// that need to be rendered).
		private bool DoShouldEagerlyLoad()
		{
			bool lazy = Value is ArrayMirror || Value is StringMirror;
			
			return !lazy;
		}
		
		private void DoEagerLoad(ThreadMirror thread)
		{
			for (int i = 0; i < NumberOfChildren; ++i)
			{
				m_children[i] = Debug::GetChild.Invoke(thread, Value, i);
			}
			
			Array.Sort(m_children, (lhs, rhs) => lhs.AttributedName.ToString().CompareTo(rhs.AttributedName.ToString()));
		}
		
		private void DoDeleteChildren()
		{
			if (m_children != null)
			{
				foreach (VariableItem child in m_children)
				{
					if (child != null)
						child.release();
				}
				m_children = null;
			}
		}
		
		private void DoRefreshValueText(string newValue)
		{
			NSAttributedString str;
			NSColor nameColor = NSColor.blackColor();
			
			ObjectMirror obj = Value as ObjectMirror;
			if (obj != null && obj.IsCollected)
			{
				str = DoCreateString("garbage collected", NSColor.disabledControlTextColor());
				nameColor = NSColor.disabledControlTextColor();
			}
			else
			{
				string oldValue = AttributedValue.ToString();
				if (newValue != oldValue)
				{
					if (newValue.Length == 0)
					{
						str = DoCreateString(string.Empty, NSColor.redColor());
						nameColor = NSColor.redColor();						// we've gone from having value text to not having it (probably because the value went from null to non-null), so make it more obvious that it has changed
					}
					else
					{
						str = DoCreateString(newValue, NSColor.redColor());
					}
				}
				else
				{
					str = NSAttributedString.Create(newValue).Retain();
				}
			}
			
			AttributedValue.release();										// note that we want to reset the text even if it has not changed so that we go from red back to black
			AttributedValue = str;
			
			NSRange range = new NSRange(0, (int) AttributedName.length());
			AttributedName.addAttribute_value_range(Externs.NSForegroundColorAttributeName, nameColor, range);
		}
		
		// The declared type should not change but we show the actual type which may.
		private void DoRefreshTypeText(string newType)
		{
			NSAttributedString str;
			
			string oldType = AttributedType.ToString();
			if (newType != oldType)
				if (newType.Length > 0)
					str = DoCreateString(newType, NSColor.redColor());
				else
					str = NSAttributedString.Create(oldType).Retain();	// this will happen if the value goes to null
			else
				str = NSAttributedString.Create(newType).Retain();
			
			AttributedType.release();
			AttributedType = str;
		}
		
		private NSAttributedString DoCreateString(string newValue, NSColor color)
		{
			var attrs = NSMutableDictionary.Create();
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			NSAttributedString result = NSAttributedString.Create(newValue, attrs).Retain();
			
			return result;
		}
		#endregion
		
		#region Fields
		private int m_index;
		private object m_hint;				// used to get the declared type of a value
		private VariableItem[] m_children;
		#endregion
	}
}
