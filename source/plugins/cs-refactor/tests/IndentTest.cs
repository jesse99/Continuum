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

using CsRefactor;
using NUnit.Framework;
using Shared;
using System;

[TestFixture]
public sealed class IndentTest
{
	private string DoEdit(string cs, int offset, int length, string indent)
	{
		Refactor refactor = new Refactor(cs);
		
		refactor.Queue(new Indent(offset, length, indent));
		
		return refactor.Process();
	}
	
	[Test]
	public void Empty()
	{
		string text = @"";
		Assert.AreEqual(@"", DoEdit(text, 0, 0, "\t"));
	}
	
	[Test]
	public void FullLine()
	{
		string text = @"
xxx
yyy
zzz
";
		Assert.AreEqual(@"
xxx
	yyy
zzz
", DoEdit(text, text.IndexOf("yyy"), 4, "\t"));
	}
	
	[Test]
	public void AlmostFullLine()
	{
		string text = @"
xxx
yyy
zzz
";
		Assert.AreEqual(@"
xxx
	yyy
zzz
", DoEdit(text, text.IndexOf("yyy"), 3, "\t"));
	}
	
	[Test]
	public void PartialLine()
	{
		string text = @"
xxx
yyy
zzz
";
		Assert.AreEqual(@"
xxx
	yyy
zzz
", DoEdit(text, text.IndexOf("yyy"), 2, "\t"));
	}
	
	[Test]
	public void InteriorLine()
	{
		string text = @"
xxx
yyy
zzz
";
		Assert.AreEqual(@"
xxx
	yyy
zzz
", DoEdit(text, text.IndexOf("yyy") + 1, 1, "\t"));
	}
	
	[Test]
	public void TwoFullLines()
	{
		string text = @"
aaa
bbb
ccc
ddd
eee
";
		Assert.AreEqual(@"
aaa
bbb
	ccc
	ddd
eee
", DoEdit(text, text.IndexOf("ccc"), 8, "\t"));
	}
	
	[Test]
	public void PartialLines()
	{
		string text = @"
aaa
bbb
ccc
ddd
eee
";
		Assert.AreEqual(@"
aaa
bbb
	ccc
	ddd
eee
", DoEdit(text, text.IndexOf("ccc") + 1, 5, "\t"));
	}
}
