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

namespace Debugger
{
	// StackFrame becomes unuseable pretty much immediately (e.g. after doing
	// an invoke) so we need to get values for all locals asap.
	internal sealed class CachedStackFrame
	{
		public CachedStackFrame(StackFrame frame)
		{
			Frame = frame;
			Thread = frame.Thread;
			VirtualMachine = frame.VirtualMachine;
			
			m_locals = frame.Method.GetLocals();
			m_values = frame.GetValues(m_locals);
		}
		
		public StackFrame Frame {get; private set;}
		
		public ThreadMirror Thread {get; private set;}
		
		public VirtualMachine VirtualMachine {get; private set;}
		
		public int Length {get {return m_values.Length;}}
		
		public LocalVariable GetLocal(int index)
		{
			return m_locals[index];
		}
		
		public Value GetValue(LocalVariable local)
		{
			int index = Array.IndexOf(m_locals, local);
			return m_values[index];
		}
		
		#region Private Methods
		private LocalVariable[] m_locals;
		private Value[] m_values;
		#endregion
	}
}
