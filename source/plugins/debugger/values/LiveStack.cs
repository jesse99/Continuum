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
using System.Linq;

namespace Debugger
{
	// List of stack frames that doesn't fall down if code in the debugee is invoked.
	internal sealed class LiveStack : IEquatable<LiveStack>
	{
		public LiveStack(ThreadMirror thread)
		{
			StackFrame[] frames = thread.GetFrames();
			
			m_frames = new LiveStackFrame[frames.Length];
			for (int i = 0; i < frames.Length; ++i)
			{
				m_frames[i] = new LiveStackFrame(thread, frames, i);
			}
		}
		
		public int Length {get {return m_frames.Length;}}
		
		// Index 0 is the furthest stack frame from main.
		public LiveStackFrame this[int index]
		{
			get {return m_frames[index];}
		}
		
		#region Equality Methods
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			LiveStack rhs = obj as LiveStack;
			return this == rhs;
		}
		
		public bool Equals(LiveStack rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(LiveStack lhs, LiveStack rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.m_frames.Length != rhs.m_frames.Length)
				return false;
			
			if (lhs.m_frames.Length > 0 && lhs.m_frames[0] != rhs.m_frames[0])
				return false;
			
			return true;
		}
		
		public static bool operator!=(LiveStack lhs, LiveStack rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += m_frames.Length.GetHashCode();
				if (m_frames.Length > 0)
					hash += m_frames[0].GetHashCode();
			}
			
			return hash;
		}
		#endregion
		
		#region Fields
		private LiveStackFrame[] m_frames;
		#endregion
	}
}
