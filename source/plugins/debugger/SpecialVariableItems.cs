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
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Debugger
{
	[ExportClass("DelegateValueItem", "VariableItem")]
	internal class DelegateValueItem : VariableItem
	{
		protected DelegateValueItem(ThreadMirror thread, string name, string type, Value value, string typeName) : base(thread, name, value, type, typeName)
		{
			OnSetup(thread, value);
		}
		
		public DelegateValueItem(ThreadMirror thread, string name, string type, Value value) : this(thread, name, type, value, "DelegateValueItem")
		{
		}
		
		public override bool IsExpandable
		{
			get {return true;}
		}
		
		public override int Count
		{
			get {return m_children.Length;}
		}
		
		public override VariableItem this[int index]
		{
			get {return m_children[index];}
		}
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			foreach (VariableItem item in m_children)
			{
				item.release();
			}
			
			base.OnDealloc();
		}
		
		protected virtual void OnSetup(ThreadMirror thread, Value target)
		{
			if (m_children == null)
				m_children = new VariableItem[2];
			
			var invoker = new InvokeMethod();
			Value result = invoker.Invoke(thread, target, "Target");
			DoSetTarget(thread, result);
			
			invoker = new InvokeMethod();
			result = invoker.Invoke(thread, target, "Method");
			DoSetMethod(thread, result);
		}
		#endregion
		
		#region Private Methods
		private void DoSetTarget(ThreadMirror thread, Value value)
		{
			if (value.IsNull())
			{
				m_children[0] = new NullValueItem(thread, "Target", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the target of a delegate.");};
				m_children[0] = new ObjectValueItem("Target", "System.Object", (ObjectMirror) value, thread, setter);
			}
		}
		
		private void DoSetMethod(ThreadMirror thread, Value value)
		{
			if (value.IsNull())
			{
				m_children[1] = new NullValueItem(thread, "Method", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the method of a delegate.");};
				m_children[1] = CreateVariable("Method", value.TypeName(), value, thread, setter);
			}
		}
		#endregion
		
		#region Fields
		protected VariableItem[] m_children;
		#endregion
	}
	
	[ExportClass("MulticastDelegateValueItem", "DelegateValueItem")]
	internal sealed class MulticastDelegateValueItem : DelegateValueItem
	{
		public MulticastDelegateValueItem(ThreadMirror thread, string name, string type, Value value) : base(thread, name, type, value, "MulticastDelegateValueItem")
		{
		}
		
		#region Protected Methods
		protected override void OnSetup(ThreadMirror thread, Value target)
		{
			Value next = EvalMember.Evaluate(thread, target, "kpm_next");
			Value prev = EvalMember.Evaluate(thread, target, "prev");
			
			if (!next.IsNull() || !prev.IsNull())
			{
				m_children = new VariableItem[4];
				
				DoSetNext(thread, next);
				DoSetPrev(thread, prev);
			}
			
			base.OnSetup(thread, target);
		}
		#endregion
		
		#region Private Methods
		private void DoSetNext(ThreadMirror thread, Value value)
		{
			if (value.IsNull())
			{
				m_children[2] = new NullValueItem(thread, "Next", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the next delegate of a MulticastDelegate.");};
				m_children[2] = new ObjectValueItem("Next", "System.Object", (ObjectMirror) value, thread, setter);
			}
		}
		
		private void DoSetPrev(ThreadMirror thread, Value value)
		{
			if (value.IsNull())
			{
				m_children[3] = new NullValueItem(thread, "Prev", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the previous delegate of a MulticastDelegate.");};
				m_children[3] = CreateVariable("Prev", value.TypeName(), value, thread, setter);
			}
		}
		#endregion
	}
	
	// Used for types which may have fields, but there is no point in showing the fields.
	// For example, IntPtr or Nullable<T>.
	[ExportClass("SimpleValueItem", "VariableItem")]
	internal class SimpleValueItem : VariableItem
	{
		public SimpleValueItem(ThreadMirror thread, string name, string type, Value value) : base(thread, name, value, type, "SimpleValueItem")
		{
		}
	}
}
