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
public sealed class MultiCommandTest
{
	[Test]
	public void Overlapping()
	{
		string cs = @"
	using System.Collections;
	using System.Text;
	using System.Threading;
";

		CsParser.Parser parser = new CsParser.Parser();
		CsGlobalNamespace globals = parser.Parse(cs);
		Refactor refactor = new Refactor(cs);
		
		refactor.Queue(new AddUsing(globals, "System.Com"));
		refactor.Queue(new Indent(1, 30, "\t"));
		
		try			// TODO: can do this better with nunit 2.4
		{
			refactor.Process();
			Assert.Fail("expected an exception");
		}
		catch (InvalidOperationException e)
		{
			if (!e.Message.Contains("AddUsing and Indent edits overlap"))
				Assert.Fail(e.ToString());
		}
	}
	
	[Test]
	public void Wrap1()
	{
		string text = @"
internal sealed class Foo
{
	static void Process(int x)
	{
		xxx;
		yyy;
	}
}
";
		Refactor refactor = new Refactor(text);

		refactor.Queue(new InsertBeforeLine(text.IndexOf("xxx"), new string[]{"try", "{"}));
		refactor.Queue(new Indent(text.IndexOf("xxx"), 9, "\t"));
		refactor.Queue(new InsertAfterLine(text.IndexOf("yyy"), 0, new string[]{"}", "catch", "{", "}"}));

		string result= refactor.Process();
		Assert.AreEqual(@"
internal sealed class Foo
{
	static void Process(int x)
	{
		try
		{
			xxx;
			yyy;
		}
		catch
		{
		}
	}
}
", result);
	}
	
	[Test]
	public void Wrap2()
	{
		string text = @"
internal sealed class Foo
{
	static void Process(int x)
	{
		xxx;
		yyy;
	}
}
";
		Refactor refactor = new Refactor(text);

		refactor.Queue(new InsertBeforeLine(text.IndexOf("xxx"), new string[]{"try", "{"}));
		refactor.Queue(new Indent(text.IndexOf("xxx"), 9, "\t"));
		refactor.Queue(new InsertAfterLine(text.IndexOf("yyy") + 5, 7, new string[]{"}", "catch", "{", "}"}));

		string result= refactor.Process();
		Assert.AreEqual(@"
internal sealed class Foo
{
	static void Process(int x)
	{
		try
		{
			xxx;
			yyy;
		}
		catch
		{
		}
	}
}
", result);
	}
}
