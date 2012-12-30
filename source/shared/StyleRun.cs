// Copyright (C) 2009 Jesse Jones
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
using Gear.Helpers;
using System;
using System.Diagnostics;

namespace Shared
{
	public struct StyleRun : IComparable<StyleRun>, IEquatable<StyleRun>
	{
		public StyleRun(int offset, int length, string type) : this()
		{
#if DEBUG
			Contract.Requires(offset >= 0, "offset is negative");
			Contract.Requires(((type == "ParseError") && length >= 0) || length > 0, "length is not correct");
#endif
			
			Offset = offset;
			Length = length < ushort.MaxValue ? (ushort) length : ushort.MaxValue;	// length may be larger than max for weird cases like commenting out an entire large file
			Type = type;
		}
		
		public int Offset {get; private set;}
		public ushort Length {get; private set;}
		public string Type {get; private set;}
		
		public override string ToString()
		{
			return string.Format("{0} [{1}, {2})", Type, Offset, Offset + Length);
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			if (GetType() != obj.GetType())
				return false;
			
			StyleRun rhs = (StyleRun) obj;
			return CompareTo(rhs) == 0;
		}
		
		public bool Equals(StyleRun rhs)
		{
			return CompareTo(rhs) == 0;
		}
		
		public int CompareTo(StyleRun rhs)
		{
			int result = 0;
			
			if (result == 0)
				result = Offset.CompareTo(rhs.Offset);
			
			if (result == 0)
				result = rhs.Length.CompareTo(Length);	// we want the longer runs to come first
			
			if (result == 0)
				result = Type.CompareTo(rhs.Type);
			
			return result;
		}
		
		public static bool operator==(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) == 0;
		}
		
		public static bool operator!=(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) != 0;
		}
		
		public static bool operator>=(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) >= 0;
		}
		
		public static bool operator>(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) > 0;
		}
		
		public static bool operator<=(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) <= 0;
		}
		
		public static bool operator<(StyleRun lhs, StyleRun rhs)
		{
			return lhs.CompareTo(rhs) < 0;
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Offset.GetHashCode();
				hash += Length.GetHashCode();
				hash += Type.GetHashCode();
			}
			
			return hash;
		}
	}
}
