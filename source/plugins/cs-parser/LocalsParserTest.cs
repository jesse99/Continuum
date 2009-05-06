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
using NUnit.Framework;
//using Shared;
using System;
using System.Diagnostics;

namespace CsParser
{
	[TestFixture]
	public sealed class LocalsParserTest
	{
		private void DoTheCheck(string text, int start, int stop, int expectedErrors, params Local[] expected)
		{
			var parser = new LocalsParser();
			Local[] actual = parser.Parse(text, start, stop);
			
			bool equal = expected.Length == actual.Length;
			for (int i = 0; i < actual.Length && equal; ++i)
			{
				equal = expected[i] == actual[i];
			}
			
			if (!equal)
			{
				var builder = new System.Text.StringBuilder();
				builder.AppendLine(string.Empty);
				
				builder.AppendLine("Expected:");
				foreach (Local l in expected)
					builder.AppendLine("    " + l);
				
				builder.AppendLine("Actual:");
				foreach (Local l in actual)
					builder.AppendLine("    " + l);
					
				Assert.Fail(builder.ToString());
			}
			
			Assert.AreEqual(expectedErrors, parser.NumErrors);
		}
		
		private void DoCheck(string text, int start, int stop, int expectedErrors, params Local[] expected)
		{
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "{0} {1} {2}", new string('*', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('*', 10));
			DoTheCheck(text, start, stop, expectedErrors, expected);
		}
		
		private void DoCheck(string text, params Local[] expected)
		{
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "{0} {1} {2}", new string('*', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('*', 10));
			DoTheCheck(text, 0, text.Length - 1, 0, expected);
		}
		
		[TestFixtureSetUp]
		public void Init()
		{
			Log.SetLevel(TraceLevel.Verbose);
		}
		
		[Test]
		public void Empty1()
		{
			string text = @"";
			DoCheck(text, 0, 0, 0, new Local[0]);
		}
		
		[Test]
		public void Empty2()
		{
			string text = @"{}";
			DoCheck(text, 0, 2, 0, new Local[0]);
		}
		
		[Test]
		public void Trivial()
		{
			string text = @"{int x;}";
			DoCheck(text, new Local("int", "x", null));
		}
		
		[Test]
		public void ComplexTypes()
		{
			string text = @"{
	bool a;
	Dictionary<string, int> b;
	System.Collections.Generic.List<LinkedList<int>> c;
	Qualified::Name d;
	int? e;
	int* f;
	int[] g;
	int[][,,] h;
}";
			DoCheck(text,
				new Local("bool", "a", null),
				new Local("Dictionary<string, int>", "b", null),
				new Local("System.Collections.Generic.List<LinkedList<int>>", "c", null),
				new Local("Qualified::Name", "d", null),
				new Local("int?", "e", null),
				new Local("int*", "f", null),
				new Local("int[]", "g", null),
				new Local("int[][,,]", "h", null)
			);
		}
		
		[Test]
		public void Values()
		{
			string text = @"{
	bool a = true;
	int b =   x +   y  	;
	int c = Foo(x, y);
	var d = new Dictionary<int, string>();
	bool e = x < 10;
}";
			DoCheck(text,
				new Local("bool", "a", "true"),
				new Local("int", "b", "x + y"),
				new Local("int", "c", "Foo ( x , y )"),
				new Local("var", "d", "new Dictionary < int , string > ( )"),
				new Local("bool", "e", "x < 10")
			);
		}
		
		[Test]
		public void MultipleDeclarators()
		{
			string text = @"{
	int x, y, z;
	int a = Math.Max(x, y), b = x < 10;
}";
			DoCheck(text,
				new Local("int", "x", null),
				new Local("int", "y", null),
				new Local("int", "z", null),
				new Local("int", "a", "Math . Max ( x , y )"),
				new Local("int", "b", "x < 10")
			);
		}
		
		[Test]
		public void Nested1()
		{
			string text = @"{
	int x; 
	if (x == 0)
	{
		int y;
		foo(|);
	}
	int z;
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("int", "y", null)
			);
		}
		
		[Test]
		public void Nested2()
		{
			string text = @"{
	int x; 
	if (x == 0)
	{
		int y;
	}
	int z;
	foo(|);
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("int", "z", null)
			);
		}
		
		[Test]
		public void Nested3()
		{
			string text = @"{
	int x; 
	if (x == 0)
	{
		int y;
		if (y == 1)
		{
			int a;
		}
		else
		{
			int b;
			foo(|);
		}
	}
	int z;
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("int", "y", null),
				new Local("int", "b", null)
			);
		}
		
		[Test]
		public void Loops()
		{
			string text = @"
{
	int x = 33; 
	for (int i = 0; i < x; ++i)		// < is not a generic because ; is not legal there
	{
		foo(x);
	}
	foreach (char ch in s)
	{
		bar(ch);
	}
}";
			DoCheck(text, 0, text.Length - 1, 1,
				new Local("int", "x", "33"),
				new Local("int", "i", "0"),
				new Local("char", "ch", null));
		}
		
		[Test]
		public void GenericCall()
		{
			string text = @"
{
	var foo = boss.Get<IFoo>(); 
	var bar = Sum<int, int>(x, y); 
}";
			DoCheck(text,
				new Local("var", "foo", "boss . Get < IFoo > ( )"),
				new Local("var", "bar", "Sum < int , int > ( x , y )"));
		}
		
		[Test]
		public void SameScope()
		{
			string text = @"{
	int i = 10;
	for (int i = 0; i < 20; ++i)		// < is not a generic because 20 is not a type name
	{
	} 
}";
			DoCheck(text, 0, text.Length - 1, 1,
				new Local("int", "i", "10"),
				new Local("int", "i", "0"));
		}
		
		[Test]
		public void InnerScope()
		{
			string text = @"{
	int i = 10;
	if (i > 10)
	{
		for (int i = 0; i < 20; ++i)		// < is not a generic because 20 is not a type name
		{
		}";
			
			DoCheck(text, 0, text.Length - 1, 1,
				new Local("int", "i", "10"),
				new Local("int", "i", "0"));
		}
		
		[Test]
		public void LessExpr()
		{
			string text = @"{
	int i = 10;
	if (i < 10)			// < is not a generic because 10 is not a type name
	{
		for (int j = 0; i < 20; ++j)
		{
		}";
			DoCheck(text, 0, text.Length - 1, 2,
				new Local("int", "i", "10"),
				new Local("int", "j", "0"));
		}
		
		[Test]
		public void Linq()
		{
			string text = @"
{
	var x = from a in alpha select a.ToString();
}";
			DoCheck(text,
				new Local("var", "x", "from a in alpha select a . ToString ( )"));
		}
		
		[Test]
		public void TryBlock()
		{
			string text = @"
{
	int x;
	try
	{
		foo();
	}
	catch (Exception e)
	{
		|;
	}
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("Exception", "e", null)
			);
		}
		
		[Test]
		public void UsingBlock1()
		{
			string text = @"{
	int x;
	using (StreamReader s = File.OpenText(name))
	{
		|;
	}
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("StreamReader", "s", "File . OpenText ( name )")
			);
		}
		
		[Test]
		public void UsingBlock2()
		{
			string text = @"{
	int x;
	using (StreamReader s = File.OpenText(name))
	{
	}
	
	string s;
	|;
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "x", null),
				new Local("StreamReader", "s", "File . OpenText ( name )"),
				new Local("string", "s", null)
			);
		}
		
		[Test]
		public void Fixed()
		{
			string text = @"{
	int oldIndex = m_index;
	int oldLine = m_line;
	
	fixed (char* buffer = m_text)
	{
		while (delta-- > 0 && Token.Kind != TokenKind.Invalid)
		{
			DoAdvance(|);
		}
	}
}";
			DoCheck(text, 0, text.IndexOf('|'), 0,
				new Local("int", "oldIndex", "m_index"),
				new Local("int", "oldLine", "m_line"),
				new Local("char*", "buffer", "m_text")
			);
		}
		
		[Test]
		public void BadArray()
		{
			string text = @"{
	NSMutableDictionary attrs = NSMutableDictionary.Create();
	attrs.addEntriesFromDictionary(ms_attributes[""text default font changed""]);	// not a rank specifier
	attrs.addEntriesFromDictionary(ms_paragraphAttrs);
	|;
}";
			DoCheck(text, 0, text.IndexOf('|'), 1,
				new Local("NSMutableDictionary", "attrs", "NSMutableDictionary . Create ( )")
			);
		}
	}
}
#endif	// TEST
