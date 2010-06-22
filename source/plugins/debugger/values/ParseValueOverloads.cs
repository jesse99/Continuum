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
using Mono.Cecil;
using Mono.Debugger.Soft;
using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Debug = Debugger;

namespace Debugger
{
	internal static class ParseValueOverloads
	{
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, bool value, string text)
		{
			Value result;
			
			if (text.ToLower() == "true")
				result = thread.VirtualMachine.CreateValue(true);
			
			else if (text.ToLower() == "false")
				result = thread.VirtualMachine.CreateValue(false);
				
			else
				throw new Exception("Expected true or false.");
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Byte value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = Byte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = Byte.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);

			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, char value, string text)
		{
			char v;
			if (text.Length == 3 && text[0] == '\'' && text[2] == '\'')
				v = text[1];
			else if (text == "'\\n'")
				v = '\n';
			else if (text == "'\\r'")
				v = '\r';
			else if (text == "'\\t'")
				v = '\t';
			else if (text == "'\\f'")
				v = '\f';
			else if (text == "'\\''")
				v = '\'';
			else if (text.Length > 4 && text.StartsWith("'\\x") && text.EndsWith("'"))
				v = unchecked((char) int.Parse(text.Substring(3, text.Length - 4), NumberStyles.AllowHexSpecifier));
			else
				throw new Exception("Character format is 'x', '\\n', '\\r', '\\t', '\\f', '\\'', or '\\x9ABC'.");
			
			Value result = thread.VirtualMachine.CreateValue(v);
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, DateTime value, string text)
		{
			value = DateTime.Parse(text);
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Decimal value, string text)
		{
			value = Decimal.Parse(text);
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Double value, string text)
		{
			value = Double.Parse(text);
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, EnumMirror value, string text)
		{
			object v = null;
			
			string[] words = text.Split('|');
			foreach (string word in words)
			{
				v = DoCombineEnum(v, DoParseEnumValue(thread, value, word), value.Value.GetType().FullName);
			}
			
			if (v == null)
				throw new Exception("No text");
			
			value.Value = v;
			
			return value;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Int16 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = Int16.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = Int16.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Int32 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = Int32.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = Int32.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Int64 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = Int64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = Int64.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, IntPtr value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = new IntPtr(unchecked((long) UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier)));
			else
				value = new IntPtr(unchecked((long) UInt64.Parse(text)));
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, object value, string text)
		{
			throw new Exception("Don't know how to set the value for a " + value.GetType());
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, ObjectMirror value, string text)
		{
			Value result;
			
			if (text == "null")
			{
				result = thread.VirtualMachine.CreateValue(null);
			}
			else
			{
				throw new Exception(value.GetType() + " can only be set to null.");
			}
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, PrimitiveValue value, string text)
		{
			Value result;
			
			if (value.IsNull())
			{
				if (text.StartsWith("\"") || text.StartsWith("@\""))
					result = Parse(thread, item, (StringMirror) null, text);
				else
					throw new Exception("Null references can only be set to string values.");
			}
			else
			{
				result = ParseValue.Invoke(thread, item, value.Value, text);
			}
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, SByte value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = SByte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = SByte.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);

			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, Single value, string text)
		{
			value = Single.Parse(text);
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, StringMirror value, string text)
		{
			Value result;
			
			if (text == "null")
			{
				result = thread.VirtualMachine.CreateValue(null);
			}
			else if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
			{
				string naked = text.Substring(1, text.Length - 2);
				result = thread.Domain.CreateString(ParseNakedString(naked));
			}
			else if (text.Length >= 3 && text.StartsWith("@\"") && text.EndsWith("\""))
			{
				string naked = text.Substring(2, text.Length - 3);
				result = thread.Domain.CreateString(ParseNakedVerbatimString(naked));
			}
			else
			{
				throw new Exception("Expected a double-quoted or verbatim string.");
			}
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, StructMirror value, string text)
		{
//			if (value.Type.FullName == "System.IntPtr")
//			{
//				FieldInfoMirror[] fields = value.Type.GetFields();
//				int i = Array.FindIndex(fields, f => f.Name == "m_value");
//				if (i >= 0)
//					value.Fields[i] = Parse(thread, null, (void*) 0, text);
//				else
//					throw new Exception("Couldn't find IntPtr.m_value");
//			}
//			else
//			{
				throw new Exception("Don't know how to parse the value for a " + value.Type.FullName);
//			}
			
//			return value;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, TimeSpan value, string text)
		{
			value = TimeSpan.Parse(text);
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, UInt16 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = UInt16.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = UInt16.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, UInt32 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = UInt32.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = UInt32.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, UInt64 value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier);
			else
				value = UInt64.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands);
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		[ParseValue.Overload]
		public static Value Parse(ThreadMirror thread, VariableItem item, UIntPtr value, string text)
		{
			if (text.Length > 2 && text.StartsWith("0x"))
				value = new UIntPtr(UInt64.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier));
			else
				value = new UIntPtr(UInt64.Parse(text));
			
			Value result = thread.VirtualMachine.CreateValue(value);
			
			return result;
		}
		
		public static string ParseNakedVerbatimString(string text)
		{
			var builder = new System.Text.StringBuilder(text.Length);
			
			int i = 0;
			while (i < text.Length)
			{
				char ch = text[i++];
				
				if (ch == '"' && i < text.Length && text[i] == '"')
				{
					builder.Append('"');
					++i;
				}
				else
				{
					builder.Append(ch);
				}
			}
			
			return builder.ToString();
		}
		
		public static string ParseNakedString(string text)
		{
			var builder = new System.Text.StringBuilder(text.Length);
			
			int i = 0;
			while (i < text.Length)
			{
				char ch = text[i++];
				
				if (ch == '\\' && i + 1 < text.Length)
				{
					if (text[i] == 'n')
					{
						builder.Append('\n');
						++i;
					}
					else if (text[i] == 'r')
					{
						builder.Append('\r');
						++i;
					}
					else if (text[i] == 't')
					{
						builder.Append('\t');
						++i;
					}
					else if (text[i] == 'f')
					{
						builder.Append('\f');
						++i;
					}
					else if (text[i] == '"')
					{
						builder.Append('"');
						++i;
					}
					else if (text[i] == '\\')
					{
						builder.Append('\\');
						++i;
					}
					else if (text[i] == 'x' || text[i] == 'u')
					{
						++i;
						uint codePoint = 0;
						int count = 0;
						while (i < text.Length && DoIsHexDigit(text[i]) && count < 4)
						{
							codePoint = 16*codePoint + DoGetHexValue(text[i++]);
							++count;
						}
						builder.Append((char) codePoint);
					}
					else
					{
						builder.Append(ch);
					}
				}
				else
				{
					builder.Append(ch);
				}
			}
			
			return builder.ToString();
		}
		
		#region Private Methods
		private static object DoParseEnumValue(ThreadMirror thread, EnumMirror value, string text)
		{
			if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1])))
				text = text.TrimAll();
			
			if (text.Length > 0 && char.IsDigit(text[0]))
			{
				return ParseValue.Invoke(thread, null, value.Value, text);
			}
			else
			{
				foreach (FieldInfoMirror field in value.Type.GetFields())
				{
					if (field.IsStatic && field.Name == text)
						return (value.Type.GetValue(field) as EnumMirror).Value;
				}
				
				throw new Exception(string.Format("{0} is not the name of a {1} value.", text, value.Type.FullName));
			}
		}
		
		private static object DoCombineEnum(object lhs, object rhs, string typeName)
		{
			object result;
			
			if (lhs != null)
			{
				switch (typeName)
				{
					case "System.SByte":
						int s1 = (System.SByte) lhs;	// need the temporaries to work around compiler warning
						int s2 = (System.SByte) rhs;
						result = (System.SByte) (s1 | s2);
						break;
						
					case "System.Int16":
						result = (System.Int16) ((System.Int16) lhs | (System.Int16) rhs);	// need the extra cast because the | expression gets promoted to a larger type
						break;
						
					case "System.Int32":
						result = (System.Int32) lhs | (System.Int32) rhs;
						break;
						
					case "System.Int64":
						result = (System.Int64) lhs | (System.Int64) rhs;
						break;
						
					case "System.Byte":
						result = (System.Byte) (((System.Byte) lhs) | ((System.Byte) rhs));
						break;
						
					case "System.UInt16":
						result = (System.UInt16) ((System.UInt16) lhs | (System.UInt16) rhs);
						break;
						
					case "System.UInt32":
						result = (System.UInt32) lhs | (System.UInt32) rhs;
						break;
						
					case "System.UInt64":
						result = (System.UInt64) lhs | (System.UInt64) rhs;
						break;
						
					default:
						Contract.Assert(false, "bad type: " + typeName);
						result = null;
						break;
				}
			}
			else
			{
				result = rhs;
			}
			
			return result;
		}
		
		private static bool DoIsHexDigit(char ch)
		{
			return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
		}
		
		private static uint DoGetHexValue(char ch)
		{
			if (ch >= '0' && ch <= '9')
				return (uint) (ch - '0');
				
			else if (ch >= 'a' && ch <= 'f')
				return (uint) (ch - 'a') + 10;
			
			else
				return (uint) (ch - 'A') + 10;
		}
		#endregion
	}
}
