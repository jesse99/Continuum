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
	// Used to invoke a method with a timeout.
	internal sealed class InvokeMethod
	{
		// Invokes a nullary method on an ObjectMirror or StructMirror. If the call
		// times out a StringMirror is returned with an error message.
		public Value Invoke(ThreadMirror thread, Value target, string name)
		{
			Contract.Requires(thread != null, "thread is null");
			Contract.Requires(target != null, "target is null");
			Contract.Requires(target is ObjectMirror || target is StructMirror, "target is a " + target.GetType().FullName);
			Contract.Requires(!name.IsNullOrWhiteSpace());
			
			Value result;
			
			try
			{
				result = UnsafeInvoke(thread, target, name);
			}
			catch (Exception e)
			{
				result = thread.Domain.CreateString(e.Message);
			}
			
			return result;
		}
		
		// Like the above except exceptions are allowed to escape.
		public Value UnsafeInvoke(ThreadMirror thread, Value target, string name)
		{
			Contract.Requires(thread != null, "thread is null");
			Contract.Requires(target != null, "target is null");
			Contract.Requires(target is ObjectMirror || target is StructMirror, "target is a " + target.GetType().FullName);
			Contract.Requires(!name.IsNullOrWhiteSpace());
			
			Value result;
			if (target is ObjectMirror)
				result = DoInvoke(thread, (ObjectMirror) target, name);
			else
				result = DoInvoke(thread, (StructMirror) target, name);
			
			return result;
		}
		
		#region Private Methods
		private Value DoInvoke(ThreadMirror thread, ObjectMirror obj, string name)
		{
			Value result;
			
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method == null)
				method = obj.Type.FindMethod(name, 0);
				
			if (method != null)
			{
				if (!Debugger.IsTypeLoaded(method.DeclaringType.FullName))
				{
					result = thread.Domain.CreateString("type not loaded");
				}
				else
				{
					IAsyncResult ar = obj.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
					
					if (!ar.IsCompleted)
						ar.AsyncWaitHandle.WaitOne(Timeout);
					
					if (ar.IsCompleted)
					{
						result = obj.EndInvokeMethod(ar);
					}
					else
					{
						Func<Value> getter = () => obj.EndInvokeMethod(ar);
						ThreadPool.QueueUserWorkItem(o => DoIgnoreResult(getter, ar));	// the pool uses background threads so the app will exit even if this thread is still running
						result = thread.Domain.CreateString("timed out");
					}
				}
			}
			else
			{
				throw new Exception(string.Format("Couldn't find a property for {0}.{1}", obj.Type.FullName, name));
			}
			
			return result;
		}
		
		private Value DoInvoke(ThreadMirror thread, StructMirror obj, string name)
		{
			Value result = null;
			
			MethodMirror method = obj.Type.ResolveProperty(name);
			if (method == null)
				method = obj.Type.FindMethod(name, 0);
				
			if (method != null)
			{
				if (!Debugger.IsTypeLoaded(method.DeclaringType.FullName))
				{
					// TODO: The soft debugger was timing out when calling BeginInvokeMethod and
					// then locking up when CreateString was called. This works around that problem,
					// but not in a good way because it's possible that the ToString will use fields which
					// have types which are not loaded.
					result = thread.Domain.CreateString("type not loaded");
				}
				else if (obj.Type.IsPrimitive)
				{
					// Boxed primitive (we need this special case or BeginInvokeMethod will hang).
					if (obj.Fields.Length > 0 && (obj.Fields[0] is PrimitiveValue))
					{
						if (name == "ToString")
						{
							PrimitiveValue value = (PrimitiveValue) (obj.Fields[0]);
							result = thread.Domain.CreateString(value.Value.ToString());
						}
					}
					
					if (result == null)
						throw new Exception(string.Format("{0} is a primitive type and only ToString can be used on those.", obj.Type.FullName));
				}
				else
				{
					IAsyncResult ar;
					Func<Value> getter;
					if (method.IsStatic)
					{
						ar = method.DeclaringType.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
						getter = () => method.DeclaringType.EndInvokeMethod(ar);
					}
					else
					{
						ar = obj.BeginInvokeMethod(thread, method, new Value[0], InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded, null, null);
						getter = () =>
						{
							Value vvv = null;
							vvv = obj.EndInvokeMethod(ar);
							return vvv;
						};
					}
					
					if (!ar.IsCompleted)
						ar.AsyncWaitHandle.WaitOne(Timeout);
					
					if (ar.IsCompleted)
					{
						result = getter();
					}
					else
					{
						ThreadPool.QueueUserWorkItem(o => DoIgnoreResult(getter, ar));	// the pool uses background threads so the app will exit even if this thread is still running
						result = thread.Domain.CreateString("timed out");
					}
				}
			}
			else
			{
				throw new Exception(string.Format("Couldn't find a property for {0}.{1}", obj.Type.FullName, name));
			}
			
			return result;
		}
		
		[ThreadModel(ThreadModel.SingleThread)]
		private void DoIgnoreResult(Func<Value> getter, IAsyncResult ar)
		{
			try
			{
				ar.AsyncWaitHandle.WaitOne();
				Unused.Value = getter();				// we need to call EndInvoke or we'll get leaks
			}
			catch (Exception eee)
			{
				// We don't care about errors here.
			}
		}
		#endregion
		
		#region Fields
		private const int Timeout = 500;	// TODO: might want to allow this to be set (especially when we get an immediate window)
		#endregion
	}
}
