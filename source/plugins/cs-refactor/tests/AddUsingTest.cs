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
public sealed class AddUsingTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		Shared.AssertListener.Install();
	}
	
	private string DoEdit(string cs, params string[] names)
	{
		CsParser parser = new CsParser(cs);
		CsGlobalNamespace globals = parser.Parse();
		Refactor refactor = new Refactor(cs);
		
		foreach (string name in names)
		{
			refactor.Queue(new AddUsing(globals, name));
		}
		
		return refactor.Process();
	}
	
	[Test]
	public void NoDeclarations()
	{
		string text = @"";
		Assert.AreEqual(@"using System.IO;
", DoEdit(text, "System.IO"));
	}
	
	[Test]
	public void Unsorted()
	{
		string text = @"
	using System.Text;
	using System.Threading;
	using System.Collections;
";
		Assert.AreEqual(@"
	using System.Text;
	using System.Threading;
	using System.Collections;
	using System.IO;
", DoEdit(text, "System.IO"));
	}
	
	[Test]
	public void Sorted()
	{
		string text = @"
	using System.Collections;
	using System.Text;
	using System.Threading;
";
		Assert.AreEqual(@"
	using System.Collections;
	using System.IO;
	using System.Text;
	using System.Threading;
", DoEdit(text, "System.IO"));
	}
	
	[Test]
	public void AlreadyUsed()
	{
		string text = @"
using System.Collections;
using System.Text;
using System.Threading;
";
		Assert.AreEqual(@"
using System.Collections;
using System.Text;
using System.Threading;
", DoEdit(text, "System.Text"));
	}
		
	[Test]
	public void NoUses()
	{
		string text = @"// a comment

public enum Names {Alpha, Beta}
";
		Assert.AreEqual(@"// a comment

using System.Text;

public enum Names {Alpha, Beta}
", DoEdit(text, "System.Text"));
	}
	
	[Test]
	public void Empty()
	{
		string text = @"namespace Foo
{
}
";
		
		CsParser parser = new CsParser(text);
		CsGlobalNamespace globals = parser.Parse();

		Refactor refactor = new Refactor(text);		
		refactor.Queue(new AddUsing(globals.Namespaces[0], "System.Text"));
		string result = refactor.Process();

		Assert.AreEqual(@"namespace Foo
{
	using System.Text;
}
", result);
	}
}
