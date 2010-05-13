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
	internal sealed class DelegateValueItem : VariableItem
	{
		public DelegateValueItem(ThreadMirror thread, string name, string type, Value value) : base(thread, name, value, type, "DelegateValueItem")
		{
			// Set target and method to ellipsis until we can get the actual values.
			StringMirror sm = thread.Domain.CreateString("...");			// TODO: VM disconnects if we try to use the ellipsis character
			Action<Value> setter = (v) => {throw new Exception("Can't set the target of a delegate.");};
			m_children[0] = new StringValueItem(thread, "Target", "System.String", sm, setter);
			
			setter = (v) => {throw new Exception("Can't set the method of a delegate.");};
			m_children[1] = new StringValueItem(thread, "Method", "System.String", sm, setter);
			
			// Asynchronously get the target.
			var invoker = new InvokeMethod(this.DoSetTarget);
			invoker.Invoke(thread, value, "Target");
			
			// Asynchronously get the method.
			invoker = new InvokeMethod(this.DoSetMethod);
			invoker.Invoke(thread, value, "Method");
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
		#endregion
		
		#region Private Methods
		private void DoSetTarget(ThreadMirror thread, Value value)
		{
			m_children[0].release();
			
			if (value.IsNull())
			{
				m_children[0] = new NullValueItem(thread, "Target", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the target of a delegate.");};
				m_children[0] = new ObjectValueItem("Target", "System.Object", (ObjectMirror) value, thread, setter);
			}
			
			// Not sure why, but we lock up if we don't use BeginInvoke here. 
			NSApplication.sharedApplication().BeginInvoke(() => VariableController.Instance.Reload());
		}
		
		private void DoSetMethod(ThreadMirror thread, Value value)
		{
			m_children[1].release();
			
			if (value.IsNull())
			{
				m_children[1] = new NullValueItem(thread, "Method", "System.Object");
			}
			else
			{
				Action<Value> setter = (v) => {throw new Exception("Can't set the method of a delegate.");};
				m_children[1] = CreateVariable("Method", value.TypeName(), value, thread, setter);
			}
			
			NSApplication.sharedApplication().BeginInvoke(() => VariableController.Instance.Reload());
		}
		#endregion
		
		#region Fields
		private VariableItem[] m_children = new VariableItem[2];
		#endregion
	}
}
