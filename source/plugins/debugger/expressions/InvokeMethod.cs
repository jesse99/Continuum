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

using MCocoa;
using Mono.Debugger.Soft;
using MObjc.Helpers;
using Shared;
using System;
using System.Threading;

namespace Debugger
{
	// This may come in handy later if we allow arbitrary methods to be called because,
	// unlike well-written properties, methods may take an arbitrary amount of time to
	// execute and we don't want to block the UI too long. In the meantime this is also
	// important because calling code in the debugee can do things like cause types to
	// load which suspends the VM causing deadlocks if we don't execute the code
	// asynchronously.
	internal sealed class InvokeMethod
	{
		[ThreadModel (ThreadModel.MainThread)]
		public delegate void Callback (ThreadMirror thread, Value value);
		
		// Setter will be called when the invoked method eventually returns.
		public InvokeMethod(Callback setter)
		{
			Contract.Requires(setter != null, "setter is null");
			
			m_setter = setter;
		}
		
		// Asynchronously invokes a nullary method on an ObjectMirror or StructMirror.
		// If the method throws the setter will be called with an error string.
		public void Invoke(ThreadMirror thread, Value target, string name)
		{
			Contract.Requires(thread != null, "thread is null");
			Contract.Requires(target != null, "target is null");
			Contract.Requires(target is ObjectMirror || target is StructMirror, "target is a " + target.GetType().FullName);
			Contract.Requires(!name.IsNullOrWhiteSpace());
			
			if (target is ObjectMirror)
				DoInvoke(thread, (ObjectMirror) target, name);
			else
				DoInvoke(thread, (StructMirror) target, name);
		}
		
		#region Private Methods
		private void DoInvoke(ThreadMirror thread, ObjectMirror obj, string name)
		{
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method != null)
			{
				IAsyncResult result = obj.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
				Func<Value> getter = () => obj.EndInvokeMethod(result);
				ThreadPool.QueueUserWorkItem(o => DoWaitForResult(thread, getter, result));	// the pool uses background threads so the app will exit even if this thread is still running
			}
			else
			{
				throw new Exception(string.Format("Couldn't find a property for {0}.{1}", obj.Type.FullName, name));
			}
		}
		
		private void DoInvoke(ThreadMirror thread, StructMirror obj, string name)
		{
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method != null)
			{
				if (obj.Type.IsPrimitive)
				{
					// Boxed primitive (we need this special case or BeginInvokeMethod will hang).
					if (obj.Fields.Length > 0 && (obj.Fields[0] is PrimitiveValue))
					{
						if (name == "ToString")
						{
							Value value = (PrimitiveValue) (obj.Fields[0]);
							NSApplication.sharedApplication().BeginInvoke(() => m_setter(thread, value));
						}
					}
					throw new Exception(string.Format("{0} is a primitive type and only ToString can be used on those.", obj.Type.FullName));
				}
				else
				{
					IAsyncResult result;
					Func<Value> getter;
					if (method.IsStatic)
					{
						result = method.DeclaringType.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
						getter = () => method.DeclaringType.EndInvokeMethod(result);
					}
					else
					{
						result = obj.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
						getter = () => obj.EndInvokeMethod(result);
					}
					ThreadPool.QueueUserWorkItem(o => DoWaitForResult(thread, getter, result));
				}
			}
			else
			{
				throw new Exception(string.Format("Couldn't find a property for {0}.{1}", obj.Type.FullName, name));
			}
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoWaitForResult(ThreadMirror thread, Func<Value> getter, IAsyncResult result)
		{
			try
			{
				if (!result.IsCompleted)
					result.AsyncWaitHandle.WaitOne();		// TODO: allow the user to specify a timeout?
				
				Value value = getter();
				NSApplication.sharedApplication().BeginInvoke(() => m_setter(thread, value));
			}
			catch (Exception e)
			{
				Value err = thread.Domain.CreateString(e.Message);
				NSApplication.sharedApplication().BeginInvoke(() => m_setter(thread, err));
			}
		}
		#endregion
		
		#region Fields
		private Callback m_setter;
		#endregion
	}
}
