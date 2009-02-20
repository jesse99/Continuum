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
public sealed class ChangeAccessTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		Shared.AssertListener.Install();
	}
	
	private string DoEdit(string cs, string access)
	{
		CsParser parser = new CsParser(cs);
		CsGlobalNamespace globals = parser.Parse();
		Refactor refactor = new Refactor(cs);
		
		refactor.Queue(new ChangeAccess(globals.Types[0].Methods[0], access));
		
		return refactor.Process();
	}
	
	[Test]
	public void ToPrivate()
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
	private void Process(int x)
	{
	}
}
", DoEdit(text, "private"));
}
	
	[Test]
	public void ToPublic()
	{
		string text = @"
internal sealed class Foo
{
	static protected void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	static public void Process(int x)
	{
	}
}
", DoEdit(text, "public"));
}
	
	[Test]
	public void NoAccessor()
	{
		string text = @"
internal sealed class Foo
{
	void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	internal void Process(int x)
	{
	}
}
", DoEdit(text, "internal"));
}
	
	[Test]
	public void SameAccessor()
	{
		string text = @"
internal sealed class Foo
{
	static void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	internal static void Process(int x)
	{
	}
}
", DoEdit(text, "internal"));
}
	
	[Test]
	public void InnerAccessor1()
	{
		string text = @"
internal sealed class Foo
{
	static protected void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	static internal void Process(int x)
	{
	}
}
", DoEdit(text, "internal"));
}
	
	[Test]
	public void InnerAccessor2()
	{
		string text = @"
internal sealed class Foo
{
	static /* blech */ protected void Process(int x)
	{
	}
}
";
		Assert.AreEqual(@"
internal sealed class Foo
{
	static /* blech */ internal void Process(int x)
	{
	}
}
", DoEdit(text, "internal"));
}
}
