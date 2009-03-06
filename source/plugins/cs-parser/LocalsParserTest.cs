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
using Shared;
using System;

namespace CsParser
{
	[TestFixture]
	public sealed class LocalsParserTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			Shared.AssertListener.Install();
		}
		
		private void DoCheck(string text, int start, int stop, params Local[] expected)
		{
			var parser = new LocalsParser();
			Local[] actual = parser.Parse(text, start, stop);
			
			for (int i = 0; i < Math.Min(expected.Length, actual.Length); ++i)
			{
				if (expected[i] != actual[i])
					Assert.Fail("{0}: expected '{1}' but found '{2}'", i, expected[i], actual[i]);
			}
			
			if (expected.Length != actual.Length)
				Assert.Fail("expected {0} locals but found {1}", expected.Length, actual.Length);
		}
		
		private void DoCheck(string text, params Local[] expected)
		{
			DoCheck(text, 0, text.Length - 1, expected);
		}
		
		[Test]
		public void Empty1()
		{
			string text = @"";
			DoCheck(text, 0, 0, new Local[0]);
		}
		
		[Test]
		public void Empty2()
		{
			string text = @"{}";
			DoCheck(text, 0, 2, new Local[0]);
		}
		
		[Test]
		public void Trivial()
		{
			string text = @"{int x;}";
			DoCheck(text, new Local("int", "x", null));
		}
		
		[Test]
		public void Multiple()
		{
			string text = @"{int x; int y; float z;}";
			DoCheck(text, new Local("int", "x", null), new Local("int", "y", null), new Local("float", "z", null));
		}
		
		[Test]
		public void ComplexTypes()
		{
			string text = @"
	{
		Dictionary<string, int> x; 
		System.Single y; 
		float[][] z;
		int? f;
		Dictionary<string, Tuple2<int, int>> x2; 
	}";
			DoCheck(text,
				new Local("Dictionary<string,int>", "x", null),
				new Local("System.Single", "y", null),
				new Local("float[][]", "z", null),
				new Local("int?", "f", null),
				new Local("Dictionary<string, Tuple2<int, int>>", "x2", null)
			);
		}
		
		[Test]
		public void Betwixt()
		{
			string text = @"{int x;\nx.foo(\nint y;}";
			DoCheck(text, 0, text.IndexOf('('), new Local("int", "x", null));
		}
		
		[Test]
		public void Nested1()
		{
			string text = @"
	{
		int x; 
		if (x == 0)
		{
			int y;
			foo(x);
		}
		int z;
	}";
			DoCheck(text, 0, text.IndexOf("x)"),
				new Local("int", "x", null),
				new Local("int", "y", null)
			);
		}
		
		[Test]
		public void Nested2()
		{
			string text = @"
	{
		int x; 
		if (x == 0)
		{
			int y;
		}
		int z;
		foo(x);
	}";
			DoCheck(text, 0, text.IndexOf("x)"),
				new Local("int", "x", null),
				new Local("int", "z", null)
			);
		}
		
		[Test]
		public void Nested3()
		{
			string text = @"
	{
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
				foo(x);
			}
		}
		int z;
	}";
			DoCheck(text, 0, text.IndexOf("x)"),
				new Local("int", "x", null),
				new Local("int", "y", null),
				new Local("int", "b", null)
			);
		}
		
		[Test]
		public void Values()
		{
			string text = @"
	{
		int x = 33; 
		int[] y = new int[100]; 
		int[] y2 = new int[]{3, 4}; 
		float z = Math.Min(x - y, x + y);
	}";
			DoCheck(text,
				new Local("int", "x", "33"),
				new Local("int[]", "y", "new int[100]"),
				new Local("int[]", "y2", "new int[]{3, 4}"),
				new Local("float", "z", "Math.Min(x - y, x + y)"));
		}
		
		[Test]
		public void Multiple2()
		{
			string text = @"
{
	int x, y = 33; 
	float a = 1.2, b = x + y; 
}";
			DoCheck(text,
				new Local("int", "x", null),
				new Local("int", "y", "33"),
				new Local("float", "a", "1.2"),
				new Local("float", "b", "x + y"));
		}
		
		[Test]
		public void Loops()
		{
			string text = @"
{
	int x = 33; 
	for (int i = 0; i < x; ++i)
	{
		foo(x);
	}
	foreach (char ch in s)
	{
		bar(ch);
	}
}";
			DoCheck(text,
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
				new Local("var", "foo", "boss.Get<IFoo>()"),
				new Local("var", "bar", "Sum<int, int>(x, y)"));
		}
		
		[Test]
		public void SameScope()
		{
			string text = @"
{
	int i = 10;
	for (int i = 0; i < 20; ++i)
	{
	} 
}";
			DoCheck(text,
				new Local("int", "i", "10"),
				new Local("int", "i", "0"));
		}
		
		[Test]
		public void InnerScope()
		{
			string text = @"
{
	int i = 10;
	if (i > 10)
	{
		for (int i = 0; i < 20; ++i)
		{
		}";
			DoCheck(text,
				new Local("int", "i", "10"),
				new Local("int", "i", "0"));
		}
		
		[Test]
		public void ArrayTypes()
		{
			string text = @"{int[] x;}";
			DoCheck(text, new Local("int[]", "x", null));
			
			text = @"{int[,] x;}";
			DoCheck(text, new Local("int[,]", "x", null));
			
			text = @"{int[][] x;}";
			DoCheck(text, new Local("int[][]", "x", null));
			
			text = @"{int[,][,,] x;}";
			DoCheck(text, new Local("int[,][,,]", "x", null));
			
			text = @"{
int[] data = new int[]{1, 2, 3};
string name;
}";
			DoCheck(text, new Local("int[]", "data", "new int[]{1, 2, 3}"), new Local("string", "name", null));
		}
		
		[Test]
		public void NullableTypes()
		{
			string text = @"{int? x;}";
			DoCheck(text, new Local("int?", "x", null));
			
			text = @"{int[,]? x;}";
			DoCheck(text, new Local("int[,]?", "x", null));
		}
	}
}
#endif	// TEST
