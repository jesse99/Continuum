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
	internal static class ValueNumChildrenOverloads
	{
		[ValueNumChildren.Overload]
		public static int GetNumChildren(object owner, ArrayMirror item)
		{
			return item.IsCollected ? 0 : item.Length;
		}
		
		[ValueNumChildren.Overload]
		public static int GetNumChildren(object owner, object item)
		{
			return 0;
		}
		
		[ValueNumChildren.Overload]
		public static int GetNumChildren(object owner, ObjectMirror item)
		{
			return item.IsCollected ? 0 : item.Type.GetAllFields().Count();
		}
		
		[ValueNumChildren.Overload]
		public static int GetNumChildren(object owner, StackFrame item)
		{
			LocalVariable[] locals = item.Method.GetLocals();
			return locals.Length;	// TODO: include a this or statics child, what about parameters?
		}
		
		[ValueNumChildren.Overload]
		public static int GetNumChildren(object owner, StringMirror item)
		{
			return item.IsCollected ? 0 : item.Value.Length;
		}
	}
}
