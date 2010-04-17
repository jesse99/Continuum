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
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Linq;

namespace Debugger
{
	internal static class MethodMirrorExtensions
	{
		// MethodMirror.FullName doesn't include parameters (with Mono 2.6) so
		// we'll roll our own. MonoDevelop also includes values for the arguments,
		// but that seems a little too busy to me (note that if we change our minds
		//  on this we'll need to pass in a fresh copy of the stack).
		public static string GetFullerName(this MethodMirror method)
		{
			var builder = new System.Text.StringBuilder();
			
			builder.Append(DoGetTypeName(method.ReturnType));
			builder.Append(' ');
			
			builder.Append(method.DeclaringType.Name);
			builder.Append('.');
			builder.Append(method.Name);
			
			builder.Append('(');
			ParameterInfoMirror[] args = method.GetParameters();
			for (int i = 0; i < args.Length; ++i)
			{
				builder.Append(DoGetTypeName(args[i].ParameterType));
				builder.Append(' ');
				builder.Append(args[i].Name);
				
				if (i + 1 < args.Length)
					builder.Append(", ");
			}
			builder.Append(')');
			
			return builder.ToString();
		}
		
		#region Private Methods
		private static string DoGetTypeName(TypeMirror type)
		{
			string result = CsHelpers.GetAliasedName(type.FullName);
			if (result == type.FullName)
				result = type.Name;
				
			return result;
		}
		#endregion
	}
	
	internal static class StackFrameExtensions
	{
		// Returns true if the two stack frames are the same.
		public static bool Matches(this StackFrame lhs, StackFrame rhs)
		{
			bool matches = false;
			
			if (lhs == rhs)					// this will use reference equality (as of mono 2.6)
			{
				matches = true;
			}
			else if (lhs != null && rhs != null)
			{
				if (lhs.Thread.Id == rhs.Thread.Id)			// note that Address can change after a GC
					if (lhs.Method.MetadataToken == rhs.Method.MetadataToken)
						if (lhs.Method.FullName == rhs.Method.FullName)	// this is kind of expensive, but we can't rely on just the metadata token (we need the assembly as well which we can't always get)
							matches = true;
			}
			
			return matches;
		}
		
		public static bool Matches(StackFrame[] lhs, StackFrame[] rhs)
		{
			bool matches = false;
			
			if (ReferenceEquals(lhs, rhs))
			{
				matches = true;
			}
			else if (lhs != null && rhs != null && lhs.Length == rhs.Length)
			{
				matches = Contract.ForAll(0, lhs.Length, i => lhs[i].Matches(rhs[i]));
			}
			
			return matches;
		}
	}
	
	internal static class TypeMirrorExtensions
	{
		public static MethodMirror FindMethod(this TypeMirror type, string name, int numArgs)
		{
			MethodMirror method = type.GetMethods().FirstOrDefault(
				m => m.Name == name && m.GetParameters().Length == numArgs);
				
			if (method == null && type.BaseType != null)
				method = FindMethod(type.BaseType, name, numArgs);
				
			return method;
		}
	}
}
