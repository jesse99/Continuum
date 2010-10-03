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
using Mono.Cecil;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

using Debug = Debugger;

namespace Debugger
{
	internal static class DeclaredTypeOverloads
	{
		[DeclaredType.Overload]
		public static TypeMirror GetType(ArrayMirror parent, int key)
		{
			return parent.Type.GetElementType();
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(LiveStackFrame parent, int key)
		{
			return parent.Method.DeclaringType;
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(object parent, FieldInfoMirror key)
		{
			return key.FieldType;
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(InstanceValue parent, int key)
		{
			return null;			// we don't have enough info to return a decent type
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(object parent, LocalVariable key)
		{
			return key.Type;
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(object parent, PropertyInfoMirror key)
		{
			return key.PropertyType;
		}
		
//		[DeclaredType.Overload]
//		public static TypeMirror GetType(ObjectMirror parent, EnumerableValue key)
//		{
//			return parent.Type;
//		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(StructMirror parent, int key)
		{
			FieldInfoMirror[] fields = parent.Type.GetFields();
			return fields[key].DeclaringType;
		}
		
		[DeclaredType.Overload]
		public static TypeMirror GetType(TypeValue parent, int key)
		{
			return null;			// we don't have enough info to return a decent type
		}
	}
}
