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
public sealed class InsertAfterLineTest
{
	private string DoEdit(string cs, int index, string lines)
	{
		Refactor refactor = new Refactor(cs);
		
		refactor.Queue(new InsertAfterLine(index, 0, lines.Split('\n')));
		
		return refactor.Process();
	}
	
	[Test]
	public void Middle()
	{
		string text = @"
internal sealed class Foo
{
	xxx
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	xxx
	aaa
}
", DoEdit(text, text.IndexOf("xxx"), "aaa"));
}

	[Test]
	public void Start()
	{
		string text = @"
internal sealed class Foo
{
	xxx
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	xxx
	aaa
}
", DoEdit(text, text.IndexOf("xxx") - 1, "aaa"));
}

	[Test]
	public void End()
	{
		string text = @"
internal sealed class Foo
{
	xxx
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	xxx
	aaa
}
", DoEdit(text, text.IndexOf("xxx") + 3, "aaa"));
}
}
