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
//using Shared;
using System;

[TestFixture]
public sealed class InsertFirstTest
{
	private string DoEdit(string cs, string lines)
	{
		CsParser.Parser parser = new CsParser.Parser();
		CsGlobalNamespace globals = parser.Parse(cs);
		Refactor refactor = new Refactor(cs);

		refactor.Queue(new InsertFirst(globals.Types[0].Methods[0].Body, lines.Split('\n')));
		
		return refactor.Process();
	}
	
	[Test]
	public void Empty()
	{
		string text = @"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	public void Process(int x)
	{
		Console.WriteLine(""on entry"");
	}
}
", DoEdit(text, "Console.WriteLine(\"on entry\");"));
}
	
	[Test]
	public void FullBody()
	{
		string text = @"
internal sealed class Foo
{
	public int Process(int x)
	{
		return x + x;
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	public int Process(int x)
	{
		Console.WriteLine(""on entry"");
		return x + x;
	}
}
", DoEdit(text, "Console.WriteLine(\"on entry\");"));
}
	
	[Test]
	public void LameBrace()
	{
		string text = @"
internal sealed class Foo
{
	public int Process(int x) {
		return x + x;
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	public int Process(int x) {
		Console.WriteLine(""on entry"");
		return x + x;
	}
}
", DoEdit(text, "Console.WriteLine(\"on entry\");"));
}
}
