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

using Shared;
using System;
using System.Diagnostics;

namespace AutoComplete
{
	internal sealed class Member : IEquatable<Member>
	{
		public Member(string text, string type)
		{
			Trace.Assert(!string.IsNullOrEmpty(text), "text is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(type), "type is null or empty");
			
			Text = text;
			Type = type;
			ArgTypes = new string[0];
			ArgNames = new string[0];
		}
		
		public Member(string text, string[] argTypes, string[] argNames, string type)
		{
			Trace.Assert(!string.IsNullOrEmpty(text), "text is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(type), "type is null or empty");
			Trace.Assert(argTypes != null, "argTypes is null");
			Trace.Assert(argNames != null, "argNames is null");
			Trace.Assert(argTypes.Length == argNames.Length, "argNames and argTypes lengths differ");
			
			Text = text;
			Type = type;
			ArgTypes = argTypes;
			ArgNames = argNames;
		}
		
		public string Text {get; private set;}
		
		// Note that this may not be the full name.
		public string Type {get; private set;}
		
		public string[] ArgTypes {get; private set;}
		
		public string[] ArgNames {get; private set;}
		
		public override string ToString()
		{
			return Text;
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Member rhs = obj as Member;
			return this == rhs;
		}
		
		public bool Equals(Member rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Member lhs, Member rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Text != rhs.Text)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Member lhs, Member rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Text.GetHashCode();
			}
			
			return hash;
		}
	}
}
