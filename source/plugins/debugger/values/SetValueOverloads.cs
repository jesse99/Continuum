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
	internal static class SetValueOverloads
	{
		[SetValue.Overload]
		public static void Set(LiveStackFrame frame, VariableItem item, Element<ArrayMirror, int> hint, Value newValue)
		{
			hint.Owner.SetValues(hint.Key, new Value[]{newValue});
		}
		
		[SetValue.Overload]
		public static void Set(LiveStackFrame frame, VariableItem item, Element<ObjectMirror, FieldInfoMirror> hint, Value newValue)
		{
			if (hint.Key.IsStatic)
				hint.Key.DeclaringType.SetValue(hint.Key, newValue);
			else
				hint.Owner.SetValue(hint.Key, newValue);
		}
		
		[SetValue.Overload]
		public static void Set(LiveStackFrame frame, VariableItem item, LocalVariable hint, Value newValue)
		{
			frame.SetValue(hint, newValue);
		}
		
		[SetValue.Overload]
		public static void Set(LiveStackFrame frame, VariableItem item, Element<StructMirror, int> hint, Value newValue)
		{
			FieldInfoMirror[] fields = hint.Owner.Type.GetFields();
			if (fields[hint.Key].IsStatic)
			{
				hint.Owner.Type.SetValue(fields[hint.Key], newValue);
			}
			else
			{
				hint.Owner.Fields[hint.Key] = newValue;
//				SetValue.Invoke(frame, item.Parent, item.Parent.Hint, newValue);
			}
		}
	}
}
