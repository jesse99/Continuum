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

//using MObjc.Helpers;
using Mono.Debugger.Soft;
//using Shared;
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
			return new VariableItem(thread, name, child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, CachedStackFrame value, int index)
		{
			LocalVariable local = value.GetLocal(index);
			
			string name = local.Name;
			if (string.IsNullOrEmpty(name))
				name = "$" + local.Index;			// temporary variable
			
			Value child = value.GetValue(local);
			return new VariableItem(thread, name, child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, ObjectMirror value, int index)
		{
			FieldInfoMirror field = value.Type.GetAllFields().ElementAt(index);
			Value child = EvalMember.Evaluate(thread, value, field.Name);
			return new VariableItem(thread, field.Name, child, index);
		}
		
		[GetChild.Overload]
		public static VariableItem GetChild(ThreadMirror thread, StringMirror value, int index)
		{
			string name = index.ToString();
			char child = value.Value[index];
			return new VariableItem(thread, name, child, index);
		}
		
		#region Private Methods
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
