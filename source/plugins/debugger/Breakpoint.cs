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

using Gear.Helpers;
using System;

namespace Debugger
{
	// A breakpoint added by the user. Note that this does not become an active
	// breakpoint until the debugger loads a type with a method defined on the
	// specified file and line.
	internal sealed class Breakpoint : IEquatable<Breakpoint>
	{
		public Breakpoint(string file, int line)
		{
			Contract.Requires(!string.IsNullOrEmpty(file), "file is null or empty");
			Contract.Requires(line > 0, "line is not positive");
			
			File = file;
			Line = line;
		}
		
		// Full path to the file.
		public string File {get; private set;}
		
		public int Line {get; private set;}
		
		public override string ToString()
		{
			return string.Format("{0}:{1}", System.IO.Path.GetFileName(File), Line);
		}
		
		#region Equality
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Breakpoint rhs = obj as Breakpoint;
			return this == rhs;
		}
		
		public bool Equals(Breakpoint rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Breakpoint lhs, Breakpoint rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.File != rhs.File)
				return false;
			
			if (lhs.Line != rhs.Line)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Breakpoint lhs, Breakpoint rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 0;
			
			unchecked
			{
				hash += File.GetHashCode();
				hash += Line.GetHashCode();
			}
			
			return hash;
		}
		#endregion
	}
}
