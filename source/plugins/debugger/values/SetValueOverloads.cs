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
		public static void Set(ThreadMirror thread, VariableItem item, ArrayMirror parent, int key, Value newValue)
		{
			parent.SetValues(key, new Value[]{newValue});
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, InstanceValue parent, FieldInfoMirror key, Value newValue)
		{
			if (key.IsStatic)
			{
				key.DeclaringType.SetValue(key, newValue);
			}
			else
			{
				var o = (ObjectMirror) parent.Instance;
				o.SetValue(key, newValue);
			}
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, InstanceValue parent, int key, Value newValue)
		{
			FieldInfoMirror field = parent.Type.GetFields()[key];
			Contract.Assert(!field.IsStatic);
			
			ObjectMirror o = parent.Instance as ObjectMirror;
			if (o != null)
			{
				o.SetValue(field, newValue);
			}
			else
			{
				var s = (StructMirror) parent.Instance;
				s.Fields[key] = newValue;
				SetValue.Invoke(thread, item.Parent, item.Parent.Parent.Value, item.Parent.Key, s);
			}
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, LiveStackFrame parent, int key, Value newValue)
		{
			LocalVariable local = parent.GetVisibleVariables()[key];
			parent.SetValue(local, newValue);
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, LiveStackFrame parent, LocalVariable key, Value newValue)
		{
			parent.SetValue(key, newValue);
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, ObjectMirror parent, FieldInfoMirror key, Value newValue)
		{
			if (key.IsStatic)
				key.DeclaringType.SetValue(key, newValue);
			else
				parent.SetValue(key, newValue);
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, ObjectMirror parent, PropertyInfoMirror key, Value newValue)
		{
			Contract.Assert(key.HasSimpleGetter());		// indexors aren't shown...
			
			MethodMirror method = key.GetSetMethod(true);
			if (method != null)
			{
				Unused.Value = parent.InvokeMethod(thread, method, new Value[]{newValue}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
			}
			else
			{
				throw new Exception("Property does not have a setter.");
			}
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, StructMirror parent, int key, Value newValue)
		{
			FieldInfoMirror[] fields = parent.Type.GetFields();
			if (fields[key].IsStatic)
			{
				parent.Type.SetValue(fields[key], newValue);
			}
			else
			{
				parent.Fields[key] = newValue;
				SetValue.Invoke(thread, item.Parent, item.Parent.Parent.Value, item.Parent.Key, parent);
			}
		}
		
		[SetValue.Overload]
		public static void Set(ThreadMirror thread, VariableItem item, TypeValue parent, int key, Value newValue)
		{
			FieldInfoMirror field = parent.Type.GetFields()[key];
			Contract.Assert(field.IsStatic);
			
			parent.Type.SetValue(field, newValue);
		}
	}
}
