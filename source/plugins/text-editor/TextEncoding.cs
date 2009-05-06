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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

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
		
		public NSString Decode(NSData data)	// threaded code
		{
			uint encoding;
			return Decode(data, out encoding);
		}
		
		public NSString Decode(NSData data, out uint encoding)	// threaded code
		{
			Contract.Requires(data != null, "data is null");
			
			NSString result = null;
			
			encoding = DoGetEncoding(data);
			if (encoding != 0)
			{
				result = DoDecode(data, encoding);
				
				// The first few bytes of most legacy documents will look like utf8 so
				// if we couldn't decode it using utf8 we need to fall back onto Mac
				// OS Roman.
				if (NSObject.IsNullOrNil(result) && encoding == Enums.NSUTF8StringEncoding)
				{
					encoding = Enums.NSMacOSRomanStringEncoding;
					result = DoDecode(data, encoding);
				}
			}
			
			if (NSObject.IsNullOrNil(result))
				throw new InvalidOperationException("Couldn't read the file as Unicode or Mac OS Roman.");	// should only happen if there are embedded control characters in the header
			
			return result;
		}
		
		public NSData Encode(NSString text, uint encoding)	// threaded code
		{
			Contract.Requires((object) text != null, "text is null");
			Contract.Requires(encoding != 0, "encoding is zero");
			
			return text.dataUsingEncoding_allowLossyConversion(encoding, false);	// TODO: might want to popup a warning if we lose stuff like accents
		}
		
		#region Private Methods
		private uint DoGetEncoding(NSData data)
		{
			uint encoding = 0;
			const int HeaderBytes = 2*64;
			
			IntPtr ptr = Marshal.AllocHGlobal(HeaderBytes);
			for (int i = 0; i < HeaderBytes; ++i)					// getBytes_length may not fill the entire buffer so we need to seed it with something
				Marshal.WriteByte(ptr, i, (byte) '?');
			data.getBytes_length(ptr, HeaderBytes);
			
			byte[] buffer = new byte[HeaderBytes];
			Marshal.Copy(ptr, buffer, 0, HeaderBytes);
			Marshal.FreeHGlobal(ptr);
			
			// Check for a BOM.
			if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
				encoding = Enums.NSUTF32BigEndianStringEncoding;
			
			else if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
				encoding = Enums.NSUTF32LittleEndianStringEncoding;
			
			else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
				encoding = Enums.NSUTF16BigEndianStringEncoding;
			
			else if (buffer[0] == 0xFF && buffer[1] == 0xFE)
				encoding = Enums.NSUTF16LittleEndianStringEncoding;
			
			// See if it looks like utf-16.
			if (encoding == 0)
			{
				if (DoLooksLikeUTF16(buffer, true, HeaderBytes))
					encoding = Enums.NSUTF16BigEndianStringEncoding;
				else if (DoLooksLikeUTF16(buffer, false, HeaderBytes))
					encoding = Enums.NSUTF16LittleEndianStringEncoding;
			}
			
			// See if it could be utf-8.
			if (encoding == 0)
			{
				if (buffer.All(b => DoIsValidUTF8(b)))
					encoding = Enums.NSUTF8StringEncoding;
			}
			
			// Fall back on Mac OS Roman.
			if (encoding == 0)
			{
				if (buffer.All(b => DoIsValidMacRoman(b)))
					encoding = Enums.NSMacOSRomanStringEncoding;
			}
			
			return encoding;
		}
		
		// Apart from the asian languages most utf16 characters will have a zero in
		// their high byte. So, if we see enough zeros we'll call the data utf16 (and
		// note that utf8 will not have zeros).
		private bool DoLooksLikeUTF16(byte[] buffer, bool bigEndian, int headerBytes)
		{
			int zeros = 0;
			int count = 0;
			
			for (int i = 0; i + 1 < headerBytes; i += 2)
			{
				if (buffer[i] != (byte) '?')		// there might be a '?' in the header (or even two together) so we'll keep going if we find one
				{
					++count;
					
					if (bigEndian)
					{
						if (buffer[i] == 0 && buffer[i + 1] != 0)
							if (DoIsControl(buffer[i + 1]))
								return false;
							else
								++zeros;
					}
					else
					{
						if (buffer[i] != 0 && buffer[i + 1] == 0)
							if (DoIsControl(buffer[i]))
								return false;
							else
								++zeros;
					}
				}
			}
			
			return zeros > 0.25*count;
		}
		
		// See http://en.wikipedia.org/wiki/UTF-8#Invalid_byte_sequences
		private bool DoIsValidUTF8(byte b)
		{
			bool valid = true;
			
			if (b == 0xC0 || b == 0xC1)
				valid = false;
			
			else if (b >= 0xF5)
				valid = false;
			
			else if (DoIsControl(b))
				valid = false;
			
			return valid;
		}
		
		private bool DoIsValidMacRoman(byte b)
		{
			bool valid = true;
			
			if (DoIsControl(b))
				valid = false;
			
			return valid;
		}
		
		private bool DoIsControl(byte b)
		{
			if (b < 0x20 && b != (byte) '\t' && b != (byte) '\n' && b != (byte) '\r')
				return true;
				
			else if (b == 0x7F)
				return true;
				
			return false;
		}
		
		private NSString DoDecode(NSData data, uint encoding)		// threaded code
		{
			NSString result = NSString.Alloc().initWithData_encoding(data, encoding);
			
			if (NSObject.IsNullOrNil(result))
				result = null;
			else
				result.autorelease();
			
			return result;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
