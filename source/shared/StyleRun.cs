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
//using MCocoa;
//using Shared;
using System;
using System.Diagnostics;
//using System.Collections.Generic;
//using System.Text.RegularExpressions;
//using System.Threading;

namespace Shared
{		
	[Serializable]
	public enum StyleType : ushort		// the StyleRun lists can get large so we'd like to keep the runs as small as possible to reduce memory pressure and increase locality
	{
		Spaces,			// leading or trailing spaces
		Tabs,				// leading or trailing tabs
		
		Default,
		Keyword,
		String,			// usually includes character literals as well
		Number,
		Comment,
		Preprocessor,
		Other1,
		Other2,
		
		Member,		// name in a member declaration 
		Type,				// name in a type declaration 
		Error,			// build or parse error
		Line,				// selected line
	}
	
	public struct StyleRun : IComparable<StyleRun>, IEquatable<StyleRun>
	{
		public StyleRun(int offset, int length, StyleType type)
		{
			Debug.Assert(offset >= 0, "offset is negative");
			Debug.Assert(((type == StyleType.Line || type == StyleType.Error) && length >= 0) || length > 0, "length is not correct");
			
			Offset = offset;
			Length = length < ushort.MaxValue ? (ushort) length : ushort.MaxValue;	// length may be larger than max for weird cases like commenting out an entire large file
			Type = type;
		}
		
		public int Offset {get; private set;}
		public ushort Length {get; private set;}
		public StyleType Type {get; private set;}
		
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
