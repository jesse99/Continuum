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

using Mono.Debugger.Soft;
using MObjc.Helpers;
using System;
using System.Linq;

namespace Debugger
{
	internal static class GetChildOverloads
	{
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, ArrayMirror value, int index)
		{
			string name = DoGetArrayName(value, index);
			Value child = value[index];
			return new VariableItem(thread, name, new Element<ArrayMirror, int>(value, index), child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, LiveStackFrame value, int index)
		{
			VariableItem child;
			
			LocalVariable[] locals = value.Method.GetLocals();
			if (index < locals.Length)
			{
				LocalVariable local = locals[index];
				
				string name = local.Name;
				if (string.IsNullOrEmpty(name))
					name = "$" + local.Index;			// temporary variable
				
				Value v = value.GetValue(local);
				child = new VariableItem(thread, name, local, v, index);
			}
			else
			{
				FieldInfoMirror[] fields = value.Method.DeclaringType.GetAllFields().ToArray();
				Contract.Assert(fields.Length > 0);
				
				object v = null;
				if (value.ThisPtr is ObjectMirror)
					v = new InstanceValue((ObjectMirror) value.ThisPtr, fields);
				else if (value.ThisPtr is StructMirror)
					v = new InstanceValue((StructMirror) value.ThisPtr, fields);
				else if (value.ThisPtr == null || value.ThisPtr.IsNull())
					v = new TypeValue(value.Method.DeclaringType, fields);
				else
					Contract.Assert(false, value.ThisPtr.TypeName() + " is bogus");
					
				string name = fields.All(f => f.IsStatic) ? "statics" : "this";
				child = new VariableItem(thread, name, value.ThisPtr, v, index);
			}
			
			return child;
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, InstanceValue value, int index)
		{
			return value.GetChild(thread, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, ObjectMirror value, int index)
		{
			FieldInfoMirror field = value.Type.GetAllFields().ElementAt(index);
			Value child = EvalMember.Evaluate(thread, value, field.Name);
			return new VariableItem(thread, DoSanitizeFieldName(field), new Element<ObjectMirror, FieldInfoMirror>(value, field), child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, StringMirror value, int index)
		{
			string name = index.ToString();
			char child = value.Value[index];
			return new VariableItem(thread, name, null, child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, StructMirror value, int index)
		{
			FieldInfoMirror field = value.Type.GetFields()[index];
			Value child;
			if (field.IsStatic)
				child = value.Type.GetValue(field);
			else
				child = value.Fields[index];
			return new VariableItem(thread, DoSanitizeFieldName(field), new Element<StructMirror, int>(value, index), child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, TypeMirror value, int index)
		{
			FieldInfoMirror field = value.GetAllFields().ElementAt(index);
			Value child = value.GetValue(field);
			return new VariableItem(thread, DoSanitizeFieldName(field), new Element<TypeMirror, int>(value, index), child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, TypeValue value, int index)
		{
			return value.GetChild(thread, index);
		}
		
		#region Private Methods
		private static string DoSanitizeFieldName(FieldInfoMirror field)
		{
			string name = field.Name;
			
			if (name.StartsWith("<") && name.EndsWith("BackingField"))
			{
				int i = name.IndexOf('>');
				name = name.Substring(1, i - 1);
			}
			
			return name;
		}
		
		private static string DoGetArrayName(ArrayMirror value, int i)
		{
			var builder = new System.Text.StringBuilder();
			
			for (int dim = 0; dim < value.Rank; ++dim)
			{
				int length = DoGetArrayLength(value, dim);
				int index;
				if (dim < value.Rank - 1)
				{
					index = i/length;
					i = i - length*index;
				}
				else
				{
					index = i;
				}
				
				builder.Append((index + value.GetLowerBound(dim)).ToString());
				if (dim + 1 < value.Rank)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		
		private static int DoGetArrayLength(ArrayMirror value, int dimension)
		{
			int length = 1;
			
			for (int dim = dimension + 1; dim < value.Rank; ++dim)
			{
				length *= value.GetLength(dim);
			}
			
			return length;
		}
		#endregion
	}
}
