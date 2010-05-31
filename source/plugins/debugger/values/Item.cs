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
using System;

namespace Debugger
{
	// Used to initialize and refresh VariableItem instances.
	internal struct Item
	{
		public Item(int count, string text, string type) : this()
		{
			Contract.Requires(count >= 0);
			Contract.Requires(text != null);
			Contract.Requires(type != null);
			
			Count = count;
			Text = text;
			Type = type;
			
			int index = Type.IndexOf("[[");	// strip off stuff like "[[System.Int32, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]"
			if (index > 0)
				Type = Type.Substring(0, index);
		}
		
		// The number of children for this value.
		public int Count {get; private set;}
		
		// String representation for the value (may be empty).
		public string Text {get; private set;}
		
		// String representation for the type (may be empty iff value is null).
		public string Type {get; private set;}
	}
}
