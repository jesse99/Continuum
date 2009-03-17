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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Shared
{
	// Misc handy methods for dealing with C# code.
	public static class CsHelpers
	{
		static CsHelpers()
		{
			foreach (var entry in ms_aliases)
			{
				// Note that ms_compositeAliases is just for aesthetics so we don't
				// need to handle every possible combination. 
				ms_compositeAliases.Add(entry.Key + "[]", entry.Value + "[]");
				ms_compositeAliases.Add(entry.Key + "?", entry.Value + "?");
				ms_compositeAliases.Add(entry.Key + "&", entry.Value + "&");
				
				ms_realNames.Add(entry.Value, entry.Key);
				ms_realNames.Add(entry.Value + "[]", entry.Key + "[]");
				ms_realNames.Add(entry.Value + "?", entry.Key + "?");
				ms_realNames.Add(entry.Value + "&", entry.Key + "&");
			}
		}
		
		// Given a name like "System.Boolean" or "System.Boolean[]" return
		// "bool" or "bool[]". If there is no alias then returns the original name.
		public static string GetAliasedName(string name)
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			
			string alias;
			
			if (ms_aliases.TryGetValue(name, out alias))
				return alias;
			
			if (ms_compositeAliases.TryGetValue(name, out alias))
				return alias;
				
			return name;
		}
		
		// Given a name like "bool" or "bool[]" return "System.Boolean" or 
		// "System.Boolean[]". If it is not an alias then returns the original name.
		public static string GetRealName(string name)
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			
			string real;
			
			if (ms_realNames.TryGetValue(name, out real))
				return real;
			
			return name;
		}
		
		// Returns true if the character is one which can start a C# identifier.
		public static bool CanStartIdentifier(char ch)
		{
			// letter-character:
			//       A Unicode character of class Lu, Ll, Lt, Lm, Lo, or Nl
			//       A unicode-escape-sequence representing a character of class Lu, Ll, Lt, Lm, Lo, or Nl
			if (char.IsLetter(ch))			// fast path
				return true;
				
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			switch (cat)
			{
				case UnicodeCategory.UppercaseLetter:
				case UnicodeCategory.LowercaseLetter:
				case UnicodeCategory.TitlecaseLetter:
				case UnicodeCategory.ModifierLetter:
				case UnicodeCategory.OtherLetter:
				case UnicodeCategory.LetterNumber:
					return true;
			}
			
			return false;
		}
		
		// Returns true if the character is one which can continue a C# identifier.
		public static bool CanContinueIdentifier(char ch)
		{
			// identifier-part-character:
			//      letter-character
			//      decimal-digit-character
			//      connecting-character
			//      combining-character
			//      formatting-character
			//
			// decimal-digit-character:
			//     A Unicode character of the class Nd
			//     A unicode-escape-sequence representing a character of class Nd
			// 
			// connecting-character:
			//     A Unicode character of the class Pc
			//     A unicode-escape-sequence representing a character of class Pc
			// 
			// combining-character:
			//     A Unicode character of class Mn or Mc
			//     A unicode-escape-sequence representing a character of class Mn or Mc
			// 
			// formatting-character:
			//     A Unicode character of the class Cf
			//     A unicode-escape-sequence representing a character of class Cf
			if (char.IsLetterOrDigit(ch) || ch == '_')	// fast path
				return true;
			
			if (ch == '\\')				// not quite right: should check for \u or \U
				return true;
				
			UnicodeCategory cat = char.GetUnicodeCategory(ch);
			switch (cat)
			{
				case UnicodeCategory.DecimalDigitNumber:
				case UnicodeCategory.ConnectorPunctuation:
				case UnicodeCategory.NonSpacingMark:
				case UnicodeCategory.SpacingCombiningMark:
				case UnicodeCategory.Format:
					return true;
			}
			
			return false;
		}
		
		#region Fields		
		private static Dictionary<string, string> ms_aliases = new Dictionary<string, string>
		{
			{"System.Boolean", "bool"},
			{"System.Byte", "byte"},
			{"System.Char", "char"},
			{"System.Decimal", "decimal"},
			{"System.Double", "double"},
			{"System.Int16", "short"},
			{"System.Int32", "int"},
			{"System.Int64", "long"},
			{"System.SByte", "sbyte"},
			{"System.Object", "object"},
			{"System.Single", "float"},
			{"System.String", "string"},
			{"System.UInt16", "ushort"},
			{"System.UInt32", "uint"},
			{"System.UInt64", "ulong"},
			{"System.Void", "void"},
		};
		private static Dictionary<string, string> ms_compositeAliases = new Dictionary<string, string>();
		private static Dictionary<string, string> ms_realNames = new Dictionary<string, string>();
		#endregion
	}
}
