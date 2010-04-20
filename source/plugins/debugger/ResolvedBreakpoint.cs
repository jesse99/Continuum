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
using System;

namespace Debugger
{
	internal struct ResolvedBreakpoint : IEquatable<ResolvedBreakpoint>
	{
		public ResolvedBreakpoint(Breakpoint bp, MethodMirror method, long offset)
		{
			Contract.Requires(bp != null, "bp is null");
			Contract.Requires(method != null, "method is null");
			Contract.Requires(offset >= 0, "offset is negative");
			
			BreakPoint = bp;
			Method = method;
			Offset = offset;
		}
		
		public readonly Breakpoint BreakPoint;
		public readonly MethodMirror Method;
		public readonly long Offset;
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			if (GetType() != obj.GetType())
				return false;
			
			ResolvedBreakpoint rhs = (ResolvedBreakpoint) obj;
			return this == rhs;
		}
		
		public bool Equals(ResolvedBreakpoint rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(ResolvedBreakpoint lhs, ResolvedBreakpoint rhs)
		{
			if (lhs.Method != rhs.Method)
				return false;
			
			if (lhs.Offset != rhs.Offset)
				return false;
			
			return true;
		}
		
		public static bool operator!=(ResolvedBreakpoint lhs, ResolvedBreakpoint rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Method.GetHashCode();
				hash += Offset.GetHashCode();
			}
			
			return hash;
		}
	}
}
