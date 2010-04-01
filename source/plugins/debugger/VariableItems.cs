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

using Gear;
using MCocoa;
using MObjc;
using MObjc.Helpers;
using Mono.Debugger;
using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Debugger
{
	[ExportClass("ArrayElementItem", "VariableItem")]
	internal sealed class ArrayElementItem : VariableItem
	{
		public ArrayElementItem(string name, string type, Value value, ThreadMirror thread, Action<Value> setter) : base(thread, name, value, type, "ArrayElementItem")
		{
			m_setter = setter;
			m_item = CreateVariable(name, type, value, thread, setter);
		}
		
		public override bool IsExpandable
		{
			get {return m_item != null && m_item.IsExpandable;}
		}
		
		public override int Count
		{
			get {return m_item != null ? m_item.Count : 0;}
		}
		
		public override VariableItem this[int index]
		{
			get {return m_item != null ? m_item[index] : null;}
		}
		
		public override void RefreshValue(ThreadMirror thread, Value value)
		{
			m_item = m_item.RefreshVariable(thread, value, m_setter);
			
			base.RefreshValue(thread, value);
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			if (m_item != null)
			{
				m_item.release();
				m_item = null;
			}
			
			base.OnDealloc();
		}
		#endregion
		
		#region Fields
		private Action<Value> m_setter;
		private VariableItem m_item;
		#endregion
	}
	
	[ExportClass("ArrayValueItem", "VariableItem")]
	internal sealed class ArrayValueItem : VariableItem
	{
		public ArrayValueItem(string name, string type, ArrayMirror value, ThreadMirror thread) : base(thread, name, value, type, "ArrayValueItem")
		{
			m_object = value;
			m_thread = thread;
		}
		
		public override bool IsExpandable
		{
			get {return m_object != null && !m_object.IsCollected && m_object.Length > 0;}
		}
		
		public override int Count
		{
			get {DoConstructItems(); return m_items.Length;}
		}
		
		public override VariableItem this[int index]
		{
			get {DoConstructItems(); return m_items[index];}
		}
		
		public override void RefreshValue(ThreadMirror thread, Value value)
		{
			m_object = (ArrayMirror) value;
			
			if (m_object != null && !m_object.IsCollected)
			{
				if (m_items != null)
				{
					Contract.Assert(m_items.Length == m_object.Length);
					for (int i = 0; i < m_object.Length; ++i)
					{
						int tmp = i;
						Action<Value> setter = (Value v) => m_object[tmp] = v;	// TODO: won't work for multiple dimensions, throw?
						m_items[i] = m_items[i].RefreshVariable(thread, m_object[i], setter);
					}
				}
			}
			else
			{
				DoReset();
			}
			
			base.RefreshValue(thread, value);
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			DoReset();
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private void DoReset()
		{
			if (m_items != null)
			{
				foreach (VariableItem item in m_items)
				{
					item.release();
				}
				Array.Clear(m_items, 0, m_items.Length);
				
				m_items = null;
			}
		}
		
		private void DoConstructItems()
		{
			if (m_items == null)
			{
				if (m_object != null && !m_object.IsCollected)
				{
					m_items = new VariableItem[m_object.Length];
					for (int i = 0; i < m_object.Length; ++i)
					{
						string name = DoGetName(i);
						int tmp = i;
						Action<Value> setter = (Value v) => m_object[tmp] = v;	// TODO: won't work for multiple dimensions, throw?
						m_items[i] = new ArrayElementItem(name, m_object.Type.GetElementType().FullName, m_object[i], m_thread, setter);
					}
				}
				else
				{
					m_items = new VariableItem[0];
				}
			}
		}
		
		private string DoGetName(int i)
		{
			var builder = new System.Text.StringBuilder();
			
			for (int dim = 0; dim < m_object.Rank; ++dim)
			{
				int length = DoGetLength(dim);
				int index;
				if (dim < m_object.Rank - 1)
				{
					index = i/length;
					i = i - length*index;
				}
				else
				{
					index = i;
				}
				
				builder.Append((index + m_object.GetLowerBound(dim)).ToString());
				if (dim + 1 < m_object.Rank)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		
		private int DoGetLength(int dimension)
		{
			int length = 1;
			
			for (int dim = dimension + 1; dim < m_object.Rank; ++dim)
			{
				length *= m_object.GetLength(dim);
			}
			
			return length;
		}
		#endregion
		
		#region Fields
		private ArrayMirror m_object;
		private VariableItem[] m_items;
		private ThreadMirror m_thread;
		#endregion
	}
	
	[ExportClass("EnumValueItem", "VariableItem")]
	internal sealed class EnumValueItem : VariableItem
	{
		public EnumValueItem(ThreadMirror thread, string name, string type, EnumMirror value) : base(thread, name, value, type, "EnumValueItem")
		{
		}
	}
	
	[ExportClass("MethodValueItem", "VariableItem")]
	internal sealed class MethodValueItem : VariableItem
	{
		public MethodValueItem(StackFrame frame) : base("MethodValueItem")
		{
			m_frame = frame;
			
			LocalVariable[] locals = frame.Method.GetLocals();
			Value[] values = frame.GetValues(locals);
			Contract.Assert(locals.Length == values.Length);
			
			for (int i = 0; i < locals.Length; ++i)
			{
				string name = locals[i].Name;
				if (string.IsNullOrEmpty(name))
					name = "$" + locals[i].Index;		// temporary variable
				
				LocalVariable tmp = locals[i];
				Action<Value> setter = (Value v) => m_frame.SetValue(tmp, v);
				m_items.Add(CreateVariable(name, locals[i].Type.FullName, values[i], frame.Thread, setter));
			}
			
			if (!frame.Method.IsStatic)		// note that this includes static fields
				m_items.Add(CreateVariable("this", frame.Method.DeclaringType.FullName, frame.GetThis(), frame.Thread, null));
				
			else if (frame.Method.DeclaringType.GetFields().Any(f => f.IsStatic))
				m_items.Add(CreateVariable("statics", frame.Method.DeclaringType.FullName, frame.Method.DeclaringType, frame.Thread));
		}
		
		public StackFrame Frame
		{
			get {return m_frame;}
		}
		
		public void Refresh(StackFrame frame)
		{
			m_frame = frame;
			
			LocalVariable[] locals = frame.Method.GetLocals();
			Value[] values = frame.GetValues(locals);
			Contract.Assert(locals.Length == values.Length);
			Contract.Assert(locals.Length <= m_items.Count);
			
			for (int i = 0; i < locals.Length; ++i)
			{
				LocalVariable tmp = locals[i];
				Action<Value> setter = (Value v) => m_frame.SetValue(tmp, v);
				m_items[i] = m_items[i].RefreshVariable(frame.Thread, values[i], setter);
			}
			
			if (!frame.Method.IsStatic)		// note that this includes static fields
				m_items[locals.Length].RefreshValue(frame.Thread, frame.GetThis());
				
			else if (frame.Method.DeclaringType.GetFields().Any(f => f.IsStatic))
				m_items[locals.Length].RefreshValue(frame.Thread, null);
		}
		
		public override int Count
		{
			get {return m_items.Count;}
		}
		
		public override VariableItem this[int index]
		{
			get {return m_items[index];}
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			foreach (VariableItem item in m_items)
			{
				item.release();
			}
			m_items.Clear();
			
			base.OnDealloc();
		}
		#endregion
		
		#region Fields
		private List<VariableItem> m_items = new List<VariableItem>();
		private StackFrame m_frame;
		#endregion
	}
	
	[ExportClass("NullValueItem", "VariableItem")]
	internal sealed class NullValueItem : VariableItem
	{
		public NullValueItem(ThreadMirror thread, string name, string type) : base(thread, name, null, type, "NullValueItem")
		{
		}
	}
	
	[ExportClass("ObjectValueItem", "VariableItem")]
	internal sealed class ObjectValueItem : VariableItem
	{
		public ObjectValueItem(string name, string type, ObjectMirror value, ThreadMirror thread) : base(thread, name, value, type, "ObjectValueItem")
		{
			m_object = value;
			m_thread = thread;
		}
		
		public override bool IsExpandable
		{
			get {return m_object != null && !m_object.IsCollected;}
		}
		
		public override int Count
		{
			get {DoConstructItems(); return m_items.Length;}
		}
		
		public override VariableItem this[int index]
		{
			get {DoConstructItems(); return m_items[index];}
		}
		
		public override void RefreshValue(ThreadMirror thread, Value value)
		{
			m_object = (ObjectMirror) value;
			
			if (m_object != null && !m_object.IsCollected)
			{
				if (m_items != null)
				{
					FieldInfoMirror[] fields = m_object.Type.GetFields();
					Contract.Assert(m_items.Length == fields.Length);
					
					Value[] values = m_object.GetValues(fields);
					Contract.Assert(values.Length == fields.Length);
					
					for (int i = 0; i < values.Length; ++i)
					{
						FieldInfoMirror tmp = fields[i];
						Action<Value> setter = (Value v) => m_object.SetValue(tmp, v);
						m_items[i] = m_items[i].RefreshVariable(thread, values[i], setter);
					}
				}
			}
			else
			{
				DoReset();
			}
			
			base.RefreshValue(thread, value);
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			DoReset();
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private void DoReset()
		{
			if (m_items != null)
			{
				foreach (VariableItem item in m_items)
				{
					item.release();
				}
				Array.Clear(m_items, 0, m_items.Length);
				
				m_items = null;
			}
		}
		
		private void DoConstructItems()
		{
			if (m_items == null)
			{
				if (m_object != null && !m_object.IsCollected)
				{
					FieldInfoMirror[] fields = m_object.Type.GetFields();
					m_items = new VariableItem[fields.Length];
					
					Value[] values = m_object.GetValues(fields);
					Contract.Assert(values.Length == fields.Length);
					
					for (int i = 0; i < values.Length; ++i)
					{
						FieldInfoMirror tmp = fields[i];
						Action<Value> setter = (Value v) => m_object.SetValue(tmp, v);
						m_items[i] = CreateVariable(fields[i].Name, fields[i].FieldType.FullName, values[i], m_thread, setter);
					}
				}
				else
				{
					m_items = new VariableItem[0];
				}
			}
		}
		#endregion
		
		#region Fields
		private ObjectMirror m_object;
		private VariableItem[] m_items;
		private ThreadMirror m_thread;
		#endregion
	}
	
	[ExportClass("PrimitiveValueItem", "VariableItem")]
	internal sealed class PrimitiveValueItem : VariableItem
	{
		public PrimitiveValueItem(ThreadMirror thread, string name, string type, PrimitiveValue value, Action<Value> setter) : base(thread, name, value, type, "PrimitiveValueItem")
		{
			Contract.Requires(value.Value != null, "use NullValueItem instead");
			Contract.Requires(setter != null, "setter is null");
			
			m_vm = thread.VirtualMachine;
			m_setter = setter;
		}
		
		public override void SetValue(string text)
		{
			try
			{
				object value = DoParse(text);
				m_setter(m_vm.CreateValue(value));
				m_value = CreateString(OnPrimitiveToString(value));
			}
			catch (Exception e)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.Show();
				transcript.WriteLine(Output.Error, "{0}", e.Message);
			}
		}
		
		#region Private Methods		
		private object DoParse(string text)
		{
			object value = null;
			switch (TypeName.ToString())
			{
				case "System.Boolean":
					if (text == "0")
						value = false;
					else if (text == "1")
						value = true;
					else
						value = Boolean.Parse(text);
					break;
					
				case "System.Char":
					if (text.Length == 1)
						value = text[0];
					else if (text.Length == 3 && text[0] == '\'' && text[2] == '\'')
						value = text[1];
					else if (text == "'\\n'")
						value = '\n';
					else if (text == "'\\r'")
						value = '\r';
					else if (text == "'\\t'")
						value = '\t';
					else if (text == "'\\f'")
						value = '\f';
					else if (text == "'\\''")
						value = '\'';
					else if (text.Length > 4 && text.StartsWith("'\\x") && text.EndsWith("'"))
						value = unchecked((char) int.Parse(text.Substring(3, text.Length - 4), NumberStyles.AllowHexSpecifier));
					else
						throw new Exception("Character format is 'x', '\\n', '\\r', '\\t', '\\f', '\\'', or '\\x9ABC'.");
					break;
					
				case "System.SByte":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = SByte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = SByte.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.Int16":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = Int16.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = Int16.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.Int32":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = Int32.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = Int32.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.Int64":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = Int64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = Int64.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.Byte":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = Byte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = Byte.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.UInt16":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = UInt16.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = UInt16.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.UInt32":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = UInt32.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = UInt32.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
					break;
					
				case "System.UInt64":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
					else
						value = UInt64.Parse(text);
					break;
					
				case "System.Single":
					value = Single.Parse(text, NumberStyles.Float | NumberStyles.AllowThousands);
					break;
					
				case "System.Double":
					value = Double.Parse(text, NumberStyles.Float | NumberStyles.AllowThousands);
					break;
					
				case "System.Decimal":
					value = Decimal.Parse(text);
					break;
					
				case "System.IntPtr":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = new IntPtr(unchecked((long) UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier)));
					else
						value = new IntPtr(unchecked((long) UInt64.Parse(text)));
					break;
					
				case "System.UIntPtr":
					if (text.Length > 2 && text.StartsWith("0x"))
						value = new UIntPtr(UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier));
					else
						value = new UIntPtr(UInt64.Parse(text));
					break;
					
				default:
					Contract.Assert(false, "bad type: " + TypeName.ToString());
					break;
			}
			
			return value;
		}
		#endregion
		
		#region Fields
		private VirtualMachine m_vm;
		private Action<Value> m_setter;
		#endregion
	}
	
	[ExportClass("StringValueItem", "VariableItem")]
	internal sealed class StringValueItem : VariableItem
	{
		public StringValueItem(ThreadMirror thread, string name, string type, StringMirror value) : base(thread, name, value, type, "StringValueItem")
		{
		}
	}
	
	[ExportClass("StructValueItem", "VariableItem")]
	internal sealed class StructValueItem : VariableItem
	{
		public StructValueItem(string name, string type, StructMirror value, ThreadMirror thread) : base(thread, name, value, type, "StructValueItem")
		{
			m_object = value;
			m_thread = thread;
		}
		
		public override bool IsExpandable
		{
			get {return true;}
		}
		
		public override int Count
		{
			get {return m_object.Fields.Length;}
		}
		
		public override VariableItem this[int index]
		{
			get {DoConstructItems(); return m_items[index];}
		}
		
		public override void RefreshValue(ThreadMirror thread, Value value)
		{
			m_object = (StructMirror) value;
			
			if (m_items != null)
			{
				Contract.Assert(m_items.Length == m_object.Fields.Length);
				
				FieldInfoMirror[] fields = m_object.Type.GetFields();
				Contract.Assert(m_object.Fields.Length == fields.Length);
				
				for (int i = 0; i < m_object.Fields.Length; ++i)
				{
					int tmp = i;
					Action<Value> setter = (Value v) => m_object.Fields[tmp] = v;
					m_items[i] = m_items[i].RefreshVariable(thread, m_object.Fields[i], setter);
				}
			}
			
			base.RefreshValue(thread, value);
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			if (m_items != null)
			{
				foreach (VariableItem item in m_items)
				{
					item.release();
				}
				Array.Clear(m_items, 0, m_items.Length);
				
				m_items = null;
			}
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private void DoConstructItems()
		{
			if (m_items == null)
			{
				m_items = new VariableItem[m_object.Fields.Length];
				
				FieldInfoMirror[] fields = m_object.Type.GetFields();
				Contract.Assert(m_object.Fields.Length == fields.Length);
				
				for (int i = 0; i < m_object.Fields.Length; ++i)
				{
					int tmp = i;
					Action<Value> setter = (Value v) => m_object.Fields[tmp] = v;
					m_items[i] = CreateVariable(fields[i].Name, fields[i].FieldType.FullName, m_object.Fields[i], m_thread, setter);
				}
			}
		}
		#endregion
		
		#region Fields
		private StructMirror m_object;
		private VariableItem[] m_items;
		private ThreadMirror m_thread;
		#endregion
	}
	
	[ExportClass("TypeValueItem", "VariableItem")]
	internal sealed class TypeValueItem : VariableItem
	{
		public TypeValueItem(string name, string type, TypeMirror value, ThreadMirror thread) : base(name, type, "TypeValueItem")
		{
			m_fields = (from f in value.GetFields() where f.IsStatic select f).ToArray();
			m_object = value;
			m_thread = thread;
		}
		
		public override bool IsExpandable
		{
			get {return true;}
		}
		
		public override int Count
		{
			get {return m_fields.Length;}
		}
		
		public override VariableItem this[int index]
		{
			get {DoConstructItems(); return m_items[index];}
		}
		
		public override void RefreshValue(ThreadMirror thread, Value value)
		{
			if (m_object != null)
			{
				if (m_items != null)
				{
					Contract.Assert(m_items.Length == m_fields.Length);
					
					for (int i = 0; i < m_fields.Length; ++i)
					{
						FieldInfoMirror tmp = m_fields[i];
						Action<Value> setter = (Value v) => m_object.SetValue(tmp, v);
						m_items[i] = m_items[i].RefreshVariable(thread, m_object.GetValue(m_fields[i]), setter);
					}
				}
			}
			else
			{
				DoReset();
			}
			
			// Don't call the base method (there's no need to and we cheat a little bit and
			// pass null in for value when MethodValueItem does its refresh).
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			DoReset();
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private void DoReset()
		{
			if (m_items != null)
			{
				foreach (VariableItem item in m_items)
				{
					item.release();
				}
				Array.Clear(m_items, 0, m_items.Length);
				
				m_items = null;
			}
		}
		
		private void DoConstructItems()
		{
			if (m_items == null)
			{
				m_items = new VariableItem[m_fields.Length];
				
				for (int i = 0; i < m_fields.Length; ++i)
				{
					FieldInfoMirror tmp = m_fields[i];
					Action<Value> setter = (Value v) => m_object.SetValue(tmp, v);
					m_items[i] = CreateVariable(m_fields[i].Name, m_fields[i].FieldType.FullName, m_object.GetValue(m_fields[i]), m_thread, setter);
				}
			}
		}
		#endregion
		
		#region Fields
		private TypeMirror m_object;
		private FieldInfoMirror[] m_fields;
		private VariableItem[] m_items;
		private ThreadMirror m_thread;
		#endregion
	}
}
