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
using Shared;
using System;
using System.Diagnostics;
using System.Text;

namespace AutoComplete
{
	internal sealed class ResolvedTarget
	{
		public ResolvedTarget(string typeName, CsType type, bool isInstance, bool isStatic)
		{
			Contract.Requires(!string.IsNullOrEmpty(typeName), "typeName is null or empty");
			Contract.Requires(isInstance || isStatic, "at least one of isInstance and isStatic should be true");
			
			TypeName = typeName;
			Type = type;
			IsInstance = isInstance;
			IsStatic = isStatic;
		}
		
		// Will always be set.
		public string TypeName {get; private set;}
		
		// The type will be null, a CsType, CsEnum, or CsDelegate.
		public CsType Type {get; private set;}
		
		// True if the target is an instance of the type.
		public bool IsInstance {get; private set;}
		
		// True if the target is the type. Note that IsInstance and IsStatic will both
		// be true when completing expressions.
		public bool IsStatic {get; private set;}
		
		public override string ToString()
		{
			var builder = new StringBuilder();
			
			builder.Append(TypeName);
			
			if (IsInstance && IsStatic)
				builder.Append(" [all]");
			else if (IsInstance)
				builder.Append(" [instance]");
			else
				builder.Append(" [statics]");
			
			return builder.ToString();
		}
	}
}
