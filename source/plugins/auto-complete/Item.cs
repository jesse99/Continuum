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
	// An item which may appear in the completions table. This can represent
	// a namespace, a type name, a field, a method, a local variable, etc.
	internal abstract class Item : IEquatable<Item>
	{
		// This is the text that appears in the completion table rows. It's also the
		// default for the text which is inserted.
		public string Text {get; protected set;}
		
		// This is the text that appears in the label above the table when
		// the member is selected.
		public string Label {get; protected set;}
		
		// This is used with the table's context menu to show or hide members.
		// It will usually be a full type name, "extension methods", etc.
		public string Filter {get; protected set;}
		
		// The type name of the item. This may not be the full type name and
		// may be null (e.g. if the item is a namespace).
		public string Type {get; protected set;}
		
		public override string ToString()
		{
			return Text;
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			Item rhs = obj as Item;
			return this == rhs;
		}
		
		public bool Equals(Item rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Item lhs, Item rhs)
		{
			if (object.ReferenceEquals(lhs, rhs))
				return true;
			
			if ((object) lhs == null || (object) rhs == null)
				return false;
			
			if (lhs.Text != rhs.Text)
				return false;
			
			return true;
		}
		
		public static bool operator!=(Item lhs, Item rhs)
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
