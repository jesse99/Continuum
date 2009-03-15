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
	internal sealed class Variable : IEquatable<Variable>
	{
		public Variable(string type, string name, string value)
		{
			Trace.Assert(!string.IsNullOrEmpty(type), "type is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Type = type;
			Name = name;
			Value = value;
		}
		
		public string Type {get; private set;}
		
		public string Name {get; private set;}
		
		// May be null;
		public string Value {get; private set;}
		
		public override string ToString()
		{
			return Type + " " + Name;
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Variable rhs = obj as Variable;
			return this == rhs;
		}
		
		public bool Equals(Variable rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Variable lhs, Variable rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Type != rhs.Type)
				return false;
			
			if (lhs.Name != rhs.Name)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Variable lhs, Variable rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Type.GetHashCode();
				hash += Name.GetHashCode();
			}
			
			return hash;
		}
	}
}
