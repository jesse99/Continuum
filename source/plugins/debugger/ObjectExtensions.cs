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

using Shared;
using System;

namespace Debugger
{
	internal static class ObjectExtensions
	{
		public static string Stringify(this object value)
		{
			if (value == null)
				return "null";
				
			else if (value.Equals(true))
				return "true";
				
			else if (value.Equals(false))
				return "false";
				
			else if (value is char)
				if ((char) value > 0x7F && VariableController.ShowUnicode)
					return "'" + new string((char) value, 1) + "'";
				else
					return "'" + CharHelpers.ToText((char) value) + "'";
				
			else if (value is SByte)
				if (VariableController.ShowHex)
					return "0x" + ((SByte) value).ToString("X1");
				else
					return ((SByte) value).ToString("N0");
				
			else if (value is Byte)
				if (VariableController.ShowHex)
					return "0x" + ((Byte) value).ToString("X1");
				else
					return ((Byte) value).ToString("N0");
				
			else if (value is Int16)
				if (VariableController.ShowHex)
					return "0x" + ((Int16) value).ToString("X2");
				else if (VariableController.ShowThousands)
					return ((Int16) value).ToString("N0");
				else
					return ((Int16) value).ToString("G");
				
			else if (value is Int32)
				if (VariableController.ShowHex)
					return "0x" + ((Int32) value).ToString("X4");
				else if (VariableController.ShowThousands)
					return ((Int32) value).ToString("N0");
				else
					return ((Int32) value).ToString("G");
				
			else if (value is Int64)
				if (VariableController.ShowHex)
					return "0x" + ((Int64) value).ToString("X8");
				else if (VariableController.ShowThousands)
					return ((Int64) value).ToString("N0");
				else
					return ((Int64) value).ToString("G");
				
			else if (value is UInt16)
				if (VariableController.ShowHex)
					return "0x" + ((UInt16) value).ToString("X2");
				else if (VariableController.ShowThousands)
					return ((UInt16) value).ToString("N0");
				else
					return ((UInt16) value).ToString("G");
				
			else if (value is UInt32)
				if (VariableController.ShowHex)
					return "0x" + ((UInt32) value).ToString("X4");
				else if (VariableController.ShowThousands)
					return ((UInt32) value).ToString("N0");
				else
					return ((UInt32) value).ToString("G");
				
			else if (value is UInt64)
				if (VariableController.ShowHex)
					return "0x" + ((UInt64) value).ToString("X8");
				else if (VariableController.ShowThousands)
					return ((UInt64) value).ToString("N0");
				else
					return ((UInt64) value).ToString("G");
				
			else if (value is Single)
				if (VariableController.ShowThousands)
					return ((Single) value).ToString("N");
				else
					return ((Single) value).ToString("G");
				
			else if (value is Double)
				if (VariableController.ShowThousands)
					return ((Double) value).ToString("N");
				else
					return ((Double) value).ToString("G");
			
			else
				return value.ToString();
		}
	}
}
