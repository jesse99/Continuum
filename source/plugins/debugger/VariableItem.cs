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
	internal abstract class VariableItem : NSObject
	{
		protected VariableItem(string typeName) : base(NSObject.AllocAndInitInstance(typeName))
		{
			m_name = CreateString(string.Empty);
			m_type = CreateString(string.Empty);
			m_value = CreateString(string.Empty);
		}
		
		protected VariableItem(string name, string type, string typeName) : base(NSObject.AllocAndInitInstance(typeName))
		{
			name = DoGetFriendlyName(name);
			
			m_name = CreateString(name);
			m_type = CreateString(type);
			m_value = CreateString(string.Empty);
		}
		
		protected VariableItem(ThreadMirror thread, string name, Value value, string type, string typeName) : base(NSObject.AllocAndInitInstance(typeName))
		{
			name = DoGetFriendlyName(name);
			
			if (DoIsGCed(value))
			{
				m_name = CreateString(NSColor.disabledControlTextColor(), name);
				m_type = CreateString(NSColor.disabledControlTextColor(), type);
				m_value = CreateString(NSColor.disabledControlTextColor(), "garbage collected");
			}
			else
			{
				m_name = CreateString(name);
				m_type = CreateString(type);
				m_value = CreateString(value.Stringify(thread));
			}
		}
		
		// Note that this should be used instead of Count because it will not force
		// all children to be loaded.
		public virtual bool IsExpandable
		{
			get {return false;}
		}
		
		public virtual int Count
		{
			get {return 0;}
		}
		
		public virtual VariableItem this[int index]
		{
			get {Contract.Assert(false); return null;}
		}
		
		public NSAttributedString Name
		{
			get {return m_name;}
		}
		
		public NSAttributedString Value
		{
			get {return m_value;}
		}
		
		public NSAttributedString TypeName
		{
			get {return m_type;}
		}
		
		public virtual VariableItem SetValue(string text)
		{
			Functions.NSBeep();
			
			return this;
		}
		
		public virtual void RefreshValue(ThreadMirror thread, Value value)
		{
			string oldText = m_value.ToString();
			string newText = value.Stringify(thread);
			
			// Note that we always reset the text (so that we can go from red back to black).
			m_value.release();
			
			const string NotNullText = "\u25BC";		// BLACK DOWN-POINTING TRIANGLE
			
			if (DoIsGCed(value))
				m_value = CreateString(NSColor.disabledControlTextColor(), newText);
			else if (newText != oldText)
				if (newText.Length == 0)
					m_value = CreateString(NSColor.redColor(), NotNullText);
				else
				m_value = CreateString(NSColor.redColor(), newText);
			else
				m_value = CreateString(newText);
		}
		
		public VariableItem RefreshVariable(ThreadMirror thread, Value v, Action<Value> setter)
		{
			VariableItem newItem;
			
			var primitive = v as PrimitiveValue;
			if (v == null || (primitive != null && primitive.Value == null))
			{
				if (this is NullValueItem)
				{
					newItem = this;
					newItem.m_value = CreateString("null");
				}
				else
				{
					// non-null to null
					newItem = new NullValueItem(thread, Name.ToString(), TypeName.ToString());
					newItem.m_value = CreateString(NSColor.redColor(), "null");
					this.release();
				}
			}
			else
			{
				if (this is NullValueItem)
				{
					// null to non-null
					newItem = CreateVariable(Name.ToString(), TypeName.ToString(), v, thread, setter);
					string newText = v.Stringify(thread);
					newItem.m_value = CreateString(NSColor.redColor(), newText);
					this.release();
				}
				else
				{
					newItem = this;
					newItem.RefreshValue(thread, v);
				}
			}
			
			return newItem;
		}
		
		#region Protected Methods
		protected VariableItem CreateVariable(string name, string type, TypeMirror v, ThreadMirror thread)
		{
			VariableItem variable = new TypeValueItem(name, type, v, thread);
			
			return variable;
		}
		
		protected VariableItem CreateVariable(string name, string type, Value v, ThreadMirror thread, Action<Value> setter)
		{
			VariableItem variable = null;
			
			do
			{
				if (v == null)		// don't think we normally hit this case
				{
					variable = new NullValueItem(thread, name, type);
					break;
				}
				
				if (v.IsType("System.MulticastDelegate"))
				{
					variable = new MulticastDelegateValueItem(thread, name, type, v);
					break;
				}
				
				if (v.IsType("System.Delegate"))
				{
					variable = new DelegateValueItem(thread, name, type, v);
					break;
				}
				
				if (v.IsType("System.IntPtr") || v.IsType("System.UIntPtr"))
				{
					variable = new IntPtrValueItem(thread, name, type, v);
					break;
				}
				
				var array = v as ArrayMirror;
				if (array != null)
				{
					variable = new ArrayValueItem(name, type, array, thread, setter);
					break;
				}
				
				var enm = v as EnumMirror;
				if (enm != null)
				{
					CodeViewer.CacheAssembly(enm.Type.Assembly);
					variable = new EnumValueItem(thread, name, type, enm, setter);
					break;
				}
				
				var primitive = v as PrimitiveValue;
				if (primitive != null)
				{
					if (primitive.Value == null)
						variable = new NullValueItem(thread, name, type);
					else
						variable = new PrimitiveValueItem(thread, name, type, primitive, setter);
					break;
				}
				
				var str = v as StringMirror;
				if (str != null)
				{
					variable = new StringValueItem(thread, name, type, str, setter);
					break;
				}
				
				// these two have to appear last
				var obj = v as ObjectMirror;
				if (obj != null)
				{
					variable = new ObjectValueItem(name, type, obj, thread, setter);
					break;
				}
				
				var strct = v as StructMirror;
				if (strct != null)
				{
					variable = new StructValueItem(name, type, strct, thread, setter);
					break;
				}
				
				Console.Error.WriteLine("bad type: {0}", v.GetType());
			}
			while (false);
			
			return variable;
		}
		
		protected NSAttributedString CreateString(string text)
		{
			return NSAttributedString.Create(text).Retain();
		}
		
		protected NSAttributedString CreateString(NSColor color, string text)
		{
			var attrs = NSMutableDictionary.Create();
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			return NSAttributedString.Create(text, attrs).Retain();
		}
		
		protected override void OnDealloc()
		{
			if (m_name != null)
			{
				m_name.release();
				m_name = null;
			}
			
			if (m_value != null)
			{
				m_value.release();
				m_value = null;
			}
			
			if (m_type != null)
			{
				m_type.release();
				m_type = null;
			}
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private string DoGetFriendlyName(string name)
		{
			string result = name;
			
			int i = name.IndexOf('<');
			int j = name.IndexOf('>');
			if (i == 0 && i < j)
				result = name.Substring(i + 1, j - i - 1);		// auto-props look like "<Command>k_BackingField"
			
			return result;
		}
		
		private bool DoIsGCed(Value value)
		{
			var obj = value as ObjectMirror;
			bool collected = obj != null && obj.IsCollected;
			
			return collected;
		}
		#endregion
		
		#region Fields
		protected NSAttributedString m_name;
		protected NSAttributedString m_type;
		protected NSAttributedString m_value;
		#endregion
	}
}
