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

namespace Debugger
{
	[ExportClass("VariableItem", "NSObject")]
	internal sealed class VariableItem : NSObject
	{
		// owner will be something like a StackFrame, ObjectMirror, StringMirror, etc.
		// item will be something like a LocalVariable, FieldInfoMirror, char, etc.
		public VariableItem(ThreadMirror thread, string name, object owner, object value) : base(NSObject.AllocAndInitInstance("VariableItem"))
		{
			Contract.Requires(!string.IsNullOrEmpty(name));
			Contract.Requires(owner != null);
			Contract.Requires(value != null);					// null debugger values are PrimitiveValues with a null Value
			
			Owner = owner;
			Value = value;
			
			AttributedName = NSAttributedString.Create(name).Retain();
			AttributedType = NSAttributedString.Create(ValueType.Invoke(Owner, Value)).Retain();
			NumberOfChildren = ValueNumChildren.Invoke(Owner, Value);
		}
		
		public NSAttributedString AttributedName {get; private set;}
		public NSAttributedString AttributedType {get; private set;}
		
		public NSAttributedString GetAttributedValue(ThreadMirror thread)
		{
			// Note that if we do this in the ctor the act of invoking a ToString method is enough
			// to render the StackFrame object we're trying to use unuseable which hoses us when
			// we try to get the local variables.
			if (m_attributedValue == null)
				m_attributedValue = NSAttributedString.Create(ValueText.Invoke(thread, Owner, Value)).Retain();
			return m_attributedValue;
		}
		
		public object Owner {get; private set;}
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
				m_children[index] = ValueChild.Invoke(thread, Owner, Value, index);
			
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
			DoRefreshValueText(thread);
			DoRefreshTypeText();
			
			int count = ValueNumChildren.Invoke(Owner, Value);
			if (count == NumberOfChildren)
			{
				if (m_children != null)
				{
					foreach (VariableItem child in m_children)
					{
						if (child != null)
							child.Refresh(thread);
					}
				}
			}
			else
			{
				DoDeleteChildren();
				NumberOfChildren = count;
			}
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			if (AttributedName != null)		// this may be called if a ctor throws so we need all the null checks
				AttributedName.release();
			
			if (AttributedType != null)
				AttributedType.release();
			
			if (m_attributedValue != null)
				m_attributedValue.release();
			
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
				m_children[i] = ValueChild.Invoke(thread, Owner, Value, i);
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
		
		private void DoRefreshValueText(ThreadMirror thread)
		{
			NSAttributedString str;
			
			ObjectMirror obj = Value as ObjectMirror;
			if (obj != null && obj.IsCollected)
			{
				str = DoCreateString("garbage collected", NSColor.disabledControlTextColor());
			}
			else
			{
				const string NotNullText = "\u25BC";		// BLACK DOWN-POINTING TRIANGLE
				
				string newValue = ValueText.Invoke(thread, Owner, Value);
				string oldValue = m_attributedValue.ToString();
				if (newValue != oldValue)
					if (newValue.Length == 0)
						str = DoCreateString(NotNullText, NSColor.redColor());	// make it a bit more obvious that we have gone from "null" to non-null (for types without a custom ToString method)
					else
						str = DoCreateString(newValue, NSColor.redColor());
				else
					str = NSAttributedString.Create(newValue).Retain();
			}
			
			m_attributedValue.release();										// note that we want to reset the text even if it has not changed so that we go from red back to black
			m_attributedValue = str;
		}
		
		private void DoRefreshTypeText()
		{
			NSAttributedString str;
			
			string newType = ValueType.Invoke(Owner, Value);			// the declared type should not change but we show the actual type which may
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
		private NSAttributedString m_attributedValue;
		private VariableItem[] m_children;
		#endregion
	}
}
