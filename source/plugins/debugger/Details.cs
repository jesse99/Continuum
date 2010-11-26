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

using System;
using System.Collections.Generic;
using System.IO;
using Gear;
using Mono.Debugger.Soft;
using Shared;

namespace Debugger
{
	// Used with the Show Details contextual menu command.
	internal static class Details
	{
		public static void Write(TextWriter writer, object obj)
		{
			if (obj is PrimitiveValue)
			{
				var pv = (PrimitiveValue) obj;
				if (pv.Value == null)
					writer.WriteLine("null");
				else if (pv.Value is System.Int32)
					DoWrite(writer, (System.Int32) pv.Value);
				else
					writer.WriteLine(pv.Value);
			}
			else
			{
				writer.WriteLine(obj);
			}
		}
		
		#region Private Methods
		private static void DoWrite(TextWriter writer, System.Int32 value)
		{
			writer.WriteLine(value);
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
			
			var boss = ObjectModel.Create("TextEditorPlugin");
			var name = boss.Get<IUnicodeName>();
			string text = name.GetName((char) value);
			if (text != "invalid code point")
			{
				int i = text.IndexOf(' ');	// get rid of the hex version of the code point
				text = text.Substring(i + 1);
				
				writer.WriteLine(text);
			}
		}
		
		private static string DoHexToBinary(string hex)
		{
			var builder = new System.Text.StringBuilder(4*hex.Length);
			
			foreach (char ch in hex)
			{
				builder.Append(ms_hexToBin[ch]);
			}
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields
		private static Dictionary<char, string> ms_hexToBin = new Dictionary<char, string>
		{
			{'0', "0000"},
			{'1', "0001"},
			{'2', "0010"},
			{'3', "0011"},
			{'4', "0100"},
			{'5', "0101"},
			{'6', "0110"},
			{'7', "0111"},
			{'8', "1000"},
			{'9', "1001"},
			
			{'A', "1010"},
			{'B', "1011"},
			{'C', "1100"},
			{'D', "1101"},
			{'E', "1110"},
			{'F', "1111"},
			
			{'a', "1010"},
			{'b', "1011"},
			{'c', "1100"},
			{'d', "1101"},
			{'e', "1110"},
			{'f', "1111"},
		};
		#endregion
	}
}
