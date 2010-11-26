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
		public static void Write(TextWriter writer, ThreadMirror thread, object obj, bool useThousands)
		{
			if (obj is PrimitiveValue)
			{
				string decimalFormat = useThousands ? "N0" : "G";
				string floatFormat = useThousands ? "N" : "G";
				
				var pv = (PrimitiveValue) obj;
				if (pv.Value == null)
					writer.WriteLine("null");
					
				else if (pv.Value is System.Byte)
					DoWrite(writer, (System.Byte) pv.Value, decimalFormat);
				else if (pv.Value is System.UInt16)
					DoWrite(writer, (System.UInt16) pv.Value, decimalFormat);
				else if (pv.Value is System.UInt32)
					DoWrite(writer, (System.UInt32) pv.Value, decimalFormat);
				else if (pv.Value is System.UInt64)
					DoWrite(writer, (System.UInt64) pv.Value, decimalFormat);
				
				else if (pv.Value is System.SByte)
					DoWrite(writer, (System.SByte) pv.Value, decimalFormat);
				else if (pv.Value is System.Int16)
					DoWrite(writer, (System.Int16) pv.Value, decimalFormat);
				else if (pv.Value is System.Int32)
					DoWrite(writer, (System.Int32) pv.Value, decimalFormat);
				else if (pv.Value is System.Int64)
					DoWrite(writer, (System.Int64) pv.Value, decimalFormat);
				
				else if (pv.Value is char)
					DoWrite(writer, (char) pv.Value);
				
				else if (pv.Value is float)									// note that Decimal is a StructMirror
					DoWrite(writer, (float) pv.Value, floatFormat);
				else if (pv.Value is double)
					DoWrite(writer, (double) pv.Value, floatFormat);
				
				else
					writer.WriteLine(pv.Value);
			}
			else if (obj is StringMirror)
			{
				DoWrite(writer, (StringMirror) obj);
			}
			else if (obj is ArrayMirror)
			{
				DoWrite(writer, thread, (ArrayMirror) obj);
			}
			else if (obj is EnumMirror)
			{
				writer.WriteLine(((Value) obj).ToDisplayText(thread));
			}
			else if (obj is StructMirror)
			{
				// TODO: It would be nice to special case IntPtr and show the memory that it points to.
				// But the soft debugger doesn't give us a way to get at the memory in the debuggee's
				// process.
				var struct_ = (StructMirror) obj;
				string type = struct_.Type.FullName;
				if (type == "System.DateTime")
					DoWriteStruct(writer, thread, struct_, useThousands, "f", "r");
				else
					writer.WriteLine(struct_.ToDisplayText(thread));
			}
			else if (obj is ObjectMirror)
			{
				DoWrite(writer, thread, (ObjectMirror) obj);
			}
			else if (obj is Value)
			{
				writer.WriteLine(((Value) obj).ToDisplayText(thread));
			}
			else
			{
				writer.WriteLine(obj);
			}
		}
		
		#region Private Methods
		private static void DoWrite(TextWriter writer, ThreadMirror thread, ArrayMirror value)
		{
			writer.WriteLine("Domain: {0}", value.Domain.FriendlyName);
			writer.WriteLine("IsCollected: {0}", value.IsCollected);
			writer.WriteLine("Length: {0}", value.Length);
			writer.WriteLine("Rank: {0}", value.Rank);
			writer.WriteLine();
			
			for (int i = 0; i < value.Length; ++i)
			{
				writer.Write("{0}: ", GetChildOverloads.GetArrayName(value, i));
				
				writer.WriteLine(value[i].ToDisplayText(thread));
			}
		}
		
		private static void DoWrite(TextWriter writer, System.Byte value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
		}
		
		private static void DoWrite(TextWriter writer, char value)
		{
			if (!char.IsControl(value) && !char.IsWhiteSpace(value))
				writer.WriteLine("'{0}'", value);
			writer.WriteLine("'\\x{0:X4}", (int) value);
			DoWriteUnicode(writer, (long) value);
		}
		
		private static void DoWrite(TextWriter writer, double value, string floatFormat)
		{
			writer.WriteLine(value.ToString(floatFormat));
			writer.WriteLine("{0:R}", value);
		}
		
		private static void DoWrite(TextWriter writer, float value, string floatFormat)
		{
			writer.WriteLine(value.ToString(floatFormat));
			writer.WriteLine("{0:R}", value);
		}
		
		private static void DoWrite(TextWriter writer, System.Int16 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
			
			DoWriteUnicode(writer, (long) value);
		}
		
		private static void DoWrite(TextWriter writer, System.Int32 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
			
			DoWriteUnicode(writer, (long) value);
		}
		
		private static void DoWrite(TextWriter writer, System.Int64 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
		}
		
		private static void DoWrite(TextWriter writer, ThreadMirror thread, ObjectMirror value)
		{
			writer.WriteLine("Domain: {0}", value.Domain.FriendlyName);
			writer.WriteLine("IsCollected: {0}", value.IsCollected);
			writer.WriteLine(value.ToDisplayText(thread));
		}
		
		private static void DoWrite(TextWriter writer, System.SByte value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
		}
		
		private static void DoWrite(TextWriter writer, StringMirror value)
		{
			writer.WriteLine("Domain: {0}", value.Domain.FriendlyName);
			writer.WriteLine("IsCollected: {0}", value.IsCollected);
			writer.WriteLine();
			foreach (char ch in value.Value)
			{
				if (char.IsControl(ch))
				{
					if (ch == '\r' || ch == '\n' || ch == '\t')
						writer.Write(ch);
					else
						writer.Write(CharHelpers.ToText(ch));
				}
				else
				{
					writer.Write(ch);
				}
			}
			writer.WriteLine();
			writer.WriteLine();
			
			var boss = ObjectModel.Create("TextEditorPlugin");
			var name = boss.Get<IUnicodeName>();
			for (int i = 0; i < value.Value.Length; ++i)
			{
				writer.Write("{0}: ", i);
				
				char ch = value.Value[i];
				if (char.IsControl(ch) || (int) ch >= 0x80)
				{
					string text = name.GetName(ch);
					writer.WriteLine(text);
				}
				else
				{
					writer.WriteLine("0x{0:X4} '{1}'", (int) ch, ch);
				}
			}
		}
		
		private static void DoWrite(TextWriter writer, System.UInt16 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
			
			DoWriteUnicode(writer, value);
		}
		
		private static void DoWrite(TextWriter writer, System.UInt32 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
			
			DoWriteUnicode(writer, value);
		}
		
		private static void DoWrite(TextWriter writer, System.UInt64 value, string decimalFormat)
		{
			writer.WriteLine(value.ToString(decimalFormat));
			writer.WriteLine("0x{0:X}", value);
			writer.WriteLine("0b{0}", DoHexToBinary(value.ToString("X")));
		}
		
		private static void DoWriteStruct(TextWriter writer, ThreadMirror thread, StructMirror value, bool useThousands, params string[] formats)
		{
			try
			{
				MethodMirror method = value.Type.FindMethod("ToString", 2);
				var nv = thread.VirtualMachine.CreateValue(null);
				foreach (string format in formats)
				{
					var arg = thread.Domain.CreateString(format);
					Value v = value.InvokeMethod(thread, method, new Value[]{arg, nv}, InvokeOptions.DisableBreakpoints | InvokeOptions.SingleThreaded);
					StringMirror s = (StringMirror) v;
					writer.WriteLine(s.Value);
				}
			}
			catch (Exception e)
			{
				writer.WriteLine(e.Message);
			}
		}
		
		private static void DoWriteUnicode(TextWriter writer, long value)
		{
			if (value >= 0 && value <= 65535)
			{
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
