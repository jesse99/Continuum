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

using Gear.Helpers;
using MCocoa;
using System;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public struct Declaration : IEquatable<Declaration>
	{
		public Declaration(string name, NSRange extent, bool isType, bool isDir) : this()
		{
			Name = name;
			Extent = extent;
			IsType = isType;
			IsDirective = isDir;
		}
		
		public string Name {get; private set;}
		
		public NSRange Extent {get; private set;}
		
		public bool IsType {get; private set;}
		
		public bool IsDirective {get; private set;}
		
		public override string ToString()
		{
			return string.Format("Name: {0}, Extent: {1}, IsType: {2}, IsDirective: {3}",
				Name, Extent, IsType, IsDirective);
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			if (GetType() != obj.GetType())
				return false;
			
			Declaration rhs = (Declaration) obj;
			return this == rhs;
		}
		
		public bool Equals(Declaration rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Declaration lhs, Declaration rhs)
		{
			if (lhs.Name != rhs.Name)
				return false;
						
			if (lhs.IsType != rhs.IsType)
				return false;
			
			if (lhs.IsDirective != rhs.IsDirective)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Declaration lhs, Declaration rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Name.GetHashCode();
				hash += IsType.GetHashCode();
				hash += IsDirective.GetHashCode();
			}
			
			return hash;
		}
	}
}
