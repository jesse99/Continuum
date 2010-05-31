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

#if UNUSED
namespace Debugger
{
	// System type overloads.
	internal static class ValueTextOverloads1
	{
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, bool value)
		{
			if (value)
				return "true";
			else
				return "false";
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Byte value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X1");
			else
				return value.ToString("N0");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, char value)
		{
			if (value > 0x7F && VariableController.ShowUnicode)
				return "'" + new string(value, 1) + "'";
			else
				return "'" + CharHelpers.ToText(value) + "'";
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Double value)
		{
			if (VariableController.ShowThousands)
				return value.ToString("N");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int16 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X2");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int32 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X4");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Int64 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X8");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, object value)
		{
			return value.ToString();
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, SByte value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X1");
			else
				return value.ToString("N0");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, Single value)
		{
			if (VariableController.ShowThousands)
				return value.ToString("N");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt16 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X2");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt32 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X4");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
		
		[ValueText.Overload]
		public static string GetText(ThreadMirror thread, object owner, UInt64 value)
		{
			if (VariableController.ShowHex)
				return "0x" + value.ToString("X8");
			else if (VariableController.ShowThousands)
				return value.ToString("N0");
			else
				return value.ToString("G");
		}
	}
}
#endif
