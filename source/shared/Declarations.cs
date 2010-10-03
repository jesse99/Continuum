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
	public sealed class Declarations : IEquatable<Declarations>
	{
		public Declarations()
		{
			Path = string.Empty;
			Decs = new Declaration[0];
		}
		
		public Declarations(string path, int edit, Declaration[] decs)
		{
			Path = path;
			Edit = edit;
			Decs = decs;
		}
		
		public string Path {get; private set;}
		
		public int Edit {get; private set;}
		
		public Declaration[] Decs {get; private set;}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Declarations rhs = obj as Declarations;
			return this == rhs;
		}
		
		public bool Equals(Declarations rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Declarations lhs, Declarations rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Path != rhs.Path)
				return false;
			
			if (lhs.Decs.Length != rhs.Decs.Length)
				return false;
				
			for (int i = 0; i < lhs.Decs.Length; ++i)
			{
				if (lhs.Decs[i] != rhs.Decs[i])
					return false;
			}
			
			return true;
		}
		
		public static bool operator!=(Declarations lhs, Declarations rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += Path.GetHashCode();
				foreach (Declaration d in Decs)
				{
					hash += d.GetHashCode();
				}
			}
			
			return hash;
		}
	}
}
