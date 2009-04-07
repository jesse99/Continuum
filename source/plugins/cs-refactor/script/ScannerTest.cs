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
using CsRefactor;
using CsRefactor.Script;
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;

[TestFixture]
public sealed class ScannerTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		Shared.AssertListener.Install();
	}
	
	[Test]
	public void Basics()	
	{
		string text = @"define Run()
	if Scope is Type then
		Process(scope)
";
		var scanner = new Scanner(text);
		var tokens = new List<Token>();
		while (scanner.Token.IsValid())
		{
			tokens.Add(scanner.Token);
			scanner.Advance();
		}
		
		Assert.AreEqual("define", tokens[0].Text());
		Assert.AreEqual("Run", tokens[1].Text());
		Assert.AreEqual("(", tokens[2].Text());
		Assert.AreEqual(")", tokens[3].Text());
		Assert.AreEqual("if", tokens[4].Text());
		Assert.AreEqual("Scope", tokens[5].Text());
		Assert.AreEqual("is", tokens[6].Text());
		Assert.AreEqual("Type", tokens[7].Text());
		Assert.AreEqual("then", tokens[8].Text());
		Assert.AreEqual("Process", tokens[9].Text());
		Assert.AreEqual("(", tokens[10].Text());
		Assert.AreEqual("scope", tokens[11].Text());
		Assert.AreEqual(")", tokens[12].Text());
		Assert.AreEqual(13, tokens.Count);
		
		Assert.AreEqual(TokenKind.Keyword, tokens[0].Kind);
		Assert.AreEqual(TokenKind.Identifier, tokens[1].Kind);
		Assert.AreEqual(TokenKind.Punct, tokens[2].Kind);
		Assert.AreEqual(TokenKind.Punct, tokens[3].Kind);
		
		Assert.AreEqual(1, tokens[0].Line);
		Assert.AreEqual(1, tokens[2].Line);
		Assert.AreEqual(1, tokens[3].Line);
		Assert.AreEqual(2, tokens[4].Line);
		Assert.AreEqual(2, tokens[5].Line);
		Assert.AreEqual(2, tokens[6].Line);
		Assert.AreEqual(2, tokens[7].Line);
		Assert.AreEqual(2, tokens[8].Line);
		Assert.AreEqual(3, tokens[9].Line);
		Assert.AreEqual(3, tokens[10].Line);
		Assert.AreEqual(3, tokens[11].Line);
		Assert.AreEqual(3, tokens[12].Line);
	}
		
	[Test]
	public void String()
	{
		string text = @"foo ""some
text
	goes here"" yyy";
		var scanner = new Scanner(text); Assert.AreEqual("foo", scanner.Token.Text());
		scanner.Advance(); Assert.AreEqual("\"some\ntext\n	goes here\"", scanner.Token.Text());
		Assert.AreEqual(1, scanner.Token.Line);
		scanner.Advance(); Assert.AreEqual("yyy", scanner.Token.Text());
		Assert.AreEqual(3, scanner.Token.Line);
		scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);

		text = "aa \"bb\"\"cc\" dd";
		scanner = new Scanner(text);
		Assert.AreEqual("aa", scanner.Token.Text());
		
		scanner.Advance();
		Assert.AreEqual("\"bb\"\"cc\"", scanner.Token.Text());

		scanner.Advance();
		Assert.AreEqual("dd", scanner.Token.Text());
	}
	
	[Test]
	public void SingleLineComment()
	{
		string text = @"alpha # text goes here
some more # foo";
		var scanner = new Scanner(text); Assert.AreEqual("alpha", scanner.Token.Text());
		scanner.Advance(); Assert.AreEqual("some", scanner.Token.Text());
		scanner.Advance(); Assert.AreEqual("more", scanner.Token.Text());
		scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
	}
	
	[Test]
	public void Identifier()
	{
		string text = @"alpha _beta";
		
		var scanner = new Scanner(text);
		Assert.AreEqual("alpha", scanner.Token.Text());
		Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
				
		scanner.Advance();
		Assert.AreEqual("_beta", scanner.Token.Text());
		Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
				
		scanner.Advance();
		Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
	}
	
	[Test]
	[ExpectedException(typeof(CsRefactor.Script.ScannerException))]
	public void BadEol()
	{
		string text = "alpha  epsi\n\rfoo";
		
		var scanner = new Scanner(text);
		while (scanner.Token.IsValid())
		{
			scanner.Advance();
		}
	}
}
#endif	// TEST
