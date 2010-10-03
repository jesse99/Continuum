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
	// Mono.Debugger.Soft.StackFrame is very painful to use: doing things like
	// invoking ToString method renders the stack frame unuseable. To get around
	// this we only talk to StackFrame using this class which will fetch a new stack
	// frame if the current one is unuseable.
	internal sealed class LiveStackFrame : IEquatable<LiveStackFrame>
	{
		// Index 0 is the furthest stack frame from main.
		public LiveStackFrame(ThreadMirror thread, int index)
			: this(thread, thread.GetFrames(), index)
		{
		}
		
		public LiveStackFrame(ThreadMirror thread, StackFrame[] stack, int index)
		{
			Contract.Requires(thread != null);
			Contract.Requires(index >= 0);
			Contract.Requires(index < stack.Length);
			
			m_thread = thread;
			m_index = index;
			m_frame = stack[m_index];
			
			Thread = thread;
			VirtualMachine = m_frame.VirtualMachine;
			Method = m_frame.Method;
			Location = m_frame.Location;
			FileName = m_frame.FileName;
			ILOffset = m_frame.ILOffset;
			LineNumber = m_frame.LineNumber;
			ThisPtr = m_frame.GetThis();
		}
		
		public VirtualMachine VirtualMachine {get; private set;}
		
		public ThreadMirror Thread {get; private set;}
		
		public MethodMirror Method {get; private set;}
		
		public Location Location {get; private set;}
		
		public string FileName {get; private set;}
		
		public int ILOffset {get; private set;}
		
		public int LineNumber {get; private set;}
		
		public Value ThisPtr {get; private set;}
		
		public Value GetValue(ParameterInfoMirror param)
		{
			Value result;
			
			try
			{
				result = m_frame.GetValue(param);
			}
			catch (InvalidStackFrameException)
			{
				m_frame = m_thread.GetFrames()[m_index];
				result = m_frame.GetValue(param);
			}
			
			return result;
		}
		
		public Value GetValue(LocalVariable variable)
		{
			Value result;
			
			try
			{
				result = m_frame.GetValue(variable);
			}
			catch (InvalidStackFrameException)
			{
				m_frame = m_thread.GetFrames()[m_index];
				result = m_frame.GetValue(variable);
			}
			
			return result;
		}
		
		public void SetValue(LocalVariable variable, Value value)
		{
			try
			{
				m_frame.SetValue(variable, value);
			}
			catch (InvalidStackFrameException)
			{
				m_frame = m_thread.GetFrames()[m_index];
				m_frame.SetValue(variable, value);
			}
		}
		
		#region Equality Methods
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			LiveStackFrame rhs = obj as LiveStackFrame;
			return this == rhs;
		}
		
		public bool Equals(LiveStackFrame rhs)
		{
			return this == rhs;
		}
		
		// Returns true if lhs and rhs refer to the same stack frame (but not
		// necessarily the same place within the stack frame).
		public static bool operator==(LiveStackFrame lhs, LiveStackFrame rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Thread.Id != rhs.Thread.Id)						// note that Address can change after a GC
				return false;
			
			if (lhs.m_index != rhs.m_index)
				return false;
			
			if (lhs.Method.MetadataToken != rhs.Method.MetadataToken)
				return false;
			
			return true;
		}
		
		public static bool operator!=(LiveStackFrame lhs, LiveStackFrame rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Thread.Id.GetHashCode();
				hash += m_index.GetHashCode();
				hash += Method.MetadataToken.GetHashCode();
				hash += ILOffset.GetHashCode();
			}
			
			return hash;
		}
		#endregion
		
		#region Fields
		private ThreadMirror m_thread;
		private int m_index;
		private StackFrame m_frame;
		#endregion
	}
}
