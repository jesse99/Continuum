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
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, ArrayMirror parent, int index)
		{
			string name = DoGetArrayName(parent, index);
			Value child = parent[index];
			return new VariableItem(thread, name, parentItem, index, child, index);
		}
		
//		[GetChild.Overload]
//		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, EnumerableValue parent, int index)
//		{
//			return parent.GetChild(thread, parentItem, index);
//		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, LiveStackFrame parent, int index)
		{
			VariableItem child;
			
			LocalVariable[] locals = parent.Method.GetLocals();
			if (index < locals.Length)
			{
				LocalVariable local = locals[index];
				
				string name = local.Name;
				if (string.IsNullOrEmpty(name))
					name = "$" + local.Index;			// temporary variable
				
				Value v = parent.GetValue(local);
				child = new VariableItem(thread, name, parentItem, local, v, index);
			}
			else
			{
				FieldInfoMirror[] fields = parent.Method.DeclaringType.GetAllFields().ToArray();
				Contract.Assert(fields.Length > 0);
				
				object v = null;
				string name = "this";
				if (parent.ThisPtr is ObjectMirror)
				{
					v = new InstanceValue((ObjectMirror) parent.ThisPtr, fields);
				}
				else if (parent.ThisPtr is StructMirror)
				{
					v = new InstanceValue((StructMirror) parent.ThisPtr, fields);
				}
				else if (parent.ThisPtr == null || parent.ThisPtr.IsNull())
				{
					v = new TypeValue(parent.Method.DeclaringType);
					name = "statics";
				}
				else
				{
					Contract.Assert(false, parent.ThisPtr.TypeName() + " is bogus");
				}
				
				child = new VariableItem(thread, name, parentItem, index, v, index);
			}
			
			return child;
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, InstanceValue parent, int index)
		{
			return parent.GetChild(thread, parentItem, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, ObjectMirror parent, int index)
		{
			if (parent.Type.IsType("System.MulticastDelegate"))
			{
				return DoGetMulticastDelegateChild(thread, parentItem, parent, index);
			}
//			else if (parent.Type.FullName.StartsWith("System.Collections") && parent.Type.FindMethod("GetEnumerator", 0) != null)	// TODO: better to use Is(ICollection) but TypeMirror does not expose interfaces
//			{
//				var child = new EnumerableValue(parentItem, parent);
//				return new VariableItem(thread, "Enumerable", parentItem, index, child, index);
//			}
			else
			{
				PropertyInfoMirror[] props = (from p in parent.Type.GetAllProperties() where p.HasSimpleGetter() select p).ToArray();
				if (index < props.Length)
				{
					PropertyInfoMirror prop = props[index];
					Value child = EvalMember.Evaluate(thread, parent, prop.Name);
					return new VariableItem(thread, prop.Name, parentItem, prop, child, index);
				}
				else
				{
					FieldInfoMirror[] fields = (from f in parent.Type.GetAllFields() where !f.Name.Contains("__BackingField") && !f.HasCustomAttribute("System.ThreadStaticAttribute") select f).ToArray();	// TODO: sdb falls down for thread static fields
					FieldInfoMirror field = fields[index - props.Length];
					Value child = EvalMember.Evaluate(thread, parent, field.Name);
					return new VariableItem(thread, field.Name, parentItem, field, child, index);
				}
			}
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, StringMirror parent, int index)
		{
			string name = index.ToString();
			char child = parent.Value[index];
			return new VariableItem(thread, name, parentItem, index, child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, StructMirror parent, int index)
		{
			PropertyInfoMirror[] props = (from p in parent.Type.GetAllProperties() where p.HasSimpleGetter() select p).ToArray();
			if (index < props.Length)
			{
				PropertyInfoMirror prop = props[index];
				Value child = EvalMember.Evaluate(thread, parent, prop.Name);
				return new VariableItem(thread, prop.Name, parentItem, prop, child, index);
			}
			else
			{
				FieldInfoMirror[] fields = (from f in parent.Type.GetAllFields() where !f.Name.Contains("__BackingField") && !f.HasCustomAttribute("System.ThreadStaticAttribute") select f).ToArray();
				FieldInfoMirror field = fields[index - props.Length];
				Value child;
				if (field.IsStatic)
					child = parent.Type.GetValue(field);
				else
					child = parent.Fields[index - props.Length];
				return new VariableItem(thread, field.Name, parentItem, index, child, index);
			}
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, TypeMirror parent, int index)
		{
			var props = (from p in parent.GetAllProperties() where p.HasSimpleGetter() && (p.GetGetMethod(true) != null && p.GetGetMethod(true).IsStatic) || (p.GetSetMethod(true) != null && p.GetSetMethod(true).IsStatic) select p).ToArray();
			if (index < props.Length)
			{
				PropertyInfoMirror prop = props[index];
				MethodMirror method = parent.ResolveProperty(prop.Name);
				Value child = parent.InvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
				return new VariableItem(thread, prop.Name, parentItem, prop, child, index);
			}
			else
			{
				var fields = from f in parent.GetAllFields() where f.IsStatic && !f.HasCustomAttribute("System.ThreadStaticAttribute") select f;
				FieldInfoMirror field = fields.ElementAt(index - props.Length);
				Value child = field.DeclaringType.GetValue(field);
				return new VariableItem(thread, field.Name, parentItem, index, child, index);
			}
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, VariableItem parentItem, TypeValue parent, int index)
		{
			return parent.GetChild(thread, parentItem, index);
		}
		
		#region Private Methods
		private static VariableItem DoGetMulticastDelegateChild(ThreadMirror thread, VariableItem parentItem, ObjectMirror target, int index)
		{
			Value result;
			FieldInfoMirror field;
			switch (index)
			{
				case 0:
					result = EvalMember.Evaluate(thread, target, "Target");
					field = target.Type.GetAllFields().Single(f => f.Name == "m_target");
					return new VariableItem(thread, "Target", parentItem, field, result, index);
				
				case 1:
					result = EvalMember.Evaluate(thread, target, "Method");
					field = target.Type.GetAllFields().Single(f => f.Name == "method");
					return new VariableItem(thread, "Method", parentItem, field, result, index);
				
				case 2:
					result = EvalMember.Evaluate(thread, target, "kpm_next");
					field = target.Type.GetAllFields().Single(f => f.Name == "kpm_next");
					return new VariableItem(thread, "Next", parentItem, field, result, index);
				
				case 3:
					result = EvalMember.Evaluate(thread, target, "prev");
					field = target.Type.GetAllFields().Single(f => f.Name == "prev");
					return new VariableItem(thread, "Previous", parentItem, field, result, index);
				
				default:
					Contract.Assert(false);
					return null;
			}
		}
		
		private static string DoGetArrayName(ArrayMirror parent, int i)
		{
			var builder = new System.Text.StringBuilder();
			
			for (int dim = 0; dim < parent.Rank; ++dim)
			{
				int length = DoGetArrayLength(parent, dim);
				int index;
				if (dim < parent.Rank - 1)
				{
					index = i/length;
					i = i - length*index;
				}
				else
				{
					index = i;
				}
				
				builder.Append((index + parent.GetLowerBound(dim)).ToString());
				if (dim + 1 < parent.Rank)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		
		private static int DoGetArrayLength(ArrayMirror parent, int dimension)
		{
			int length = 1;
			
			for (int dim = dimension + 1; dim < parent.Rank; ++dim)
			{
				length *= parent.GetLength(dim);
			}
			
			return length;
		}
		#endregion
	}
}
