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
using System.Collections.Generic;

using Debug = Debugger;

namespace Debugger
{
	// Fields associated with the object executing a stack frame.
	internal sealed class InstanceValue
	{
		public InstanceValue(ObjectMirror instance, FieldInfoMirror[] fields)
		{
			Contract.Requires(instance != null);
			Contract.Requires(fields != null);
			
			m_instance = instance;
			Length = fields.Length;
			Type = instance.Type;
		}
		
		public InstanceValue(StructMirror instance, FieldInfoMirror[] fields)
		{
			Contract.Requires(instance != null);
			Contract.Requires(fields != null);
			
			m_instance = instance;
			Length = fields.Length;
			Type = instance.Type;
		}
		
		public int Length {get; private set;}
		
		public TypeMirror Type {get; private set;}
		
		public string GetText(ThreadMirror thread)
		{
			Item item = GetItem.Invoke(thread, null, m_instance);
			
			return item.Text;
		}
		
		public VariableItem GetChild(ThreadMirror thread, int index)
		{
			return Debug::GetChild.Invoke(thread, m_instance, index);
		}
		
		#region Private Methods
		private object m_instance;
		#endregion
	}
}
