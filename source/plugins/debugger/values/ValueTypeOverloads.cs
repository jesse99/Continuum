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

//using MCocoa;
//using MObjc;
using MObjc.Helpers;
using Mono.Debugger.Soft;
using Shared;
using System;

namespace Debugger
{
	internal static class ValueTypeOverloads
	{
		[ValueType.Overload]
		public static string GetTypeName(object owner, object item)
		{
			return item.GetType().FullName;
		}
		
		[ValueType.Overload]
		public static string GetTypeName(object owner, ObjectMirror item)
		{
			return item.Type.FullName;
			
//			int index = result.IndexOf("[[");	// strip off stuff like "[[System.Int32, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"
//			if (index > 0)
//				result = result.Substring(0, index);
		}
		
		[ValueType.Overload]
		public static string GetTypeName(object owner, PrimitiveValue item)
		{
			if (item.Value == null)
				return string.Empty;
			else
				return item.Value.GetType().FullName;
		}
		
		[ValueType.Overload]
		public static string GetTypeName(object owner, StructMirror item)
		{
			string result = item.Type.FullName;
			
			int index = result.IndexOf("[[");	// strip off stuff like "[[System.Int32, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"
			if (index > 0)
				result = result.Substring(0, index);
			
			return result;
		}
	}
}
