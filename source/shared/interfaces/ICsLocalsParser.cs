// Copyright (C) 2008 Jesse Jones
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
using System;
using System.Diagnostics;

namespace Shared
{
	public sealed class Local : IEquatable<Local>
	{
		public Local(string type, string name, string value)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			
			Name = name;
			Type = type.TrimAll();
			
			if (value != null)
				Value = value.Trim();
		}
		
		public string Name {get; private set;}
		
		// May be "var". Will not have any whitespace.
		public string Type {get; private set;}
		
		// May be null. If present will not have leading or trailing whitespace.
		public string Value {get; private set;}
		
		public override string ToString()
		{
			string result = Type + " " + Name;
			
			if (Value != null)
				result += " = " + Value;
				
			return result;
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Local rhs = obj as Local;
			return this == rhs;
		}
		
		public bool Equals(Local rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Local lhs, Local rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Name != rhs.Name)
				return false;
			
			if (lhs.Type != rhs.Type)
				return false;
			
			if (lhs.Value != rhs.Value)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Local lhs, Local rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Name.GetHashCode();
				hash += Type.GetHashCode();
				if (Value != null)
					hash += Value.GetHashCode();
			}
			
			return hash;
		}
	}
	
	// Interface used to extract local declarations from C# method bodies.
	public interface ICsLocalsParser : IInterface
	{
		// Return the locals declared in [start, stop). The code may be arbitrarily
		// malformed and locals declared in inner scopes which are closed before
		// stop are omitted. When checking for a local the last match should be
		// used.
		Local[] Parse(string text, int start, int stop);
		
		// Attempts to parse the text at [start, stop) as a type. If successful the
		// type's name is returned and next is set to the first token after the 
		// type. If not null is returned. 
		string ParseType(string text, int start, int stop, ref Token next);
	}
}
