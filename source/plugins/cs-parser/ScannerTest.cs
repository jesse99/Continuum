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
using CsParser;
using NUnit.Framework;
using Shared;
using System;

namespace CsParser
{
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
			string text = @"using System.IO;
	 	
protected override Tuple2<NSMenu, int> GetScriptsLocation()
{
}
	
";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("using", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("System", scanner.Token.Text());
			Assert.AreEqual(1, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual(".", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("IO", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(";", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("protected", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("override", scanner.Token.Text());
			Assert.AreEqual(3, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual("Tuple2", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("<", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("NSMenu", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(",", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("int", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(">", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("GetScriptsLocation", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("(", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(")", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("{", scanner.Token.Text());
			Assert.AreEqual(4, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual("}", scanner.Token.Text());
			Assert.AreEqual(5, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void String()
		{
			string text = "foo \"hmm\" xx \"aa { \" ";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("foo", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("\"hmm\"", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("xx", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("\"aa { \"", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
	
			text = "xxx \"aa\\\"b\" yyy";
			scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("xxx", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("\"aa\\\"b\"", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("yyy", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void VerbatimString()
		{
			string text = @"foo @""some
text
	goes here"" yyy";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("foo", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("\"some\ntext\n	goes here\"", scanner.Token.Text());
			Assert.AreEqual(1, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual("yyy", scanner.Token.Text());
			Assert.AreEqual(3, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
	
			text = "aa @\"bb\"\"cc\" dd";
			scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("aa", scanner.Token.Text());
			
			scanner.Advance();
			Assert.AreEqual("\"bb\"\"cc\"", scanner.Token.Text());
	
			scanner.Advance();
			Assert.AreEqual("dd", scanner.Token.Text());
		}
		
		[Test]
		public void Char()
		{
			string text = "aa 'x' bb '\\'' cc";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("aa", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("'x'", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("bb", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("'\\''", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("cc", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
	
			text = "'\\t' '\\x21' '\\u0041' '\"' '\\\\' ";
			scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("'\\t'", scanner.Token.Text()); Assert.AreEqual(TokenKind.Char, scanner.Token.Kind);
	
			scanner.Advance();
			Assert.AreEqual("'\\x21'", scanner.Token.Text()); Assert.AreEqual(TokenKind.Char, scanner.Token.Kind);
	
			scanner.Advance();
			Assert.AreEqual("'\\u0041'", scanner.Token.Text()); Assert.AreEqual(TokenKind.Char, scanner.Token.Kind);
	
			scanner.Advance();
			Assert.AreEqual("'\"'", scanner.Token.Text()); Assert.AreEqual(TokenKind.Char, scanner.Token.Kind);
	
			scanner.Advance();
			Assert.AreEqual(@"'\\'", scanner.Token.Text()); Assert.AreEqual(TokenKind.Char, scanner.Token.Kind);
	
			scanner.Advance();
			Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void SingleLineComment()
		{
			string text = @"alpha // text goes here @"" hmm
some more // foo";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("alpha", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("some", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("more", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void DelimitedComment()
		{
			string text = @"alpha /* this
is a comment */
 foo";
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("alpha", scanner.Token.Text());
			scanner.Advance(); Assert.AreEqual("foo", scanner.Token.Text());
			Assert.AreEqual(3, scanner.Token.Line);
			scanner.Advance(); Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void Identifier()
		{
			string text = @"alpha _beta @gamma";
			
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("alpha", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
					
			scanner.Advance();
			Assert.AreEqual("_beta", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("gamma", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void Preprocess()
		{
			string text = "alpha\n#region Your Name Here	\n#define Foo\n#endif\n#endregion\n";
			
			var scanner = new Scanner();
			scanner.Init(text);
			Assert.AreEqual("alpha", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Identifier, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
			
			CsPreprocess[] preprocess = scanner.Preprocess;
			Assert.AreEqual(4, preprocess.Length);
			
			Assert.AreEqual("region", preprocess[0].Name);
			Assert.AreEqual("Your Name Here", preprocess[0].Text);
			
			Assert.AreEqual("define", preprocess[1].Name);
			Assert.AreEqual("Foo", preprocess[1].Text);
			
			Assert.AreEqual("endif", preprocess[2].Name);
			Assert.AreEqual("", preprocess[2].Text);
			
			Assert.AreEqual("endregion", preprocess[3].Name);
			Assert.AreEqual("", preprocess[3].Text);
		}
		
		[Test]
		public void Integers()
		{
			string text = "15 15U 0x1F 0x1FUL";
			
			var scanner = new Scanner();
			scanner.Init(text);
			
			Assert.AreEqual("15", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("15U", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("0x1F", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("0x1FUL", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
		
		[Test]
		public void Floats()
		{
			string text = "10.4 10.4M 10.4e100 .33 .33e-3 15f 15e100";
			
			var scanner = new Scanner();
			scanner.Init(text);
			
			Assert.AreEqual("10.4", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("10.4M", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("10.4e100", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual(".33", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual(".33e-3", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("15f", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);
			
			scanner.Advance();
			Assert.AreEqual("15e100", scanner.Token.Text());
			Assert.AreEqual(TokenKind.Number, scanner.Token.Kind);

			scanner.Advance();
			Assert.AreEqual(TokenKind.Invalid, scanner.Token.Kind);
		}
	}
}
#endif	// TEST
