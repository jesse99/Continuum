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
public sealed class AddBaseTypeTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		Shared.AssertListener.Install();
	}
	
	private string DoEdit(string cs, params string[] bases)
	{
		CsParser parser = new CsParser(cs);
		CsGlobalNamespace globals = parser.Parse();
		Refactor refactor = new Refactor(cs);
		
		foreach (string b in bases)
		{
			refactor.Queue(new AddBaseType(globals.Types[0], b));
		}
		
		return refactor.Process();
	}
	
	[Test]
	public void NoBases()
	{
		string text = @"
internal sealed class Foo
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Base
{
}
", DoEdit(text, "Base"));
}
	
	[Test]
	public void Unsorted()
	{
		string text = @"
internal sealed class Foo : IAlpha, IGamma, IBaz
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : IAlpha, IGamma, IBaz, IBeta
{
}
", DoEdit(text, "IBeta"));
}
	
	[Test]
	public void Sorted1()
	{
		string text = @"
internal sealed class Foo : IAlpha, IBaz, IGamma
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : IAlpha, IBaz, IBeta, IGamma
{
}
", DoEdit(text, "IBeta"));
}
	
	[Test]
	public void Sorted2()
	{
		string text = @"
internal sealed class Foo : IAlpha, IBaz, IGamma
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : IAlpha, IBaz, IGamma, IZeta
{
}
", DoEdit(text, "IZeta"));
}
	
	[Test]
	public void BaseWithInterfaces()
	{
		string text = @"
internal sealed class Foo : IAlpha, IBaz, IGamma
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zowie, IAlpha, IBaz, IGamma
{
}
", DoEdit(text, "Zowie"));
}
	
	[Test]
	public void Sorted3()
	{
		string text = @"
internal sealed class Foo : Zeta, IAlpha, IBaz, IGamma
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zeta, IAlpha, IBaz, IBeta, IGamma
{
}
", DoEdit(text, "IBeta"));
}
	
	[Test]
	public void Unsorted2()
	{
		string text = @"
internal sealed class Foo : Zeta, IAlpha, IGamma, IBaz
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zeta, IAlpha, IGamma, IBaz, IBeta
{
}
", DoEdit(text, "IBeta"));
}
	
	[Test]
	public void Exists()
	{
		string text = @"
internal sealed class Foo : Zeta, IAlpha, IBaz, IGamma
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zeta, IAlpha, IBaz, IGamma
{
}
", DoEdit(text, "IAlpha"));
}
	
	[Test]
	public void Multiple1()
	{
		string text = @"
internal sealed class Foo
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zeta, IAlpha, IBeta
{
}
", DoEdit(text, "Zeta", "IAlpha", "IBeta"));
}
	
	[Test]
	public void Multiple2()
	{
		string text = @"
internal sealed class Foo : IAlpha, IDelta
{
}
";
		Assert.AreEqual(@"
internal sealed class Foo : Zeta, IAlpha, IDelta, IGamma
{
}
", DoEdit(text, "Zeta", "IGamma"));
}
}
