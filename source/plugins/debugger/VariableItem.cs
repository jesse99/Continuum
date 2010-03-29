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
using Mono.Debugger;
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
			m_name = CreateString(name);
			m_type = CreateString(type);
			m_value = CreateString(string.Empty);
		}
		
		protected VariableItem(ThreadMirror thread, string name, Value value, string type, string typeName) : base(NSObject.AllocAndInitInstance(typeName))
		{
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
				m_value = CreateString(DoGetValueText(thread, value));
			}
		}
		
		// Note that this should be used instead of Count because it will not force
		// all children to be loaded.
		public abstract bool IsExpandable {get;}
		
		public abstract int Count {get;}
		
		public abstract VariableItem this[int index] {get;}
		
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
		
		public virtual void RefreshValue(ThreadMirror thread, Value value)
		{
			string oldText = m_value.ToString();
			string newText = DoGetValueText(thread, value);
			
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
		
		#region Protected Methods
		protected VariableItem CreateVariable(string name, string type, TypeMirror v, ThreadMirror thread)
		{
			VariableItem variable = new TypeValueItem(name, type, v, thread);
			
			return variable;
		}
		
		protected VariableItem CreateVariable(string name, string type, Value v, ThreadMirror thread)
		{
			VariableItem variable = null;
			
			do
			{
				var array = v as ArrayMirror;
				if (array != null)
				{
					variable = new ArrayValueItem(name, type, array, thread);
					break;
				}
				
				var enm = v as EnumMirror;
				if (enm != null)
				{
					variable = new EnumValueItem(thread, name, type, enm);
					break;
				}
				
				var primitive = v as PrimitiveValue;
				if (primitive != null)
				{
					variable = new PrimitiveValueItem(thread, name, type, primitive);
					break;
				}
				
				var str = v as StringMirror;
				if (str != null)
				{
					variable = new StringValueItem(thread, name, type, str);
					break;
				}
				
				// these two have to appear last
				var obj = v as ObjectMirror;
				if (obj != null)
				{
					variable = new ObjectValueItem(name, type, obj, thread);
					break;
				}
				
				var strct = v as StructMirror;
				if (strct != null)
				{
					variable = new StructValueItem(name, type, strct, thread);
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
		private string DoGetValueText(ThreadMirror thread, Value value)
		{
			string text = string.Empty;
			
			do
			{
				if (value == null)
				{
					text = "null";
					break;
				}
				
				// these two have to appear first
				var obj = value as ObjectMirror;
				if (obj != null)
				{
					if (obj.IsCollected)
					{
						text = "garbage collected";
					}
					else if (!(value is StringMirror) && !obj.Type.IsArray)
					{
						MethodMirror method = obj.Type.FindMethod("ToString", 0);
						if (method.DeclaringType.FullName != "System.Object")
						{
							Value v = obj.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
							StringMirror s = (StringMirror) v;
							text = s.Value;
						}
					}
					if (text.Length > 0)
						break;
				}
				
				var strct = value as StructMirror;
				if (strct != null)
				{
					MethodMirror method = strct.Type.FindMethod("ToString", 0);
					if (method.DeclaringType.FullName != "System.ValueType" && method.DeclaringType.FullName != "System.Enum")
					{
						Value v = strct.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
						StringMirror s = (StringMirror) v;
						text = s.Value;
					}
					if (text.Length > 0)
						break;
				}
				
				var enm = value as EnumMirror;
				if (enm != null)
				{
					text = enm.StringValue;
					break;
				}
				
				var primitive = value as PrimitiveValue;
				if (primitive != null)
				{
					if (primitive.Value != null)
						text = primitive.Value.ToString();
					else
						text = "null";
					break;
				}
				
				var str = value as StringMirror;
				if (str != null)
				{
					text = "\"" + str.Value + "\"";
					break;
				}
			}
			while (false);
			
			return text;
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
