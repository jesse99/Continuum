// Copyright (C) 2007-2008 Jesse Jones
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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class TextEncoding : ITextEncoding
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public Tuple2<NSString, uint> Decode(NSData data)	// threaded code
		{			
			Trace.Assert(data != null, "data is null");

			// TODO: would be nice to also support utf16, but it looks like we'll 
			// need to check the data for embedded nulls and/or a BOM because NSString 
			// doesn't seem to reject invalid utf16.
			
			// First we'll try utf8 which will usually fail if the data is not utf8.
			var result = DoDecode(data, Enums.NSUTF8StringEncoding);
			
			// If that fails we'll fallback to Mac OS Roman which seems to read everything.
			if ((object) result.First == null)
				result = DoDecode(data, Enums.NSMacOSRomanStringEncoding);

			if ((object) result.First == null)
				throw new InvalidOperationException("Couldn't read the file with utf8 or Mac OS Roman.");

			return result;
		}
		
		public NSData Encode(NSString text, uint encoding)	// threaded code
		{
			Trace.Assert((object) text != null, "text is null");
			Trace.Assert(encoding != 0, "encoding is zero");
			
			return text.dataUsingEncoding_allowLossyConversion(encoding, false);	// TODO: might want to popup a warning if we lose stuff like accents
		}
		
		#region Private Methods ----------------------------------------------- 		
		private Tuple2<NSString, uint> DoDecode(NSData data, uint encoding)	// threaded code
		{
			Tuple2<NSString, uint> result;
			
			NSString s = NSString.Alloc().initWithData_encoding(data, encoding);
			s.autorelease();
			if (!NSObject.IsNullOrNil(s))
				result = Tuple.Make<NSString, uint>(s, encoding);
			else
				result = new Tuple2<NSString, uint>(null, 0);
			
			return result;
		}
		#endregion
 		
		#region Fields --------------------------------------------------------
		private Boss m_boss; 
		#endregion
	} 
}	