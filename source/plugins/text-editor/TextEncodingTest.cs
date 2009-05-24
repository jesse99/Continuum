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

#if TEST
using MCocoa;
using MObjc;
using NUnit.Framework;
using TextEditor;
using Shared;
using System;
using System.Runtime.InteropServices;

[TestFixture]
public class TextEncodingTest
{
	private void DoDecode(byte[] buffer, uint expectedEncoding, string expectedStr)
	{
		NSDefaultMallocZone();		// force appkit to load (TODO: might want to do this in mobj)
		Registrar.CanInit = true;
		
		var pool = NSAutoreleasePool.Create();
		
		GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
		NSData data = NSData.Alloc().initWithBytes_length(handle.AddrOfPinnedObject(), (uint) buffer.Length);
		
		var coder = new TextEncoding();
		uint actualEncoding;
		NSString str = coder.Decode(data, out actualEncoding);
		
		Assert.AreEqual(expectedEncoding, actualEncoding);
		Assert.AreEqual(expectedStr, str.description().EscapeAll());
		
		pool.release();
	}
	
	[Test]
	public void DecodeAscii()
	{
		// Note that utf8 is a superset of ascii so the decoder treats this as utf8.
		var buffer = new byte[]{0x68, 0x65, 0x6c, 0x6c, 0x6f};
		DoDecode(buffer, Enums.NSUTF8StringEncoding, "hello");
	}
	
	[Test]
	public void DecodeUtf8()
	{
		var buffer = new byte[]{0x21, 0x3d, 0xe2, 0x89, 0xa0};
		DoDecode(buffer, Enums.NSUTF8StringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeMacOSRoman()
	{
		var buffer = new byte[]{0x21, 0x3d, 0xad};
		DoDecode(buffer, Enums.NSMacOSRomanStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf16Big()
	{
		var buffer = new byte[]{0x00, 0x21, 0x00, 0x3d, 0x22, 0x60};
		DoDecode(buffer, Enums.NSUTF16BigEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf16Little()
	{
		var buffer = new byte[]{0x21, 0x00, 0x3d, 0x00, 0x60, 0x22};
		DoDecode(buffer, Enums.NSUTF16LittleEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf32Big()
	{
		var buffer = new byte[]{0x00, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00, 0x3d, 0x00, 0x00, 0x22, 0x60};
		DoDecode(buffer, Enums.NSUTF32BigEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf32Little()
	{
		var buffer = new byte[]{0x21, 0x00, 0x00, 0x00, 0x3d, 0x00, 0x00, 0x00, 0x60, 0x22, 0x00, 0x00};
		DoDecode(buffer, Enums.NSUTF32LittleEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf16BigBOM()
	{
		var buffer = new byte[]{0xFE, 0xFF, 0x00, 0x21, 0x00, 0x3d, 0x22, 0x60};
		DoDecode(buffer, Enums.NSUTF16BigEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf16LittleBOM()
	{
		var buffer = new byte[]{0xFF, 0xFE, 0x21, 0x00, 0x3d, 0x00, 0x60, 0x22};
		DoDecode(buffer, Enums.NSUTF16LittleEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf32BigBOM()
	{
		var buffer = new byte[]{0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00, 0x3d, 0x00, 0x00, 0x22, 0x60};
		DoDecode(buffer, Enums.NSUTF32BigEndianStringEncoding, "!=\\x2260");
	}
	
	[Test]
	public void DecodeUtf32LittleBOM()
	{
		var buffer = new byte[]{0xFF, 0xFE, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00, 0x3d, 0x00, 0x00, 0x00, 0x60, 0x22, 0x00, 0x00};
		DoDecode(buffer, Enums.NSUTF32LittleEndianStringEncoding, "!=\\x2260");
	}
	
	[DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
	private static extern IntPtr NSDefaultMallocZone();
}
#endif	// TEST
