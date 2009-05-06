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
public sealed class AddRelativeMemberTest
{
	private string DoEdit(string cs, bool after, int i, params string[] lines)
	{
		CsParser.Parser parser = new CsParser.Parser();
		CsGlobalNamespace globals = parser.Parse(cs);
		Refactor refactor = new Refactor(cs);
		
		refactor.Queue(new AddRelativeMember(globals.Types[0].Methods[i], after, lines));
		
		return refactor.Process();
	}
	
	[Test]
	public void AddBefore1()
	{
		string text = @"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
}
";
		string lines = 
@"public int Zounds(int z)
{
	return 2*z;
}";

		Assert.AreEqual(@"
internal sealed class Foo
{
	public int Zounds(int z)
	{
		return 2*z;
	}
	
	public void Process(int x)
	{
	}
}
", DoEdit(text, false, 0, lines.Split('\n')));
}
	
	[Test]
	public void AddAfter1()
	{
		string text = @"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
}
";
		string lines = 
@"public int Zounds(int z)
{
	return 2*z;
}";

		Assert.AreEqual(@"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
	
	public int Zounds(int z)
	{
		return 2*z;
	}
}
", DoEdit(text, true, 0, lines.Split('\n')));
}
	
	[Test]
	public void AddBefore2()
	{
		string text = @"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
	
	public void Work(int x)
	{
	}
}
";
		string lines = 
@"public int Zounds(int z)
{
	return 2*z;
}";

		Assert.AreEqual(@"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
	
	public int Zounds(int z)
	{
		return 2*z;
	}
	
	public void Work(int x)
	{
	}
}
", DoEdit(text, false, 1, lines.Split('\n')));
}
	
	[Test]
	public void AddAfter2()
	{
		string text = @"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
	
	public void Work(int x)
	{
	}
}
";
		string lines = 
@"public int Zounds(int z)
{
	return 2*z;
}";

		Assert.AreEqual(@"
internal sealed class Foo
{
	public void Process(int x)
	{
	}
	
	public int Zounds(int z)
	{
		return 2*z;
	}
	
	public void Work(int x)
	{
	}
}
", DoEdit(text, true, 0, lines.Split('\n')));
}
}
