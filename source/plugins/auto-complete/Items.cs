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
//using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AutoComplete
{
	// This is used for everything but methods.
	internal sealed class NameItem : Item
	{
		public NameItem(string text, string label, string filter) : this(text, label, filter, null)
		{
		}
		
		public NameItem(string text, string label, string filter, string type)
		{
			Contract.Requires(!string.IsNullOrEmpty(text), "text is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(label), "label is null or empty");
			Contract.Requires(filter != null, "filter is null");
			
			Text = text;
			Label = label;
			Filter = filter;
			Type = type;
		}
	}
	
	// Note that this is only used for things that look like methods (i.e. it's
	// not used for properties).
	internal sealed class MethodItem : Item
	{
		public MethodItem(string rtype, string name, string[] gargs, string[] argTypes, string[] argNames, string type, string filter)
			: this(rtype, name, gargs, argTypes, argNames, type, filter, '(', ')')
		{
		}
		
		public MethodItem(string rtype, string name, string[] gargs, string[] argTypes, string[] argNames, string type, string filter, char open, char close)
		{
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			Contract.Requires(!rtype.Contains("`"), "rtype should not have a '`'");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(argTypes != null, "argTypes is null");
			Contract.Requires(argNames != null, "argNames is null");
			Contract.Requires(argTypes.Length == argNames.Length, "arg lengths don't match");
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(filter), "filter is null or empty");
			
			Text = DoGetText(name, gargs, argTypes, argNames, rtype.Length + 1, open, close);
			Label = rtype + ' ' + Text;
			Filter = filter;
			Type = type;
			
			Name = name;
			Arity = argTypes.Length;
		}
		
		public string Name {get; private set;}
		
		public int Arity {get; private set;}
		
		// Returns the range of the name in Label.
		public NSRange GetNameRange()
		{
			return m_name;
		}
		
		// Returns the range of a generic or regular argument in Label. Returns an 
		// empty range if the index is oor.
		public NSRange GetArgumentRange(int index)
		{
			NSRange result;
			
			if (!m_args.TryGetValue(index, out result))
				result = NSRange.Empty;
			
			return result;
		}
		
		#region Private Methods
		private string DoGetText(string name, string[] gargs, string[] argTypes, string[] argNames, int offset, char open, char close)
		{
			var result = new System.Text.StringBuilder();
			
			m_name = new NSRange(offset + result.Length, name.Length);
			result.Append(name);
			
			if (gargs != null && gargs.Length > 0)
			{
				result.Append('<');
				for (int i = 0; i < gargs.Length; ++i)
				{
					m_args.Add(-(i + 1), new NSRange(offset + result.Length, gargs[i].Length));
					result.Append(gargs[i]);
					
					if (i + 1 < gargs.Length)
						result.Append(", ");
				}
				result.Append('>');
			}
			
			result.Append(open);
			for (int i = 0; i < argTypes.Length; ++i)
			{
				Contract.Assert(!argTypes[i].Contains("`"), "type should not have a '`'");
				result.Append(argTypes[i]);
				result.Append(' ');
				
				m_args.Add(i + 1, new NSRange(offset + result.Length, argNames[i].Length));
				result.Append(argNames[i]);
				
				if (i + 1 < argTypes.Length)
					result.Append(", ");
			}
			result.Append(close);
			
			return result.ToString();
		}
		#endregion
		
		#region Fields
		private NSRange m_name;
		private Dictionary<int, NSRange> m_args = new Dictionary<int, NSRange>();
		#endregion
	}
}
