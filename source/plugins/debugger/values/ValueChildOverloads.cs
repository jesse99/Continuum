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

using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Linq;

namespace Debugger
{
	internal static class ValueChildOverloads
	{
		[ValueChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, object owner, ArrayMirror item, int index)
		{
			string name = DoGetArrayName(item, index);
			Value value = item[index];
			return new VariableItem(thread, name, item, value);
		}
		
		[ValueChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, object owner, ObjectMirror item, int index)
		{
			FieldInfoMirror field = item.Type.GetAllFields().ElementAt(index);
			Value value = EvalMember.Evaluate(thread, item, field.Name);
			return new VariableItem(thread, field.Name, item, value);
		}
		
		[ValueChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, object owner, StackFrame item, int index)
		{
			LocalVariable[] locals = item.Method.GetLocals();
			
			string name = locals[index].Name;
			if (string.IsNullOrEmpty(name))
				name = "$" + locals[index].Index;		// temporary variable
			
			Value value = item.GetValue(locals[index]);
			return new VariableItem(thread, name, item, value);
		}
		
		[ValueChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, object owner, StringMirror item, int index)
		{
			char ch = item.Value[index];
			Value value = thread.VirtualMachine.CreateValue(ch);
			return new VariableItem(thread, index.ToString(), item, value);
		}
		
		#region Private Methods
		private static string DoGetArrayName(ArrayMirror item, int i)
		{
			var builder = new System.Text.StringBuilder();
			
			for (int dim = 0; dim < item.Rank; ++dim)
			{
				int length = DoGetArrayLength(item, dim);
				int index;
				if (dim < item.Rank - 1)
				{
					index = i/length;
					i = i - length*index;
				}
				else
				{
					index = i;
				}
				
				builder.Append((index + item.GetLowerBound(dim)).ToString());
				if (dim + 1 < item.Rank)
					builder.Append(", ");
			}
			
			return builder.ToString();
		}
		
		private static int DoGetArrayLength(ArrayMirror item, int dimension)
		{
			int length = 1;
			
			for (int dim = dimension + 1; dim < item.Rank; ++dim)
			{
				length *= item.GetLength(dim);
			}
			
			return length;
		}
		#endregion
	}
}
